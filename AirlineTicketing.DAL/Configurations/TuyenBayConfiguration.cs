using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class TuyenBayConfiguration : IEntityTypeConfiguration<TuyenBay>
{
    public void Configure(EntityTypeBuilder<TuyenBay> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.SanBayDiId, x.SanBayDenId }).IsUnique();
        builder.HasIndex(x => x.SanBayDenId);
        builder.Property(x => x.DangKhaiThac).HasDefaultValue(true);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_TuyenBay_AirportsDiffer", "\"SanBayDiId\" <> \"SanBayDenId\"");
            t.HasCheckConstraint("CK_TuyenBay_Duration", "\"ThoiLuongDuKienPhut\" IS NULL OR \"ThoiLuongDuKienPhut\" > 0");
        });
        builder.HasOne(x => x.SanBayDi).WithMany(x => x.TuyenBayDi)
            .HasForeignKey(x => x.SanBayDiId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SanBayDen).WithMany(x => x.TuyenBayDen)
            .HasForeignKey(x => x.SanBayDenId).OnDelete(DeleteBehavior.Restrict);
    }
}
