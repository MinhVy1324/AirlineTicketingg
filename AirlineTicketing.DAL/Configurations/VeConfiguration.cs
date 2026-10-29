using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class VeConfiguration : IEntityTypeConfiguration<Ve>
{
    public void Configure(EntityTypeBuilder<Ve> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaVe).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.MaVe).IsUnique();
        builder.Property(x => x.SoGhe).HasMaxLength(5).IsRequired();
        builder.HasIndex(x => new { x.ChuyenBayId, x.SoGhe }).IsUnique()
            .HasFilter("\"TrangThai\" IN ('TamGiu', 'DaPhatHanh')");
        builder.HasIndex(x => new { x.ChuyenBayId, x.HanhKhachId }).IsUnique()
            .HasFilter("\"TrangThai\" IN ('TamGiu', 'DaPhatHanh')");
        builder.HasIndex(x => new { x.DatVeId, x.TrangThai });
        builder.Property(x => x.HangVe).HasMaxLength(20).IsRequired();
        builder.Property(x => x.GiaVe).HasPrecision(12, 2);
        builder.Property(x => x.TrangThai).HasConversion<string>().HasMaxLength(20);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Ve_GiaVe", "\"GiaVe\" >= 0");
            t.HasCheckConstraint("CK_Ve_TrangThai", "\"TrangThai\" IN ('TamGiu', 'DaPhatHanh', 'DaHuy', 'DaHoanTien')");
        });

        builder.HasOne(x => x.ChuyenBay).WithMany(x => x.Ves)
            .HasForeignKey(x => x.ChuyenBayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DatVe).WithMany(x => x.Ves)
            .HasForeignKey(x => x.DatVeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.HanhKhach).WithMany(x => x.Ves)
            .HasForeignKey(x => x.HanhKhachId).OnDelete(DeleteBehavior.Restrict);
    }
}
