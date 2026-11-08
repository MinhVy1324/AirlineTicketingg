using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class HangVeConfiguration : IEntityTypeConfiguration<HangVe>
{
    public void Configure(EntityTypeBuilder<HangVe> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaHang).HasMaxLength(10).IsRequired();
        builder.Property(x => x.TenHang).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NhomHang).HasMaxLength(50).IsRequired();
        builder.Property(x => x.GiaCoBan).HasPrecision(12, 2);
        builder.Property(x => x.PhiDoi).HasPrecision(12, 2);
        builder.Property(x => x.PhiHoan).HasPrecision(12, 2);
        builder.HasIndex(x => new { x.ChuyenBayId, x.MaHang }).IsUnique();
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_HangVe_Gia", "\"GiaCoBan\" >= 0");
            t.HasCheckConstraint("CK_HangVe_Cho", "\"SoGhe\" > 0 AND \"SoGheDaBan\" >= 0 AND \"SoGheDaBan\" <= \"SoGhe\"");
            t.HasCheckConstraint("CK_HangVe_HanhLy", "\"HanhLyXachTayKg\" >= 0 AND \"HanhLyKyGuiKg\" >= 0");
            t.HasCheckConstraint("CK_HangVe_Phi", "\"PhiDoi\" >= 0 AND \"PhiHoan\" >= 0");
        });

        builder.HasOne(x => x.ChuyenBay).WithMany(x => x.HangVes)
            .HasForeignKey(x => x.ChuyenBayId).OnDelete(DeleteBehavior.Cascade);
    }
}
