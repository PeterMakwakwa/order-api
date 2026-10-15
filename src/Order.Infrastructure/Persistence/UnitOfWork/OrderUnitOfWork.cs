using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Order.Domain.Observability.Messages;
using Order.Domain.Repository;
using Order.Domain.UnitOfWork;
using Order.Domain.UnitOfWork.Exceptions;
using Order.Infrastructure.Persistence.Context;
using Order.Infrastructure.Persistence.Exceptions;
using Order.Infrastructure.Persistence.Repositories;

namespace Order.Infrastructure.Persistence.UnitOfWork
{
    public sealed class OrderUnitOfWork(OrderDatabaseContext orderDatabaseContext) : IOrderUnitOfWork
    {
        /// <summary>
        /// SQL Server: 2601 = duplicate key in a unique index, 2627 = unique/primary key constraint violation.
        /// </summary>
        private static readonly int[] UniqueViolationErrorNumbers = [2601, 2627];

        private readonly OrderDatabaseContext _orderDatabaseContext = orderDatabaseContext;

        public IOrderRepository OrderRepository { get; } = new OrderRepository(orderDatabaseContext.Orders);

        public IProductRepository ProductRepository { get; } = new ProductRepository(orderDatabaseContext.Products);

        public IPromotionRepository PromotionRepository { get; } = new PromotionRepository(orderDatabaseContext.Promotions);

        public IOrderPlacedOutboxRepository OrderPlacedOutboxRepository { get; } = new OrderPlacedOutboxRepository(orderDatabaseContext.OrderPlacedOutboxMessages);

        public void Dispose()
        {
            _orderDatabaseContext.ChangeTracker.Clear();
            _orderDatabaseContext.Dispose();
        }

        /// <summary>
        /// Translates the failures callers act on into domain exceptions, so the application layer never depends
        /// on Entity Framework or SQL Server: <see cref="ConcurrencyConflictException"/> and
        /// <see cref="UniqueConstraintViolationException"/>. Everything else becomes a <see cref="SaveContextException"/>.
        /// </summary>
        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _orderDatabaseContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConcurrencyConflictException(ErrorMessages.ConcurrencyConflictErrorMessage, ex);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlException && UniqueViolationErrorNumbers.Contains(sqlException.Number))
            {
                throw new UniqueConstraintViolationException(ErrorMessages.UniqueConstraintViolationErrorMessage, ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new SaveContextException(ErrorMessages.SaveContextErrorMessage, ex);
            }
        }
    }
}