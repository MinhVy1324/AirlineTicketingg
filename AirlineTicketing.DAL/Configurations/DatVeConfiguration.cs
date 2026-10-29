using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class DatVeConfiguration : IEntityTypeConfiguration<DatVe>
{
    public void Configure(EntityTypeBuilder<DatVe> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaDatVe).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.MaDatVe).IsUnique();
        builder.HasIndex(x => new { x.KhachHangId, x.NgayDat });
        builder.Property(x => x.NgayDat).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.TrangThai).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TongTien).HasPrecision(12, 2);
        builder.Property(x => x.DonViTienTe).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_DatVe_TongTien", "\"TongTien\" >= 0");
            t.HasCheckConstraint("CK_DatVe_DonViTienTe", "\"DonViTienTe\" ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint("CK_DatVe_TrangThai", "\"TrangThai\" IN ('ChoThanhToan', 'DaXacNhan', 'DaHuy', 'HetHan', 'HoanTien')");
        });

        builder.HasOne(x => x.KhachHang).WithMany(x => x.DatVes)
            .HasForeignKey(x => x.KhachHangId).OnDelete(DeleteBehavior.Restrict);
    }
}
