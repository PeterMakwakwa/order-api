using Microsoft.EntityFrameworkCore;
using Order.Domain.Repository.Base;
using System.Linq.Expressions;

namespace Order.Infrastructure.Persistence.Repositories.Base
{
    /// <summary>
    /// Queries are tracked so loaded aggregates can be changed and saved through the unit of work.
    /// </summary>
    public abstract class RepositoryBase<TEntity>(DbSet<TEntity> dbSet) : IRepository<TEntity>
        where TEntity : class
    {
        protected readonly DbSet<TEntity> _dbSet = dbSet;

        public async Task<IEnumerable<TEntity>> GetAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet.ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(predicate)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            await _dbSet.AddAsync(entity, cancellationToken);
        }

        public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            await _dbSet.AddRangeAsync(entities, cancellationToken);
        }
    }
}