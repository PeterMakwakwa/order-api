using Microsoft.EntityFrameworkCore;
using Order.Domain.Entities;
using Order.Domain.Repository;
using Order.Infrastructure.Persistence.Repositories.Base;

namespace Order.Infrastructure.Persistence.Repositories
{
    public sealed class ProductRepository(DbSet<Product> dbSet)
        : RepositoryBase<Product>(dbSet), IProductRepository
    {
        public async Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .ToListAsync(cancellationToken);
        }
    }
}