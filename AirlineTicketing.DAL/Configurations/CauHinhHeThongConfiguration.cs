using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class CauHinhHeThongConfiguration : IEntityTypeConfiguration<CauHinhHeThong>
{
    public void Configure(EntityTypeBuilder<CauHinhHeThong> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Khoa).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Khoa).IsUnique();
        builder.Property(x => x.GiaTri).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Nhom).HasMaxLength(50).IsRequired();
        builder.Property(x => x.MoTa).HasMaxLength(300);
        builder.Property(x => x.KieuDuLieu).HasMaxLength(20);
        builder.Property(x => x.NgayCapNhat).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.NguoiCapNhat).HasMaxLength(150);
        builder.HasIndex(x => x.Nhom);
    }
}
