using Order.Application.Constants;
using Order.Application.Handlers.Abstractions;
using Order.Application.Handlers.Exceptions;
using Order.Application.Handlers.Payments;
using Order.Application.Helpers;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Request.V1.Order;
using Order.Application.Models.Response.V1;
using Order.Domain.Commands;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Payments.Models;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Domain.Observability.Metrics;
using Order.Domain.UnitOfWork;
using Order.Domain.UnitOfWork.Exceptions;
using System.Diagnostics;
using Domain = Order.Domain.Entities;

namespace Order.Application.Handlers.Request.V1.Commands
{
    /// <summary>
    /// Flow: (request validated by the controller) → replay an earlier request with the same Idempotency-Key →
    /// check the products and promotion code → build the aggregate → save it as PendingPayment → charge → record
    /// the outcome together with the OrderPlaced outbox message.
    /// An earlier request with the same key whose payment failed with a provider error is not replayed: the same order is
    /// charged again (the provider idempotency key is the order id, so this can never charge twice).
    /// The confirmation email is sent by <see cref="Outbox.OrderPlacedOutboxMessageHandler"/>.
    /// </summary>
    internal sealed class PlaceOrderRequestHandler(IObservabilityManager observabilityManager,
        IUnitOfWorkFactory<IOrderUnitOfWork> unitOfWorkFactory,
        OrderPaymentProcessor orderPaymentProcessor,
        TimeProvider timeProvider) : IRequestHandler<PlaceOrderRequest, PlaceOrderResponse>
    {
        private const int CentsPerUnit = 100;

        public async Task<PlaceOrderResponse> HandleAsync(PlaceOrderRequest request, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            bool isReplay = false;
            using (Activity? activity = observabilityManager.CreateTrace(InfoMessages.PlaceOrderTrace).Start())
            {
                try
                {
                    string correlationId = headers.GetRequiredValue(RequestHeaderKeys.CorrelationId);
                    string idempotencyKey = headers.GetRequiredValue(RequestHeaderKeys.IdempotencyKey);
                    string requestHash = RequestHashHelper.Compute(NormalizeForHash(request));

                    using (IOrderUnitOfWork unitOfWork = unitOfWorkFactory.Create())
                    {
                        Domain::Order? order = await unitOfWork.OrderRepository.GetByIdempotencyKeyAsync(request.CustomerId, idempotencyKey, cancellationToken);
                        if (order == null)
                        {
                            order = await CreateOrderAsync(unitOfWork, request, correlationId, idempotencyKey, requestHash, cancellationToken);
                        }
                        else
                        {
                            EnsureSameRequest(order, requestHash);

                            if (!order.CanRetryPayment)
                            {
                                isReplay = true;
                                return Replay(order);
                            }

                            await RetryPaymentAsync(unitOfWork, order);
                        }

                        activity?.SetTag("order.id", order.UniqueId);

                        PaymentResult paymentResult = await orderPaymentProcessor.ChargeAsync(order);
                        await orderPaymentProcessor.RecordOutcomeAsync(unitOfWork, order, paymentResult);
                        EnsurePaymentAccepted(order);

                        RecordSuccessMetrics(order);
                        observabilityManager.LogMessage(string.Format(InfoMessages.OrderPlaced, order.UniqueId, order.Status)).AsInfo();

                        return BuildPlaceOrderResponse(order, isReplay: false);
                    }
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    RecordFailureMetric(request, ex.GetBaseException(), isReplay);

                    throw new HandlerException(ErrorMessages.HandlerGenericErrorMessage, ex);
                }
            }
        }

        /// <summary>
        /// The request as the order sees it, so that requests placing the same order hash the same: items merged per product
        /// and sorted, and the promotion code in its stored form. Otherwise a retry listing the same items in another order,
        /// or the code in another case, would be rejected as a reused idempotency key.
        /// </summary>
        private static PlaceOrderRequest NormalizeForHash(PlaceOrderRequest request)
        {
            return request with
            {
                Items = [.. request.Items
                    .GroupBy(item => item.ProductId)
                    .OrderBy(group => group.Key)
                    .Select(group => new OrderItem { ProductId = group.Key, Quantity = group.Sum(item => item.Quantity) })],
                PromotionCode = request.PromotionCode == null ? null : Promotion.Normalize(request.PromotionCode)
            };
        }

        /// <summary>
        /// Same key and a different request: reject.
        /// </summary>
        private static void EnsureSameRequest(Domain::Order existingOrder, string requestHash)
        {
            if (!string.Equals(existingOrder.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new IdempotencyKeyReusedException(ResponseMessages.IdempotencyKeyReused);
            }
        }

        /// <summary>
        /// Same key and same request: return the original outcome (the success, or the same final payment error) without
        /// charging again. Still pending: the first request is in flight.
        /// </summary>
        private PlaceOrderResponse Replay(Domain::Order existingOrder)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            if (!existingOrder.IsSettled)
            {
                throw new IdempotentRequestInProgressException(ResponseMessages.IdempotentRequestInProgress);
            }

            observabilityManager
                .RecordMetric(MetricDefinitions.PlaceOrderRequestReplayedCount)
                .Record(1, new KeyValuePair<string, object?>(MetricDimensions.OrderStatus, existingOrder.Status.ToString()));
            observabilityManager.LogMessage(string.Format(InfoMessages.OrderReplayed, existingOrder.UniqueId)).AsInfo();

            EnsurePaymentAccepted(existingOrder);

            return BuildPlaceOrderResponse(existingOrder, isReplay: true);
        }

        private async Task<Domain::Order> CreateOrderAsync(IOrderUnitOfWork unitOfWork, PlaceOrderRequest request,
            string correlationId, string idempotencyKey, string requestHash, CancellationToken cancellationToken)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            DateTimeOffset now = timeProvider.GetUtcNow();
            Dictionary<string, string[]> errors = [];

            List<CreateOrderLineItem> lineItems = await BuildLineItemsAsync(unitOfWork, request.Items, errors, cancellationToken);
            Promotion? promotion = await GetApplicablePromotionAsync(unitOfWork, request.PromotionCode, now, errors, cancellationToken);

            if (errors.Count != 0)
            {
                throw new OrderValidationException(ResponseMessages.OrderReferencesInvalid, errors);
            }

            Domain::Order order = new(request.BuildCommand(correlationId, idempotencyKey, requestHash,
                promotion?.Code, promotion?.DiscountRate ?? 0, lineItems, now));

            // Saved before charging so the order id can be the payment idempotency key and
            // there is always a record of an order whose payment was attempted.
            await unitOfWork.OrderRepository.AddAsync(order, cancellationToken);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueConstraintViolationException)
            {
                // A concurrent request with the same Idempotency-Key saved its order first.
                throw new IdempotentRequestInProgressException(ResponseMessages.IdempotentRequestInProgress);
            }

            return order;
        }

        /// <summary>
        /// Moves the order back to PendingPayment before charging it again. The concurrency token makes sure that of two
        /// retries arriving together only one charges; the other is told the request is still in progress.
        /// </summary>
        private async Task RetryPaymentAsync(IOrderUnitOfWork unitOfWork, Domain::Order order)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            order.RetryPayment(timeProvider.GetUtcNow());

            try
            {
                // CancellationToken.None: once saved, the charge below must go ahead and its outcome be recorded.
                await unitOfWork.SaveChangesAsync(CancellationToken.None);
            }
            catch (ConcurrencyConflictException)
            {
                throw new IdempotentRequestInProgressException(ResponseMessages.IdempotentRequestInProgress);
            }

            observabilityManager.LogMessage(string.Format(InfoMessages.PaymentRetried, order.UniqueId)).AsInfo();
        }

        private async Task<List<CreateOrderLineItem>> BuildLineItemsAsync(IOrderUnitOfWork unitOfWork, List<OrderItem> items,
            Dictionary<string, string[]> errors, CancellationToken cancellationToken)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            int[] productIds = [.. items.Select(i => i.ProductId).Distinct()];
            Dictionary<int, Product> productsById = (await unitOfWork
                    .ProductRepository
                    .GetByIdsAsync(productIds, cancellationToken))
                .ToDictionary(p => p.Id);

            string[] unknownProducts = [.. productIds
                .Where(id => !productsById.ContainsKey(id))
                .Select(id => string.Format(ResponseMessages.RequestValidationFailure, string.Format(ResponseMessages.ItemProductNotFound, id)))];
            if (unknownProducts.Length != 0)
            {
                errors[nameof(PlaceOrderRequest.Items)] = unknownProducts;
                return [];
            }

            return [.. items.Select(item => new CreateOrderLineItem
            {
                ProductId = item.ProductId,
                ProductName = productsById[item.ProductId].Name,
                UnitPrice = productsById[item.ProductId].Price,
                Quantity = item.Quantity
            })];
        }

        private async Task<Promotion?> GetApplicablePromotionAsync(IOrderUnitOfWork unitOfWork, string? promotionCode, DateTimeOffset now,
            Dictionary<string, string[]> errors, CancellationToken cancellationToken)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            if (promotionCode == null)
            {
                return null;
            }

            Promotion? promotion = await unitOfWork.PromotionRepository.GetByCodeAsync(promotionCode, cancellationToken);
            if (promotion == null || !promotion.IsApplicableAt(now))
            {
                errors[nameof(PlaceOrderRequest.PromotionCode)] =
                    [string.Format(ResponseMessages.RequestValidationFailure, string.Format(ResponseMessages.PromotionCodeInvalid, promotionCode))];
                return null;
            }

            return promotion;
        }

        /// <summary>
        /// Turns a settled order that was not accepted into the matching exception (402, 409 or 503), for the
        /// first request and for every replay of it.
        /// </summary>
        private void EnsurePaymentAccepted(Domain::Order order)
        {
            if (order.Status != OrderStatus.PaymentFailed)
            {
                return;
            }

            observabilityManager.LogMessage(string.Format(WarningMessages.PaymentFailed,
                order.UniqueId, order.PaymentOutcome, order.PaymentFailureReason)).AsWarning();

            throw order.PaymentOutcome switch
            {
                PaymentOutcome.Duplicate => new DuplicatePaymentException(string.Format(ErrorMessages.DuplicatePaymentErrorMessage, order.UniqueId)),
                PaymentOutcome.Rejected => new PaymentDeclinedException(string.Format(ErrorMessages.PaymentDeclinedErrorMessage, order.UniqueId, order.PaymentFailureReason)),
                _ => new PaymentProviderUnavailableException(string.Format(ErrorMessages.PaymentProviderUnavailableErrorMessage, order.UniqueId))
            };
        }

        /// <summary>
        /// Requests the client has to correct (validation, idempotency) count as rejected. Everything else counts as failed,
        /// except a replay of an earlier failure, which is already counted (and is recorded as replayed).
        /// </summary>
        private void RecordFailureMetric(PlaceOrderRequest request, Exception baseException, bool isReplay)
        {
            KeyValuePair<string, object?> paymentMethodTag = new(MetricDimensions.PaymentMethod, request.PaymentMethod.ToString());
            KeyValuePair<string, object?> reasonTag = new(MetricDimensions.FailureReason, baseException.GetType().Name);

            if (baseException is OrderValidationException or IdempotencyKeyReusedException or IdempotentRequestInProgressException)
            {
                observabilityManager
                    .RecordMetric(MetricDefinitions.PlaceOrderRequestRejectedCount)
                    .Record(1, paymentMethodTag, reasonTag);
            }
            else if (!isReplay)
            {
                observabilityManager
                    .RecordMetric(MetricDefinitions.PlaceOrderRequestFailedCount)
                    .Record(1, paymentMethodTag, reasonTag);
            }
        }

        private void RecordSuccessMetrics(Domain::Order order)
        {
            KeyValuePair<string, object?> paymentMethodTag = new(MetricDimensions.PaymentMethod, order.PaymentMethod.ToString());

            observabilityManager
                .RecordMetric(MetricDefinitions.PlaceOrderRequestSuccessCount)
                .Record(1, paymentMethodTag, new KeyValuePair<string, object?>(MetricDimensions.OrderStatus, order.Status.ToString()));

            observabilityManager
                .RecordMetric(MetricDefinitions.OrderTotalAmount)
                .Record((long)(order.Total * CentsPerUnit), paymentMethodTag);

            observabilityManager
                .RecordMetric(MetricDefinitions.OrderLineItemCount)
                .Record(order.LineItems.Count, paymentMethodTag);
        }

        private PlaceOrderResponse BuildPlaceOrderResponse(Domain::Order order, bool isReplay)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            return new PlaceOrderResponse
            {
                Id = order.CorrelationId,
                OrderId = order.UniqueId,
                PromotionCode = order.PromotionCode,
                Subtotal = order.Subtotal,
                Total = order.Total,
                Status = order.Status,
                DateCreated = order.CreatedDate.UtcDateTime,
                IsReplay = isReplay
            };
        }
    }
}