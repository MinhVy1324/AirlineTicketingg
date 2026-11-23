using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class ChungTuDoiSoatConfiguration : IEntityTypeConfiguration<ChungTuDoiSoat>
{
    public void Configure(EntityTypeBuilder<ChungTuDoiSoat> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NhaCungCap).HasMaxLength(50).IsRequired();
        builder.Property(x => x.MaLenh).HasMaxLength(100).IsRequired();
        builder.Property(x => x.MaChungTu).HasMaxLength(64).IsRequired();
        builder.Property(x => x.KetQua).HasMaxLength(24).IsRequired();
        builder.Property(x => x.NguoiDoiSoat).HasMaxLength(150).IsRequired();
        builder.Property(x => x.SoTien).HasPrecision(12, 2);
        builder.HasIndex(x => new { x.NhaCungCap, x.MaChungTu }).IsUnique();
        builder.HasOne(x => x.ThanhToan).WithMany().HasForeignKey(x => x.ThanhToanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.HoanTien).WithMany().HasForeignKey(x => x.HoanTienId).OnDelete(DeleteBehavior.Restrict);
    }
}
