using Order.Domain.Entities;
using Order.Domain.Repository.Base;

namespace Order.Domain.Repository
{
    public interface IOrderPlacedOutboxRepository : IRepository<OrderPlacedOutboxMessage>
    {
        /// <summary>
        /// Ids of the oldest messages that are unprocessed, have attempts left and are not claimed by another processor.
        /// </summary>
        Task<IReadOnlyList<long>> GetClaimableIdsAsync(int batchSize, int maxAttempts, DateTimeOffset moment, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tracked, so it can be claimed and updated.
        /// </summary>
        Task<OrderPlacedOutboxMessage?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        Task<int> CountPendingAsync(int maxAttempts, CancellationToken cancellationToken = default);
    }
}