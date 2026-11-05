using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class LichSuDatVeConfiguration : IEntityTypeConfiguration<LichSuDatVe>
{
    public void Configure(EntityTypeBuilder<LichSuDatVe> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.HanhDong).HasMaxLength(40).IsRequired();
        builder.Property(x => x.TrangThaiTruoc).HasMaxLength(20);
        builder.Property(x => x.TrangThaiSau).HasMaxLength(20).IsRequired();
        builder.Property(x => x.LyDo).HasMaxLength(300);
        builder.Property(x => x.NguoiThucHien).HasMaxLength(150);
        builder.Property(x => x.DuLieuBoSung).HasColumnType("jsonb");
        builder.Property(x => x.ThoiDiem).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(x => new { x.DatVeId, x.ThoiDiem });

        builder.HasOne(x => x.DatVe).WithMany(x => x.LichSus)
            .HasForeignKey(x => x.DatVeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Ve).WithMany()
            .HasForeignKey(x => x.VeId).OnDelete(DeleteBehavior.Restrict);
    }
}
