using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class MayBayConfiguration : IEntityTypeConfiguration<MayBay>
{
    public void Configure(EntityTypeBuilder<MayBay> builder)
    {
        builder.HasKey(mayBay => mayBay.Id);
        builder.Property(mayBay => mayBay.MaMayBay).HasMaxLength(20).IsRequired();
        builder.HasIndex(mayBay => mayBay.MaMayBay).IsUnique();
        builder.Property(mayBay => mayBay.TenMayBay).HasMaxLength(100).IsRequired();
        builder.Property(mayBay => mayBay.HangSanXuat).HasMaxLength(100).IsRequired();
        builder.Property(mayBay => mayBay.SoGhe).IsRequired();
    }
}
