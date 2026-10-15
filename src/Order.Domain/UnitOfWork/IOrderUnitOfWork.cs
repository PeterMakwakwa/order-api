using Order.Domain.Repository;

namespace Order.Domain.UnitOfWork
{
    public interface IOrderUnitOfWork : IUnitOfWork
    {
        public IOrderRepository OrderRepository { get; }

        public IProductRepository ProductRepository { get; }

        public IPromotionRepository PromotionRepository { get; }

        public IOrderPlacedOutboxRepository OrderPlacedOutboxRepository { get; }
    }
}