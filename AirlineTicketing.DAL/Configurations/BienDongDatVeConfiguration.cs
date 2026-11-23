using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class BienDongDatVeConfiguration : IEntityTypeConfiguration<BienDongDatVe>
{
    public void Configure(EntityTypeBuilder<BienDongDatVe> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SoHieuCu).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SoHieuMoi).HasMaxLength(20);
        builder.Property(x => x.KetQua).HasMaxLength(300);
        builder.HasIndex(x => new { x.DatVeId, x.NgayXuLy });
        builder.HasIndex(x => new { x.NgayXuLy, x.NgayTao });
        builder.HasOne(x => x.DatVe).WithMany(x => x.BienDongs)
            .HasForeignKey(x => x.DatVeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ChuyenBay).WithMany()
            .HasForeignKey(x => x.ChuyenBayId).OnDelete(DeleteBehavior.Restrict);
    }
}
