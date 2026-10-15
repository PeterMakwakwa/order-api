using Microsoft.EntityFrameworkCore;
using Order.Infrastructure.Persistence.Mappings;
using Domain = Order.Domain.Entities;

namespace Order.Infrastructure.Persistence.Context
{
    public sealed class OrderDatabaseContext(DbContextOptions<OrderDatabaseContext> options) : DbContext(options)
    {
        public required DbSet<Domain::Order> Orders { get; set; }

        public required DbSet<Domain::Product> Products { get; set; }

        public required DbSet<Domain::Promotion> Promotions { get; set; }

        public required DbSet<Domain::OrderPlacedOutboxMessage> OrderPlacedOutboxMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new OrderMapping());
            modelBuilder.ApplyConfiguration(new ProductMapping());
            modelBuilder.ApplyConfiguration(new PromotionMapping());
            modelBuilder.ApplyConfiguration(new OrderPlacedOutboxMessageMapping());
        }
    }
}