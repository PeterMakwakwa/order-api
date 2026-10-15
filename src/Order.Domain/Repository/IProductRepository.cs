using Order.Domain.Entities;
using Order.Domain.Repository.Base;

namespace Order.Domain.Repository
{
    /// <summary>
    /// Products are read straight from the database, so an order always gets the current price.
    /// </summary>
    public interface IProductRepository : IRepository<Product>
    {
        /// <summary>
        /// The products with these ids (not tracked); unknown ids are left out.
        /// </summary>
        public Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);
    }
}