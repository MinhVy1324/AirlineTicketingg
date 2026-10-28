using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class VeConfiguration : IEntityTypeConfiguration<Ve>
{
    public void Configure(EntityTypeBuilder<Ve> builder)
    {
        builder.HasKey(ve => ve.Id);
        builder.Property(ve => ve.MaVe).HasMaxLength(30).IsRequired();
        builder.HasIndex(ve => ve.MaVe).IsUnique();
        builder.Property(ve => ve.SoGhe).HasMaxLength(10).IsRequired();
        builder.HasIndex(ve => new { ve.ChuyenBayId, ve.SoGhe }).IsUnique();
        builder.Property(ve => ve.HangVe).HasMaxLength(30).IsRequired();
        builder.Property(ve => ve.GiaVe).HasPrecision(18, 2);
        builder.Property(ve => ve.TrangThai).HasMaxLength(30).IsRequired();

        builder.HasOne(ve => ve.ChuyenBay)
            .WithMany(chuyenBay => chuyenBay.Ves)
            .HasForeignKey(ve => ve.ChuyenBayId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(ve => ve.DatVe)
            .WithMany(datVe => datVe.Ves)
            .HasForeignKey(ve => ve.DatVeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
