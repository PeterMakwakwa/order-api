using Order.Domain.Commands;
using Order.Domain.Entities.Base;
using Order.Domain.Entities.Exceptions;
using Order.Domain.Entities.Messages;
using Order.Domain.Enums;

namespace Order.Domain.Entities
{
    /// <summary>
    /// Aggregate root. An order can only be created complete and valid (<see cref="Order(CreateOrder)"/>) and then
    /// settled (<see cref="Process(RecordPaymentOutcome)"/>), so the totals, discount rules and status transitions are
    /// enforced in one place. Only a provider error can be retried (<see cref="RetryPayment"/>); every other outcome is final.
    /// </summary>
    public sealed class Order : Aggregate<long>
    {
        public const int MaxLineItems = 100;

        private readonly List<OrderLineItem> _lineItems;

        /// <summary>
        /// For Entity Framework only.
        /// </summary>
        private Order()
            : base(0)
        {
            UniqueId = Guid.Empty;
            CorrelationId = string.Empty;
            IdempotencyKey = string.Empty;
            RequestHash = string.Empty;
            CustomerId = 0;
            CustomerEmail = string.Empty;
            PaymentMethod = PaymentMethods.CreditCard;
            Status = OrderStatus.PendingPayment;
            DiscountRate = 0;
            Subtotal = 0;
            Total = 0;
            CreatedDate = DateTimeOffset.MinValue;
            PaymentStartedDate = DateTimeOffset.MinValue;
            ConcurrencyToken = Guid.Empty;
            _lineItems = [];
        }

        public Order(CreateOrder createOrderCommand)
            : this()
        {
            Process(createOrderCommand);
        }

        public Guid UniqueId { get; private set; }

        public string CorrelationId { get; private set; }

        public string IdempotencyKey { get; private set; }

        public string RequestHash { get; private set; }

        public int CustomerId { get; private set; }

        public string CustomerEmail { get; private set; }

        public PaymentMethods PaymentMethod { get; private set; }

        public OrderStatus Status { get; private set; }

        /// <summary>
        /// The provider's answer to the charge; null until the order is settled.
        /// </summary>
        public PaymentOutcome? PaymentOutcome { get; private set; }

        public string? PromotionCode { get; private set; }

        public decimal DiscountRate { get; private set; }

        public decimal Subtotal { get; private set; }

        public decimal Total { get; private set; }

        public string? PaymentFailureReason { get; private set; }

        public DateTimeOffset CreatedDate { get; private set; }

        /// <summary>
        /// When the current payment attempt started: the order's creation, or its latest retry. An order still
        /// PendingPayment long after this was abandoned mid-charge (see <see cref="IsPaymentStalledAt"/>).
        /// </summary>
        public DateTimeOffset PaymentStartedDate { get; private set; }

        /// <summary>
        /// Changes on every status change; used as the optimistic concurrency token, so two requests can never
        /// both move the same order on (for example, both retry its payment).
        /// </summary>
        public Guid ConcurrencyToken { get; private set; }

        public IReadOnlyCollection<OrderLineItem> LineItems => _lineItems.AsReadOnly();

        public bool IsSettled => Status != OrderStatus.PendingPayment;

        /// <summary>
        /// The provider could not be reached or failed, so the customer was not charged and the same order (and the
        /// same provider idempotency key) can be charged again.
        /// </summary>
        public bool CanRetryPayment => Status == OrderStatus.PaymentFailed && PaymentOutcome == Enums.PaymentOutcome.ProviderError;

        /// <summary>
        /// Still PendingPayment more than <paramref name="timeout"/> after the payment attempt started: the request charging
        /// it died between charging and recording the outcome, so the order has to be reconciled with the provider.
        /// </summary>
        public bool IsPaymentStalledAt(DateTimeOffset moment, TimeSpan timeout)
        {
            return Status == OrderStatus.PendingPayment && PaymentStartedDate + timeout < moment;
        }

        /// <summary>
        /// Moves an order whose payment failed with a provider error back to PendingPayment, so it can be charged again.
        /// </summary>
        public void RetryPayment(DateTimeOffset retriedDate)
        {
            if (!CanRetryPayment)
            {
                throw new DomainRuleException(string.Format(DomainErrorMessages.OrderPaymentNotRetryableErrorMessage, UniqueId, Status, PaymentOutcome));
            }

            Status = OrderStatus.PendingPayment;
            PaymentOutcome = null;
            PaymentFailureReason = null;
            PaymentStartedDate = retriedDate;
            ConcurrencyToken = Guid.NewGuid();
        }

        /// <summary>
        /// Records the result of the payment attempt and moves the order to its final status:
        /// Succeeded → Completed, Pending (bank transfer) → AwaitingPayment, anything else → PaymentFailed.
        /// An order can only be settled once.
        /// </summary>
        public void Process(RecordPaymentOutcome recordPaymentOutcomeCommand)
        {
            if (IsSettled)
            {
                throw new DomainRuleException(string.Format(DomainErrorMessages.OrderAlreadySettledErrorMessage, UniqueId, Status));
            }

            if (!Enum.IsDefined(recordPaymentOutcomeCommand.Outcome))
            {
                throw new DomainRuleException(string.Format(DomainErrorMessages.OrderPaymentOutcomeInvalidErrorMessage, recordPaymentOutcomeCommand.Outcome));
            }

            PaymentOutcome = recordPaymentOutcomeCommand.Outcome;
            Status = recordPaymentOutcomeCommand.Outcome switch
            {
                Enums.PaymentOutcome.Succeeded => OrderStatus.Completed,
                Enums.PaymentOutcome.Pending => OrderStatus.AwaitingPayment,
                _ => OrderStatus.PaymentFailed
            };
            PaymentFailureReason = Status == OrderStatus.PaymentFailed
                ? recordPaymentOutcomeCommand.FailureReason ?? recordPaymentOutcomeCommand.Outcome.ToString()
                : null;
            ConcurrencyToken = Guid.NewGuid();
        }

        private void Process(CreateOrder createOrderCommand)
        {
            EnsureValid(createOrderCommand);

            UniqueId = Guid.NewGuid();
            CorrelationId = createOrderCommand.CorrelationId;
            IdempotencyKey = createOrderCommand.IdempotencyKey;
            RequestHash = createOrderCommand.RequestHash;
            CustomerId = createOrderCommand.CustomerId;
            CustomerEmail = createOrderCommand.CustomerEmail;
            PaymentMethod = createOrderCommand.PaymentMethod;
            PromotionCode = createOrderCommand.PromotionCode;
            DiscountRate = createOrderCommand.DiscountRate;
            CreatedDate = createOrderCommand.CreatedDate;
            PaymentStartedDate = createOrderCommand.CreatedDate;
            Status = OrderStatus.PendingPayment;
            ConcurrencyToken = Guid.NewGuid();

            ProcessLineItems(createOrderCommand.LineItems);
            RecalculateTotals();
        }

        private static void EnsureValid(CreateOrder createOrderCommand)
        {
            if (createOrderCommand.CustomerId <= 0)
            {
                throw new DomainRuleException(DomainErrorMessages.OrderCustomerIdInvalidErrorMessage);
            }

            if (string.IsNullOrWhiteSpace(createOrderCommand.CustomerEmail))
            {
                throw new DomainRuleException(DomainErrorMessages.OrderCustomerEmailInvalidErrorMessage);
            }

            if (string.IsNullOrWhiteSpace(createOrderCommand.IdempotencyKey) || string.IsNullOrWhiteSpace(createOrderCommand.RequestHash))
            {
                throw new DomainRuleException(DomainErrorMessages.OrderIdempotencyKeyRequiredErrorMessage);
            }

            if (!Enum.IsDefined(createOrderCommand.PaymentMethod))
            {
                throw new DomainRuleException(string.Format(DomainErrorMessages.OrderPaymentMethodInvalidErrorMessage, createOrderCommand.PaymentMethod));
            }

            if (createOrderCommand.DiscountRate < 0 || createOrderCommand.DiscountRate >= 1)
            {
                throw new DomainRuleException(DomainErrorMessages.OrderDiscountRateInvalidErrorMessage);
            }

            if (createOrderCommand.DiscountRate > 0 && string.IsNullOrWhiteSpace(createOrderCommand.PromotionCode))
            {
                throw new DomainRuleException(DomainErrorMessages.OrderDiscountWithoutPromotionErrorMessage);
            }

            if (createOrderCommand.LineItems.Count == 0)
            {
                throw new DomainRuleException(DomainErrorMessages.OrderLineItemsRequiredErrorMessage);
            }

            if (createOrderCommand.LineItems.Count > MaxLineItems)
            {
                throw new DomainRuleException(string.Format(DomainErrorMessages.OrderTooManyLineItemsErrorMessage, MaxLineItems));
            }
        }

        private void ProcessLineItems(List<CreateOrderLineItem> lineItems)
        {
            _lineItems.Clear();

            foreach (CreateOrderLineItem lineItem in lineItems)
            {
                OrderLineItem? existing = _lineItems.SingleOrDefault(li => li.ProductId == lineItem.ProductId);
                if (existing == null)
                {
                    _lineItems.Add(new OrderLineItem(lineItem));
                }
                else
                {
                    existing.IncreaseQuantity(lineItem.Quantity);
                }
            }
        }

        private void RecalculateTotals()
        {
            Subtotal = _lineItems.Sum(li => li.LineTotal);
            Total = decimal.Round(Subtotal - (Subtotal * DiscountRate), 2, MidpointRounding.AwayFromZero);
        }
    }
}