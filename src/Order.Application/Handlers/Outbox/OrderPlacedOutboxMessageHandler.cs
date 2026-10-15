using Microsoft.Extensions.Hosting;
using Order.Application.Events.Exceptions;
using Order.Application.Events.V1.Produced;
using Order.Application.Models.Configuration;
using Order.Application.Models.Configuration.Outbox;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Email;
using Order.Domain.Interfaces.Email.Models;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Domain.Observability.Metrics;
using Order.Domain.UnitOfWork;
using Order.Domain.UnitOfWork.Exceptions;
using System.Globalization;
using System.Text.Json;

namespace Order.Application.Handlers.Outbox
{
    /// <summary>
    /// Polls the queueOrderPlaced outbox and sends the confirmation email. Each message is handled in its own unit of work:
    /// claim (lease + concurrency token, so several instances never send the same message at once) → send → record the
    /// result. A failure records the attempt and error and the message is retried on a later poll (up to MaxAttempts), so a
    /// broken mail server delays confirmations but never fails an order or blocks other messages.
    /// </summary>
    public sealed class OrderPlacedOutboxMessageHandler(IObservabilityManager observabilityManager,
        IUnitOfWorkFactory<IOrderUnitOfWork> unitOfWorkFactory,
        IEmailSender emailSender,
        OrderConfiguration configuration,
        TimeProvider timeProvider) : BackgroundService
    {
        private const string ConfirmationSubject = "Order Confirmation";

        private readonly OutboxConfiguration _outboxConfiguration = configuration.ApplicationConfiguration.Outbox;

        public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            IReadOnlyList<long> messageIds;
            using (IOrderUnitOfWork unitOfWork = unitOfWorkFactory.Create())
            {
                messageIds = await unitOfWork
                    .OrderPlacedOutboxRepository
                    .GetClaimableIdsAsync(_outboxConfiguration.BatchSize, _outboxConfiguration.MaxAttempts, timeProvider.GetUtcNow(), cancellationToken);
            }

            foreach (long messageId in messageIds)
            {
                await ProcessMessageAsync(messageId, cancellationToken);
            }

            await RecordPendingCountAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (PeriodicTimer timer = new(_outboxConfiguration.PollingInterval, timeProvider))
            {
                do
                {
                    try
                    {
                        await ProcessPendingMessagesAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        observabilityManager.LogMessage(ErrorMessages.OutboxProcessingErrorMessage).AsError(ex);
                    }
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
        }

        private async Task ProcessMessageAsync(long messageId, CancellationToken cancellationToken)
        {
            using (IOrderUnitOfWork unitOfWork = unitOfWorkFactory.Create())
            {
                OrderPlacedOutboxMessage? message = await ClaimAsync(unitOfWork, messageId, cancellationToken);
                if (message == null)
                {
                    return;
                }

                try
                {
                    OrderPlaced orderPlaced = Deserialize(message);
                    await emailSender.SendAsync(BuildConfirmationEmail(orderPlaced), cancellationToken);
                    message.MarkAsProcessed(timeProvider.GetUtcNow());

                    observabilityManager
                        .RecordMetric(MetricDefinitions.OutboxMessageProcessedCount)
                        .Record();
                    observabilityManager.LogMessage(string.Format(InfoMessages.OutboxMessageProcessed, message.Id)).AsInfo();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.RecordFailedAttempt(ex.ToString());

                    observabilityManager
                        .RecordMetric(MetricDefinitions.OutboxMessageFailedCount)
                        .Record(1, new KeyValuePair<string, object?>(MetricDimensions.FailureReason, ex.GetBaseException().GetType().Name));
                    observabilityManager.LogMessage(string.Format(WarningMessages.OutboxMessageFailed, message.Id, message.Attempts)).AsWarning(ex);
                }

                // CancellationToken.None: the email may already have gone out, so record it even while shutting down.
                await unitOfWork.SaveChangesAsync(CancellationToken.None);
            }
        }

        /// <summary>
        /// Reserves the message for this processor. Returns null if it was processed, used up its attempts or was
        /// claimed by another processor since it was listed.
        /// </summary>
        private async Task<OrderPlacedOutboxMessage?> ClaimAsync(IOrderUnitOfWork unitOfWork, long messageId, CancellationToken cancellationToken)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();

            OrderPlacedOutboxMessage? message = await unitOfWork.OrderPlacedOutboxRepository.GetByIdAsync(messageId, cancellationToken);
            if (message == null || !message.IsClaimableAt(now, _outboxConfiguration.MaxAttempts))
            {
                return null;
            }

            message.Claim(now + _outboxConfiguration.LockDuration);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return message;
            }
            catch (ConcurrencyConflictException)
            {
                observabilityManager
                    .RecordMetric(MetricDefinitions.OutboxMessageClaimConflictCount)
                    .Record();
                observabilityManager.LogMessage(string.Format(InfoMessages.OutboxMessageClaimedElsewhere, messageId)).AsDebug();

                return null;
            }
        }

        private async Task RecordPendingCountAsync(CancellationToken cancellationToken)
        {
            using (IOrderUnitOfWork unitOfWork = unitOfWorkFactory.Create())
            {
                int pendingCount = await unitOfWork
                    .OrderPlacedOutboxRepository
                    .CountPendingAsync(_outboxConfiguration.MaxAttempts, cancellationToken);

                observabilityManager
                    .RecordMetric(MetricDefinitions.OutboxPendingMessageCount)
                    .Record(pendingCount);
            }
        }

        private static OrderPlaced Deserialize(OrderPlacedOutboxMessage message)
        {
            if (message.Version != OrderPlaced.Version)
            {
                throw new OutboxMessageDeserializationException(string.Format(WarningMessages.OutboxMessageVersionUnsupported, message.Id, message.Version));
            }

            try
            {
                return JsonSerializer.Deserialize<OrderPlaced>(message.Message)
                    ?? throw new OutboxMessageDeserializationException(ErrorMessages.OutboxMessageDeserializationErrorMessage);
            }
            catch (JsonException ex)
            {
                throw new OutboxMessageDeserializationException(ErrorMessages.OutboxMessageDeserializationErrorMessage, ex);
            }
        }

        private static EmailMessage BuildConfirmationEmail(OrderPlaced orderPlaced)
        {
            // Invariant culture: the amount is in USD and must not pick up the server's decimal separator.
            string body = orderPlaced.Status == OrderStatus.AwaitingPayment
                ? string.Create(CultureInfo.InvariantCulture, $"Your order {orderPlaced.Id} has been placed and is awaiting your bank transfer. Total: ${orderPlaced.Total:0.00}")
                : string.Create(CultureInfo.InvariantCulture, $"Your order {orderPlaced.Id} has been placed. Total: ${orderPlaced.Total:0.00}");

            return new EmailMessage
            {
                To = orderPlaced.CustomerEmail,
                Subject = ConfirmationSubject,
                Body = body,
                // Delivery is at-least-once; a confirmation re-sent after a crash keeps the same Message-ID.
                DeduplicationKey = $"order-confirmation.{orderPlaced.Id:N}"
            };
        }
    }
}