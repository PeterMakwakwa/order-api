using Microsoft.EntityFrameworkCore;
using Order.Domain.UnitOfWork;
using Order.Infrastructure.Persistence.Context;

namespace Order.Infrastructure.Persistence.UnitOfWork
{
    internal sealed class OrderUnitOfWorkFactory(IDbContextFactory<OrderDatabaseContext> dbContextFactory) : IUnitOfWorkFactory<IOrderUnitOfWork>
    {
        public IOrderUnitOfWork Create()
        {
            OrderDatabaseContext context = dbContextFactory.CreateDbContext();
            context.ChangeTracker.LazyLoadingEnabled = false;

            return new OrderUnitOfWork(context);
        }
    }
}