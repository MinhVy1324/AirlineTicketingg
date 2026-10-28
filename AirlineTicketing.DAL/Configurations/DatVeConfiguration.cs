using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class DatVeConfiguration : IEntityTypeConfiguration<DatVe>
{
    public void Configure(EntityTypeBuilder<DatVe> builder)
    {
        builder.HasKey(datVe => datVe.Id);
        builder.Property(datVe => datVe.MaDatVe).HasMaxLength(30).IsRequired();
        builder.HasIndex(datVe => datVe.MaDatVe).IsUnique();
        builder.Property(datVe => datVe.HoTenKhachHang).HasMaxLength(150).IsRequired();
        builder.Property(datVe => datVe.Email).HasMaxLength(254).IsRequired();
        builder.Property(datVe => datVe.SoDienThoai).HasMaxLength(20).IsRequired();
        builder.Property(datVe => datVe.TrangThai).HasMaxLength(30).IsRequired();
    }
}
