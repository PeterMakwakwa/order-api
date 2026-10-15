using Order.Application.Handlers.Abstractions;
using Order.Application.Handlers.Exceptions;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Response.V1;
using Order.Application.Models.Response.V1.Order;
using Order.Domain.Observability.Interfaces;
using Order.Domain.Observability.Messages;
using Order.Domain.Observability.Metrics;
using Order.Domain.Repository.ReadModels;
using Order.Domain.UnitOfWork;

namespace Order.Application.Handlers.Request.V1.Queries
{
    /// <summary>
    /// Read side: one order with its line items, from a projection (the aggregate is never loaded).
    /// The query only matches the caller's own orders (unless the request has no customer id: support staff), so another
    /// customer's order is reported as not found and never read.
    /// </summary>
    internal sealed class GetOrderRequestHandler(IObservabilityManager observabilityManager,
        IUnitOfWorkFactory<IOrderUnitOfWork> unitOfWorkFactory) : IRequestHandler<GetOrderRequest, GetOrderResponse>
    {
        public async Task<GetOrderResponse> HandleAsync(GetOrderRequest request, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            try
            {
                OrderDetail orderDetail = await GetOrderDetailAsync(request, cancellationToken)
                    ?? throw new OrderNotFoundException(string.Format(ResponseMessages.OrderNotFound, request.OrderId));

                observabilityManager
                    .RecordMetric(MetricDefinitions.GetOrderRequestSuccessCount)
                    .Record();

                return BuildGetOrderResponse(orderDetail);
            }
            catch (Exception ex)
            {
                observabilityManager
                    .RecordMetric(MetricDefinitions.GetOrderRequestFailedCount)
                    .Record(1, new KeyValuePair<string, object?>(MetricDimensions.FailureReason, ex.GetBaseException().GetType().Name));

                throw new HandlerException(ErrorMessages.HandlerGenericErrorMessage, ex);
            }
        }

        private async Task<OrderDetail?> GetOrderDetailAsync(GetOrderRequest request, CancellationToken cancellationToken)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            using (IOrderUnitOfWork unitOfWork = unitOfWorkFactory.Create())
            {
                return await unitOfWork
                    .OrderRepository
                    .GetDetailAsync(request.OrderId, request.CustomerId, cancellationToken);
            }
        }

        private GetOrderResponse BuildGetOrderResponse(OrderDetail orderDetail)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            return new GetOrderResponse
            {
                OrderId = orderDetail.Id,
                CustomerId = orderDetail.CustomerId,
                Status = orderDetail.Status,
                PaymentMethod = orderDetail.PaymentMethod,
                PromotionCode = orderDetail.PromotionCode,
                DiscountRate = orderDetail.DiscountRate,
                Subtotal = orderDetail.Subtotal,
                Total = orderDetail.Total,
                DateCreated = orderDetail.CreatedDate.UtcDateTime,
                Items = [.. orderDetail.LineItems.Select(lineItem => new OrderLineItemResponse
                {
                    ProductId = lineItem.ProductId,
                    ProductName = lineItem.ProductName,
                    UnitPrice = lineItem.UnitPrice,
                    Quantity = lineItem.Quantity
                })]
            };
        }
    }
}