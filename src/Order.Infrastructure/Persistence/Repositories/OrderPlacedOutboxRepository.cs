using Microsoft.EntityFrameworkCore;
using Order.Domain.Entities;
using Order.Domain.Repository;
using Order.Infrastructure.Persistence.Repositories.Base;

namespace Order.Infrastructure.Persistence.Repositories
{
    public sealed class OrderPlacedOutboxRepository(DbSet<OrderPlacedOutboxMessage> dbSet)
        : RepositoryBase<OrderPlacedOutboxMessage>(dbSet), IOrderPlacedOutboxRepository
    {
        public async Task<IReadOnlyList<long>> GetClaimableIdsAsync(int batchSize, int maxAttempts, DateTimeOffset moment, CancellationToken cancellationToken = default)
        {
            return await Pending(maxAttempts)
                .Where(x => x.LockedUntil == null || x.LockedUntil < moment)
                .OrderBy(x => x.CreatedDate)
                .Select(x => x.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        public Task<OrderPlacedOutboxMessage?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            return _dbSet.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public Task<int> CountPendingAsync(int maxAttempts, CancellationToken cancellationToken = default)
        {
            return Pending(maxAttempts).CountAsync(cancellationToken);
        }

        private IQueryable<OrderPlacedOutboxMessage> Pending(int maxAttempts)
        {
            return _dbSet.Where(x => x.ProcessedDate == null && x.Attempts < maxAttempts);
        }
    }
}