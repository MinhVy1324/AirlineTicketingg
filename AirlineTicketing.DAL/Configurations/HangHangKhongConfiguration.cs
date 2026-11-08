using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class HangHangKhongConfiguration : IEntityTypeConfiguration<HangHangKhong>
{
    public void Configure(EntityTypeBuilder<HangHangKhong> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaHang).HasMaxLength(5).IsRequired();
        builder.HasIndex(x => x.MaHang).IsUnique();
        builder.Property(x => x.TenHang).HasMaxLength(150).IsRequired();
        builder.Property(x => x.QuocGia).HasMaxLength(100).IsRequired();
        builder.Property(x => x.MaIcao).HasMaxLength(5);
        builder.Property(x => x.DangHoatDong).HasDefaultValue(true);
    }
}
