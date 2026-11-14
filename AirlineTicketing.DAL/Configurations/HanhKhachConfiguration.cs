using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class HanhKhachConfiguration : IEntityTypeConfiguration<HanhKhach>
{
    public void Configure(EntityTypeBuilder<HanhKhach> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.HoTen).HasMaxLength(150).IsRequired();
        builder.Property(x => x.QuocTich).HasMaxLength(100);
        builder.Property(x => x.SoHoChieu).HasMaxLength(30);
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.Property(x => x.SoDienThoai).HasMaxLength(20);
        builder.Property(x => x.GioiTinh).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.LoaiHanhKhach).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.SoHoChieu).IsUnique().HasFilter("\"SoHoChieu\" IS NOT NULL");
    }
}
