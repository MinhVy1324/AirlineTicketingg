using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class NhatKyHoatDongConfiguration : IEntityTypeConfiguration<NhatKyHoatDong>
{
    public void Configure(EntityTypeBuilder<NhatKyHoatDong> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaNhatKy).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.MaNhatKy).IsUnique();
        builder.Property(x => x.TenNguoiDung).HasMaxLength(150).IsRequired();
        builder.Property(x => x.VaiTro).HasMaxLength(20).IsRequired();
        builder.Property(x => x.HanhDong).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DoiTuong).HasMaxLength(100).IsRequired();
        builder.Property(x => x.KetQua).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ChiTiet).HasMaxLength(1000);
        builder.Property(x => x.DiaChiIp).HasMaxLength(64);
        builder.Property(x => x.ThoiGian).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(x => x.ThoiGian);
        builder.HasIndex(x => new { x.VaiTro, x.ThoiGian });
        builder.ToTable(t =>
            t.HasCheckConstraint("CK_NhatKy_KetQua", "\"KetQua\" IN ('ThanhCong', 'ThatBai')"));

        builder.HasOne(x => x.TaiKhoan).WithMany(x => x.NhatKys)
            .HasForeignKey(x => x.TaiKhoanId).OnDelete(DeleteBehavior.SetNull);
    }
}
