using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class SanBayConfiguration : IEntityTypeConfiguration<SanBay>
{
    public void Configure(EntityTypeBuilder<SanBay> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaSanBay).HasMaxLength(3).IsRequired();
        builder.HasIndex(x => x.MaSanBay).IsUnique();
        builder.Property(x => x.TenSanBay).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ThanhPho).HasMaxLength(100).IsRequired();
        builder.Property(x => x.QuocGia).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DangHoatDong).HasDefaultValue(true);
    }
}
