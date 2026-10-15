using Order.Domain.Entities;
using Order.Domain.Repository.Base;

namespace Order.Domain.Repository
{
    public interface IPromotionRepository : IRepository<Promotion>
    {
        /// <summary>
        /// The promotion with this code (case-insensitive), whether or not it is currently applicable.
        /// </summary>
        Task<Promotion?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    }
}