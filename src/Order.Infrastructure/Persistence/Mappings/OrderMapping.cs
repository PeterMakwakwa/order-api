using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Domain.Entities;
using Domain = Order.Domain.Entities;

namespace Order.Infrastructure.Persistence.Mappings
{
    internal sealed class OrderMapping : IEntityTypeConfiguration<Domain::Order>
    {
        public void Configure(EntityTypeBuilder<Domain::Order> builder)
        {
            builder.ToTable("tblOrder");

            ConfigureKeys(builder);
            ConfigureProperties(builder);
            ConfigureOwnership(builder);
        }

        private static void ConfigureKeys(EntityTypeBuilder<Domain::Order> builder)
        {
            builder.HasKey(x => x.Id).HasName("PK_Order");
        }

        private static void ConfigureProperties(EntityTypeBuilder<Domain::Order> builder)
        {
            builder.Property(x => x.Id).HasColumnType(ColumnDataType.BigInt).ValueGeneratedOnAdd();
            builder.Property(x => x.UniqueId).HasColumnType(ColumnDataType.UniqueIdentifier);
            builder.Property(x => x.CorrelationId).HasColumnType(ColumnDataType.Varchar100);
            builder.Property(x => x.IdempotencyKey).HasColumnType(ColumnDataType.Varchar100);
            builder.Property(x => x.RequestHash).HasColumnType(ColumnDataType.Char64);
            builder.Property(x => x.CustomerId).HasColumnType(ColumnDataType.Int);
            builder.Property(x => x.CustomerEmail).HasColumnType(ColumnDataType.Nvarchar320);
            builder.Property(x => x.PaymentMethod).HasColumnType(ColumnDataType.Varchar32).HasConversion<string>();
            builder.Property(x => x.Status).HasColumnType(ColumnDataType.Varchar32).HasConversion<string>();
            builder.Property(x => x.PaymentOutcome).HasColumnType(ColumnDataType.Varchar32).HasConversion<string>();
            builder.Property(x => x.PromotionCode).HasColumnType(ColumnDataType.Varchar50);
            builder.Property(x => x.DiscountRate).HasColumnType(ColumnDataType.Rate);
            builder.Property(x => x.Subtotal).HasColumnType(ColumnDataType.Money);
            builder.Property(x => x.Total).HasColumnType(ColumnDataType.Money);
            builder.Property(x => x.PaymentFailureReason).HasColumnType(ColumnDataType.Nvarchar500);
            builder.Property(x => x.CreatedDate).HasColumnType(ColumnDataType.DateTimeOffset);
            builder.Property(x => x.PaymentStartedDate).HasColumnType(ColumnDataType.DateTimeOffset);
            builder.Property(x => x.ConcurrencyToken).HasColumnType(ColumnDataType.UniqueIdentifier).IsConcurrencyToken();

            builder.HasIndex(x => x.UniqueId).IsUnique().HasDatabaseName("UC_Order_UniqueId");
            builder.HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique().HasDatabaseName("UC_Order_CustomerId_IdempotencyKey");
            builder.HasIndex(x => new { x.CustomerId, x.CreatedDate }).HasDatabaseName("IX_Order_CustomerId_CreatedDate");
            builder.HasIndex(x => new { x.Status, x.PaymentStartedDate }).HasDatabaseName("IX_Order_Status_PaymentStartedDate");
            builder.Ignore(x => x.IsSettled);
            builder.Ignore(x => x.CanRetryPayment);
        }

        private static void ConfigureOwnership(EntityTypeBuilder<Domain::Order> builder)
        {
            builder.OwnsMany(x => x.LineItems, ConfigureLineItemOwnership);
            builder.Navigation(x => x.LineItems).HasField("_lineItems").UsePropertyAccessMode(PropertyAccessMode.Field);
        }

        private static void ConfigureLineItemOwnership(OwnedNavigationBuilder<Domain::Order, OrderLineItem> builder)
        {
            builder.ToTable("tblOrderLineItem");

            builder.HasKey(x => x.Id).HasName("PK_OrderLineItem");
            builder.WithOwner().HasForeignKey(x => x.OrderId).HasConstraintName("FK_OrderLineItem_Order");

            builder.Property(x => x.Id).HasColumnType(ColumnDataType.BigInt).ValueGeneratedOnAdd();
            builder.Property(x => x.OrderId).HasColumnType(ColumnDataType.BigInt);
            builder.Property(x => x.ProductId).HasColumnType(ColumnDataType.Int);
            builder.Property(x => x.ProductName).HasColumnType(ColumnDataType.Nvarchar200);
            builder.Property(x => x.UnitPrice).HasColumnType(ColumnDataType.Money);
            builder.Property(x => x.Quantity).HasColumnType(ColumnDataType.Int);

            builder.Ignore(x => x.LineTotal);
        }
    }
}