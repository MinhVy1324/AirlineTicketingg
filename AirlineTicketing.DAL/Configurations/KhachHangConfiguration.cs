using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class KhachHangConfiguration : IEntityTypeConfiguration<KhachHang>
{
    public void Configure(EntityTypeBuilder<KhachHang> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.HoTen).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Property(x => x.SoDienThoai).HasMaxLength(20).IsRequired();
        builder.Property(x => x.NgayTao).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.SoDienThoai);
    }
}
