using Microsoft.EntityFrameworkCore;
using Order.Domain.Entities;
using Order.Domain.Repository;
using Order.Infrastructure.Persistence.Repositories.Base;

namespace Order.Infrastructure.Persistence.Repositories
{
    public sealed class PromotionRepository(DbSet<Promotion> dbSet)
        : RepositoryBase<Promotion>(dbSet), IPromotionRepository
    {
        public Task<Promotion?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            string normalizedCode = Promotion.Normalize(code);

            return _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == normalizedCode, cancellationToken);
        }
    }
}