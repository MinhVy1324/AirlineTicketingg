using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class ChuyenBayConfiguration : IEntityTypeConfiguration<ChuyenBay>
{
    public void Configure(EntityTypeBuilder<ChuyenBay> builder)
    {
        builder.HasKey(chuyenBay => chuyenBay.Id);
        builder.Property(chuyenBay => chuyenBay.SoHieu).HasMaxLength(20).IsRequired();
        builder.HasIndex(chuyenBay => new { chuyenBay.SoHieu, chuyenBay.GioKhoiHanh }).IsUnique();
        builder.Property(chuyenBay => chuyenBay.GiaCoBan).HasPrecision(18, 2);

        builder.HasOne(chuyenBay => chuyenBay.SanBayDi)
            .WithMany(sanBay => sanBay.ChuyenBayDi)
            .HasForeignKey(chuyenBay => chuyenBay.SanBayDiId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(chuyenBay => chuyenBay.SanBayDen)
            .WithMany(sanBay => sanBay.ChuyenBayDen)
            .HasForeignKey(chuyenBay => chuyenBay.SanBayDenId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(chuyenBay => chuyenBay.MayBay)
            .WithMany(mayBay => mayBay.ChuyenBays)
            .HasForeignKey(chuyenBay => chuyenBay.MayBayId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
