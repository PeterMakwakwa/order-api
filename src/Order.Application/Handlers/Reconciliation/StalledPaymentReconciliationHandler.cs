using Microsoft.Extensions.Hosting;
using Order.Application.Handlers.Payments;
using Order.Application.Models.Configuration;
using Order.Application.Models.Configuration.Reconciliation;
using Order.Domain.Interfaces.Payments.Models;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Domain.Observability.Metrics;
using Order.Domain.UnitOfWork;
using Order.Domain.UnitOfWork.Exceptions;
using Domain = Order.Domain.Entities;

namespace Order.Application.Handlers.Reconciliation
{
    /// <summary>
    /// Settles orders left PendingPayment because the process charging them died between charging and recording the
    /// outcome. Such an order is charged again with the same provider idempotency key (its order id): if the first charge
    /// went through, the provider returns that result instead of charging twice; if it never arrived, the customer is
    /// charged now. The outcome is recorded exactly as the place-order handler records it, including the outbox message.
    /// Each order is handled in its own unit of work; the order's concurrency token means that if a client retry or
    /// another instance settles it first, this one's save fails and the order is skipped.
    /// </summary>
    internal sealed class StalledPaymentReconciliationHandler(IObservabilityManager observabilityManager,
        IUnitOfWorkFactory<IOrderUnitOfWork> unitOfWorkFactory,
        OrderPaymentProcessor orderPaymentProcessor,
        OrderConfiguration configuration,
        TimeProvider timeProvider) : BackgroundService
    {
        private readonly ReconciliationConfiguration _reconciliationConfiguration = configuration.ApplicationConfiguration.Reconciliation;

        public async Task ReconcileStalledPaymentsAsync(CancellationToken cancellationToken)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            IReadOnlyList<Guid> orderIds;
            using (IOrderUnitOfWork unitOfWork = unitOfWorkFactory.Create())
            {
                orderIds = await unitOfWork
                    .OrderRepository
                    .GetStalledPaymentOrderIdsAsync(timeProvider.GetUtcNow() - _reconciliationConfiguration.StalledPaymentTimeout,
                        _reconciliationConfiguration.BatchSize, cancellationToken);
            }

            foreach (Guid orderId in orderIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await ReconcileAsync(orderId, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One order failing (for example the database blipping) must not stop the others; it is retried next poll.
                    observabilityManager.LogMessage(string.Format(ErrorMessages.PaymentReconciliationErrorMessage, orderId)).AsError(ex);
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (PeriodicTimer timer = new(_reconciliationConfiguration.PollingInterval, timeProvider))
            {
                do
                {
                    try
                    {
                        await ReconcileStalledPaymentsAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        observabilityManager.LogMessage(ErrorMessages.PaymentReconciliationProcessingErrorMessage).AsError(ex);
                    }
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
        }

        private async Task ReconcileAsync(Guid orderId, CancellationToken cancellationToken)
        {
            using (IOrderUnitOfWork unitOfWork = unitOfWorkFactory.Create())
            {
                Domain::Order? order = await unitOfWork.OrderRepository.GetByUniqueIdAsync(orderId, cancellationToken);
                if (order == null || !order.IsPaymentStalledAt(timeProvider.GetUtcNow(), _reconciliationConfiguration.StalledPaymentTimeout))
                {
                    return;
                }

                PaymentResult paymentResult = await orderPaymentProcessor.ChargeAsync(order);

                try
                {
                    await orderPaymentProcessor.RecordOutcomeAsync(unitOfWork, order, paymentResult);
                }
                catch (ConcurrencyConflictException)
                {
                    observabilityManager.LogMessage(string.Format(InfoMessages.PaymentReconciliationSkipped, orderId)).AsInfo();
                    return;
                }

                observabilityManager
                    .RecordMetric(MetricDefinitions.PaymentReconciledCount)
                    .Record(1, new KeyValuePair<string, object?>(MetricDimensions.OrderStatus, order.Status.ToString()));
                observabilityManager.LogMessage(string.Format(InfoMessages.PaymentReconciled, orderId, order.PaymentStartedDate, order.Status)).AsWarning();
            }
        }
    }
}