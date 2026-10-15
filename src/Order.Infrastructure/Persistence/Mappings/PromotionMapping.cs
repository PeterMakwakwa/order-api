using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Domain.Entities;

namespace Order.Infrastructure.Persistence.Mappings
{
    internal sealed class PromotionMapping : IEntityTypeConfiguration<Promotion>
    {
        public void Configure(EntityTypeBuilder<Promotion> builder)
        {
            builder.ToTable("tblPromotion");

            ConfigureKeys(builder);
            ConfigureProperties(builder);
        }

        private static void ConfigureKeys(EntityTypeBuilder<Promotion> builder)
        {
            builder.HasKey(x => x.Id).HasName("PK_Promotion");
        }

        private static void ConfigureProperties(EntityTypeBuilder<Promotion> builder)
        {
            builder.Property(x => x.Id).HasColumnType(ColumnDataType.Int).ValueGeneratedOnAdd();
            builder.Property(x => x.Code).HasColumnType(ColumnDataType.Varchar50);
            builder.Property(x => x.DiscountRate).HasColumnType(ColumnDataType.Rate);
            builder.Property(x => x.IsActive).HasColumnType(ColumnDataType.Bit);
            builder.Property(x => x.ValidFrom).HasColumnType(ColumnDataType.DateTimeOffset);
            builder.Property(x => x.ValidTo).HasColumnType(ColumnDataType.DateTimeOffset);

            builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UC_Promotion_Code");
        }
    }
}