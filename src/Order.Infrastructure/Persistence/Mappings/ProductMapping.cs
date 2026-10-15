using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Domain.Entities;

namespace Order.Infrastructure.Persistence.Mappings
{
    internal sealed class ProductMapping : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("tblProduct");

            ConfigureKeys(builder);
            ConfigureProperties(builder);
        }

        private static void ConfigureKeys(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(x => x.Id).HasName("PK_Product");
        }

        private static void ConfigureProperties(EntityTypeBuilder<Product> builder)
        {
            builder.Property(x => x.Id).HasColumnType(ColumnDataType.Int).ValueGeneratedNever();
            builder.Property(x => x.Name).HasColumnType(ColumnDataType.Nvarchar200);
            builder.Property(x => x.Price).HasColumnType(ColumnDataType.Money);

            builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UC_Product_Name");
        }
    }
}