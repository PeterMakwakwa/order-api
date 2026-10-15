using Order.Application.Handlers.Abstractions;
using Order.Application.Handlers.Exceptions;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Request.V1.Order;
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
    /// Read side: uses a projection from the repository and never loads the aggregate.
    /// </summary>
    internal sealed class GetCustomerOrderHistoryRequestHandler(IObservabilityManager observabilityManager,
        IUnitOfWorkFactory<IOrderUnitOfWork> unitOfWorkFactory) : IRequestHandler<GetCustomerOrderHistoryRequest, GetCustomerOrderHistoryResponse>
    {
        public async Task<GetCustomerOrderHistoryResponse> HandleAsync(GetCustomerOrderHistoryRequest request, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            try
            {
                (IReadOnlyList<OrderSummary> orderSummaries, int totalCount) = await GetOrderSummariesAsync(request, cancellationToken);

                observabilityManager
                    .RecordMetric(MetricDefinitions.GetCustomerOrderHistoryRequestSuccessCount)
                    .Record();

                return BuildGetCustomerOrderHistoryResponse(request, orderSummaries, totalCount);
            }
            catch (Exception ex)
            {
                observabilityManager
                    .RecordMetric(MetricDefinitions.GetCustomerOrderHistoryRequestFailedCount)
                    .Record();

                throw new HandlerException(ErrorMessages.HandlerGenericErrorMessage, ex);
            }
        }

        private async Task<(IReadOnlyList<OrderSummary> OrderSummaries, int TotalCount)> GetOrderSummariesAsync(GetCustomerOrderHistoryRequest request,
            CancellationToken cancellationToken)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            using (IOrderUnitOfWork unitOfWork = unitOfWorkFactory.Create())
            {
                int totalCount = await unitOfWork
                    .OrderRepository
                    .CountByCustomerAsync(request.CustomerId, cancellationToken);

                // The validator has already checked the cursor.
                OrderHistoryCursor.TryDecode(request.Cursor, out OrderHistoryCursor? cursor);

                // One more than the page size, to tell whether there is a next page without another query.
                IReadOnlyList<OrderSummary> orderSummaries = await unitOfWork
                    .OrderRepository
                    .GetSummariesByCustomerAsync(request.CustomerId,
                        cursor == null ? null : (cursor.CreatedDate, cursor.Position),
                        request.PageSize + 1,
                        cancellationToken);

                return (orderSummaries, totalCount);
            }
        }

        private GetCustomerOrderHistoryResponse BuildGetCustomerOrderHistoryResponse(GetCustomerOrderHistoryRequest request,
            IReadOnlyList<OrderSummary> orderSummaries, int totalCount)
        {
            observabilityManager.LogMessage(InfoMessages.MethodStarted).AsDebug();

            IReadOnlyList<OrderSummary> page = [.. orderSummaries.Take(request.PageSize)];
            OrderSummary? last = orderSummaries.Count > request.PageSize ? page[^1] : null;

            return new GetCustomerOrderHistoryResponse
            {
                CustomerId = request.CustomerId,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                NextCursor = last == null ? null : new OrderHistoryCursor { CreatedDate = last.CreatedDate, Position = last.Position }.Encode(),
                Orders = [.. page.Select(summary => new OrderSummaryResponse
                {
                    Id = summary.Id,
                    Total = summary.Total,
                    Status = summary.Status,
                    PaymentMethod = summary.PaymentMethod,
                    DateCreated = summary.CreatedDate.UtcDateTime
                })]
            };
        }
    }
}