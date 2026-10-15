using Order.Application.Events.V1.Produced;
using Order.Domain.Commands;
using Order.Domain.Entities;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Interfaces.Payments.Models;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Domain.Observability.Metrics;
using Order.Domain.UnitOfWork;
using System.Text.Json;
using Domain = Order.Domain.Entities;

namespace Order.Application.Handlers.Payments
{
    /// <summary>
    /// Charges an order that is PendingPayment and records the outcome, together with the OrderPlaced outbox message when
    /// the payment was accepted. Shared by the place-order handler and the reconciliation of orders left PendingPayment,
    /// so an order is settled the same way whichever of them charges it.
    /// </summary>
    internal sealed class OrderPaymentProcessor(IObservabilityManager observabilityManager,
        IPaymentGatewayResolver paymentGatewayResolver,
        TimeProvider timeProvider)
    {
        private const string Currency = "USD";

        /// <summary>
        /// Charged with CancellationToken.None: the order is already saved as PendingPayment, and if the caller went away
        /// mid-charge the provider might take the money while the order stayed pending for ever. The gateway's own
        /// timeout still bounds the call. The order id is the provider's idempotency key, so charging the same order
        /// again can never charge twice.
        /// </summary>
        public async Task<PaymentResult> ChargeAsync(Domain::Order order)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            IPaymentGateway paymentGateway = paymentGatewayResolver.Resolve(order.PaymentMethod);
            long startTimestamp = timeProvider.GetTimestamp();

            PaymentResult paymentResult = await paymentGateway.ChargeAsync(new PaymentRequest
            {
                OrderId = order.UniqueId,
                CustomerId = order.CustomerId,
                Amount = order.Total,
                Currency = Currency,
                PaymentMethod = order.PaymentMethod
            }, CancellationToken.None);

            long elapsedMilliseconds = (long)timeProvider.GetElapsedTime(startTimestamp).TotalMilliseconds;
            RecordPaymentMetrics(paymentGateway.Name, paymentResult, elapsedMilliseconds);
            observabilityManager.LogMessage(string.Format(InfoMessages.PaymentChargeCompleted,
                order.UniqueId, paymentGateway.Name, paymentResult.Outcome, elapsedMilliseconds)).AsInfo();

            return paymentResult;
        }

        /// <summary>
        /// Settles the order and saves it in one go with the outbox message. Throws ConcurrencyConflictException if
        /// another request or processor changed the order since it was loaded.
        /// </summary>
        public async Task RecordOutcomeAsync(IOrderUnitOfWork unitOfWork, Domain::Order order, PaymentResult paymentResult)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            order.Process(new RecordPaymentOutcome
            {
                Outcome = paymentResult.Outcome,
                FailureReason = paymentResult.Reason
            });

            if (paymentResult.IsAccepted)
            {
                await AddOutboxMessageAsync(unitOfWork, order);
            }

            // CancellationToken.None: the customer may already have been charged, so the outcome
            // must be recorded even if the caller has gone away.
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        private async Task AddOutboxMessageAsync(IOrderUnitOfWork unitOfWork, Domain::Order order)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            await unitOfWork
                .OrderPlacedOutboxRepository
                .AddAsync(new OrderPlacedOutboxMessage(new CreateOrderPlacedOutboxMessage
                {
                    Version = OrderPlaced.Version,
                    Message = JsonSerializer.Serialize(BuildOrderPlacedEvent(order)),
                    CreatedDate = timeProvider.GetUtcNow()
                }), CancellationToken.None);
        }

        private void RecordPaymentMetrics(string gatewayName, PaymentResult paymentResult, long elapsedMilliseconds)
        {
            KeyValuePair<string, object?> gatewayTag = new(MetricDimensions.PaymentGateway, gatewayName);
            KeyValuePair<string, object?> outcomeTag = new(MetricDimensions.PaymentOutcome, paymentResult.Outcome.ToString());

            observabilityManager
                .RecordMetric(MetricDefinitions.PaymentChargeCount)
                .Record(1, gatewayTag, outcomeTag);

            observabilityManager
                .RecordMetric(MetricDefinitions.PaymentChargeDuration)
                .Record(elapsedMilliseconds, gatewayTag, outcomeTag);
        }

        private OrderPlaced BuildOrderPlacedEvent(Domain::Order order)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            return new OrderPlaced
            {
                Id = order.UniqueId,
                CorrelationId = order.CorrelationId,
                CustomerId = order.CustomerId,
                CustomerEmail = order.CustomerEmail,
                Total = order.Total,
                PaymentMethod = order.PaymentMethod,
                Status = order.Status,
                CreatedDate = order.CreatedDate
            };
        }
    }
}