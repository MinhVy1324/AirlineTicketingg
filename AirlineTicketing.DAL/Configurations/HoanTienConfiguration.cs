using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class HoanTienConfiguration : IEntityTypeConfiguration<HoanTien>
{
    public void Configure(EntityTypeBuilder<HoanTien> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SoTien).HasPrecision(12, 2);
        builder.Property(x => x.DonViTienTe).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(x => x.NhaCungCap).HasMaxLength(50).HasDefaultValue("NoiBo").IsRequired();
        builder.Property(x => x.MaGiaoDich).HasMaxLength(100);
        builder.Property(x => x.LyDo).HasMaxLength(300);
        builder.Property(x => x.TrangThai).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.NgayTao).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(x => new { x.ThanhToanId, x.NgayTao });
        builder.HasIndex(x => new { x.NhaCungCap, x.MaGiaoDich }).IsUnique()
            .HasFilter("\"MaGiaoDich\" IS NOT NULL");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_HoanTien_SoTien", "\"SoTien\" > 0");
            t.HasCheckConstraint("CK_HoanTien_DonViTienTe", "\"DonViTienTe\" ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint("CK_HoanTien_TrangThai", "\"TrangThai\" IN ('ChoXuLy', 'ThanhCong', 'ThatBai')");
        });
        builder.HasOne(x => x.ThanhToan).WithMany(x => x.HoanTiens)
            .HasForeignKey(x => x.ThanhToanId).OnDelete(DeleteBehavior.Restrict);
    }
}
