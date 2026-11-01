using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class TaiKhoanConfiguration : IEntityTypeConfiguration<TaiKhoan>
{
    public void Configure(EntityTypeBuilder<TaiKhoan> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenDangNhap).HasMaxLength(50).IsRequired();
        builder.Property(x => x.MatKhauHash).HasMaxLength(256).IsRequired();
        builder.Property(x => x.HoTen).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Property(x => x.SoDienThoai).HasMaxLength(20);
        builder.Property(x => x.VaiTro).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.TrangThai).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.NgayTao).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.LanDangNhapCuoi);

        builder.HasIndex(x => x.TenDangNhap).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.VaiTro);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_TaiKhoan_VaiTro", "\"VaiTro\" IN ('Admin', 'NhanVien', 'KhachHang')");
            t.HasCheckConstraint("CK_TaiKhoan_TrangThai", "\"TrangThai\" IN ('HoatDong', 'BiKhoa')");
        });

        builder.HasOne(x => x.KhachHang)
            .WithOne(x => x.TaiKhoan)
            .HasForeignKey<TaiKhoan>(x => x.KhachHangId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
