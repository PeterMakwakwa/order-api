using Order.Domain.Commands;
using Order.Domain.Entities.Base;
using Order.Domain.Entities.Exceptions;
using Order.Domain.Entities.Messages;

namespace Order.Domain.Entities
{
    /// <summary>
    /// Child entity of the <see cref="Order"/> aggregate. It can only be created through the root,
    /// and it snapshots the product name and price at the time the order is placed.
    /// </summary>
    public sealed class OrderLineItem : Entity<long>
    {
        public const int MaxQuantity = 1000;

        private OrderLineItem()
            : base(0)
        {
            OrderId = 0;
            ProductId = 0;
            ProductName = string.Empty;
            UnitPrice = 0;
            Quantity = 0;
        }

        internal OrderLineItem(CreateOrderLineItem createOrderLineItemCommand)
            : this()
        {
            Process(createOrderLineItemCommand);
        }

        public long OrderId { get; private set; }

        public int ProductId { get; private set; }

        public string ProductName { get; private set; }

        public decimal UnitPrice { get; private set; }

        public int Quantity { get; private set; }

        public decimal LineTotal => UnitPrice * Quantity;

        internal void IncreaseQuantity(int quantity)
        {
            EnsureValidQuantity(quantity);

            // Compared as long, so merging two large quantities cannot overflow past the check.
            EnsureValidQuantity((long)Quantity + quantity);

            Quantity += quantity;
        }

        private void Process(CreateOrderLineItem createOrderLineItemCommand)
        {
            EnsureValidQuantity(createOrderLineItemCommand.Quantity);

            if (createOrderLineItemCommand.UnitPrice < 0)
            {
                throw new DomainRuleException(DomainErrorMessages.LineItemUnitPriceInvalidErrorMessage);
            }

            ProductId = createOrderLineItemCommand.ProductId;
            ProductName = createOrderLineItemCommand.ProductName;
            UnitPrice = createOrderLineItemCommand.UnitPrice;
            Quantity = createOrderLineItemCommand.Quantity;
        }

        private static void EnsureValidQuantity(long quantity)
        {
            if (quantity <= 0 || quantity > MaxQuantity)
            {
                throw new DomainRuleException(string.Format(DomainErrorMessages.LineItemQuantityInvalidErrorMessage, MaxQuantity));
            }
        }
    }
}