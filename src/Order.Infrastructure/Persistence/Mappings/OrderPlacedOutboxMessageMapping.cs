using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Domain.Entities;

namespace Order.Infrastructure.Persistence.Mappings
{
    internal sealed class OrderPlacedOutboxMessageMapping : IEntityTypeConfiguration<OrderPlacedOutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OrderPlacedOutboxMessage> builder)
        {
            builder.ToTable("queueOrderPlaced");

            ConfigureKeys(builder);
            ConfigureProperties(builder);
        }

        private static void ConfigureKeys(EntityTypeBuilder<OrderPlacedOutboxMessage> builder)
        {
            builder.HasKey(x => x.Id).HasName("PK_OrderPlaced");
        }

        private static void ConfigureProperties(EntityTypeBuilder<OrderPlacedOutboxMessage> builder)
        {
            builder.Property(x => x.Id).HasColumnType(ColumnDataType.BigInt).ValueGeneratedOnAdd();
            builder.Property(x => x.Version).HasColumnType(ColumnDataType.Int);
            builder.Property(x => x.Message).HasColumnType(ColumnDataType.NvarcharMax);
            builder.Property(x => x.CreatedDate).HasColumnType(ColumnDataType.DateTimeOffset);
            builder.Property(x => x.ProcessedDate).HasColumnType(ColumnDataType.DateTimeOffset);
            builder.Property(x => x.Attempts).HasColumnType(ColumnDataType.Int);
            builder.Property(x => x.LastError).HasColumnType(ColumnDataType.Nvarchar2000);
            builder.Property(x => x.LockedUntil).HasColumnType(ColumnDataType.DateTimeOffset);
            builder.Property(x => x.LockToken).HasColumnType(ColumnDataType.UniqueIdentifier).IsConcurrencyToken();

            builder.HasIndex(x => new { x.ProcessedDate, x.CreatedDate }).HasDatabaseName("IX_OrderPlaced_ProcessedDate_CreatedDate");
        }
    }
}