using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class MayBayConfiguration : IEntityTypeConfiguration<MayBay>
{
    public void Configure(EntityTypeBuilder<MayBay> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaMayBay).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.MaMayBay).IsUnique();
        builder.Property(x => x.TenMayBay).HasMaxLength(100).IsRequired();
        builder.Property(x => x.HangSanXuat).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DangHoatDong).HasDefaultValue(true);
        builder.ToTable(t => t.HasCheckConstraint("CK_MayBay_SoGhe", "\"SoGhe\" > 0"));
    }
}
