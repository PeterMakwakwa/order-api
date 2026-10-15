using Microsoft.EntityFrameworkCore;
using Order.Domain.Enums;
using Order.Domain.Repository;
using Order.Domain.Repository.ReadModels;
using Order.Infrastructure.Persistence.Repositories.Base;
using Domain = Order.Domain.Entities;

namespace Order.Infrastructure.Persistence.Repositories
{
    public sealed class OrderRepository(DbSet<Domain::Order> dbSet)
        : RepositoryBase<Domain::Order>(dbSet), IOrderRepository
    {
        public Task<Domain::Order?> GetByIdempotencyKeyAsync(int customerId, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            return _dbSet
                .FirstOrDefaultAsync(x => x.CustomerId == customerId && x.IdempotencyKey == idempotencyKey, cancellationToken);
        }

        public async Task<IReadOnlyList<Guid>> GetStalledPaymentOrderIdsAsync(DateTimeOffset startedBefore, int batchSize, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(x => x.Status == OrderStatus.PendingPayment && x.PaymentStartedDate < startedBefore)
                .OrderBy(x => x.PaymentStartedDate)
                .Select(x => x.UniqueId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        public Task<Domain::Order?> GetByUniqueIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            return _dbSet.FirstOrDefaultAsync(x => x.UniqueId == orderId, cancellationToken);
        }

        public async Task<IReadOnlyList<OrderSummary>> GetSummariesByCustomerAsync(int customerId, (DateTimeOffset CreatedDate, long Position)? before, int take,
            CancellationToken cancellationToken = default)
        {
            IQueryable<Domain::Order> orders = _dbSet
                .AsNoTracking()
                .Where(x => x.CustomerId == customerId);

            if (before is (DateTimeOffset createdDate, long position))
            {
                // Seeks straight to the position on IX_Order_CustomerId_CreatedDate (which carries Id as the clustered key).
                orders = orders.Where(x => x.CreatedDate < createdDate || (x.CreatedDate == createdDate && x.Id < position));
            }

            // Id breaks ties between orders placed at the same moment, so the pages are stable.
            return await orders
                .OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .Select(x => new OrderSummary
                {
                    Id = x.UniqueId,
                    Position = x.Id,
                    Total = x.Total,
                    Status = x.Status,
                    PaymentMethod = x.PaymentMethod,
                    CreatedDate = x.CreatedDate
                })
                .ToListAsync(cancellationToken);
        }

        public Task<int> CountByCustomerAsync(int customerId, CancellationToken cancellationToken = default)
        {
            return _dbSet.CountAsync(x => x.CustomerId == customerId, cancellationToken);
        }

        public Task<OrderDetail?> GetDetailAsync(Guid orderId, int? customerId, CancellationToken cancellationToken = default)
        {
            return _dbSet
                .AsNoTracking()
                .Where(x => x.UniqueId == orderId && (customerId == null || x.CustomerId == customerId))
                .Select(x => new OrderDetail
                {
                    Id = x.UniqueId,
                    CustomerId = x.CustomerId,
                    Status = x.Status,
                    PaymentMethod = x.PaymentMethod,
                    PromotionCode = x.PromotionCode,
                    DiscountRate = x.DiscountRate,
                    Subtotal = x.Subtotal,
                    Total = x.Total,
                    CreatedDate = x.CreatedDate,
                    LineItems = x.LineItems
                        .OrderBy(lineItem => lineItem.Id)
                        .Select(lineItem => new OrderLineItemDetail
                        {
                            ProductId = lineItem.ProductId,
                            ProductName = lineItem.ProductName,
                            UnitPrice = lineItem.UnitPrice,
                            Quantity = lineItem.Quantity
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}