using Order.Domain.Repository.Base;
using Order.Domain.Repository.ReadModels;
using Domain = Order.Domain.Entities;

namespace Order.Domain.Repository
{
    public interface IOrderRepository : IRepository<Domain::Order>
    {
        /// <summary>
        /// The order a customer already placed with this idempotency key, if any. Tracked, so a payment that failed with
        /// a provider error can be retried on the same order.
        /// </summary>
        Task<Domain::Order?> GetByIdempotencyKeyAsync(int customerId, string idempotencyKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// The orders still PendingPayment whose payment attempt started before <paramref name="startedBefore"/>, oldest first.
        /// </summary>
        Task<IReadOnlyList<Guid>> GetStalledPaymentOrderIdsAsync(DateTimeOffset startedBefore, int batchSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// One order with its line items, tracked so it can be changed and saved.
        /// </summary>
        Task<Domain::Order?> GetByUniqueIdAsync(Guid orderId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read side: up to <paramref name="take"/> of a customer's orders, newest first, projected straight to summaries
        /// without loading the aggregate. Keyset paging: with a <paramref name="before"/> position, only the orders older than
        /// it (by creation date, then position) are returned, so a page costs the same however deep it is.
        /// </summary>
        Task<IReadOnlyList<OrderSummary>> GetSummariesByCustomerAsync(int customerId, (DateTimeOffset CreatedDate, long Position)? before, int take,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Read side: how many orders a customer has, for paging.
        /// </summary>
        Task<int> CountByCustomerAsync(int customerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read side: one order with its line items, projected without tracking. With a <paramref name="customerId"/>, only
        /// if the order belongs to that customer, so another customer's order is never even read.
        /// </summary>
        Task<OrderDetail?> GetDetailAsync(Guid orderId, int? customerId, CancellationToken cancellationToken = default);
    }
}