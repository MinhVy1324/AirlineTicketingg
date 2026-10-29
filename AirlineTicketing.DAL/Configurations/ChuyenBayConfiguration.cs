using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class ChuyenBayConfiguration : IEntityTypeConfiguration<ChuyenBay>
{
    public void Configure(EntityTypeBuilder<ChuyenBay> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SoHieu).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.SoHieu, x.GioKhoiHanh }).IsUnique();
        builder.HasIndex(x => new { x.TuyenBayId, x.GioKhoiHanh });
        builder.HasIndex(x => new { x.MayBayId, x.GioKhoiHanh });
        builder.Property(x => x.GiaCoBan).HasPrecision(12, 2);
        builder.Property(x => x.TrangThai).HasConversion<string>().HasMaxLength(20);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ChuyenBay_TimeRange", "\"GioHaCanh\" > \"GioKhoiHanh\"");
            t.HasCheckConstraint("CK_ChuyenBay_GiaCoBan", "\"GiaCoBan\" >= 0");
            t.HasCheckConstraint("CK_ChuyenBay_TrangThai", "\"TrangThai\" IN ('DuKien', 'DangBay', 'HoanTat', 'BiHuy')");
        });
        builder.HasOne(x => x.TuyenBay).WithMany(x => x.ChuyenBays)
            .HasForeignKey(x => x.TuyenBayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MayBay).WithMany(x => x.ChuyenBays)
            .HasForeignKey(x => x.MayBayId).OnDelete(DeleteBehavior.Restrict);
    }
}
