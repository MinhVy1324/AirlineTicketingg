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
        builder.HasIndex(x => x.TenDangNhap).IsUnique();
        builder.Property(x => x.MatKhauHash).HasMaxLength(300).IsRequired();
        builder.Property(x => x.HoTen).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.SoDienThoai).HasMaxLength(20);
        builder.Property(x => x.VaiTro).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TrangThai).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.MaNhanSu).HasMaxLength(20);
        builder.Property(x => x.NgayTao).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.GhiChu).HasMaxLength(300);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_TaiKhoan_VaiTro", "\"VaiTro\" IN ('Admin', 'Manager', 'KhachHang')");
            t.HasCheckConstraint("CK_TaiKhoan_TrangThai", "\"TrangThai\" IN ('HoatDong', 'BiKhoa')");
        });

        builder.HasOne(x => x.KhachHang).WithMany(x => x.TaiKhoans)
            .HasForeignKey(x => x.KhachHangId).OnDelete(DeleteBehavior.SetNull);
    }
}
