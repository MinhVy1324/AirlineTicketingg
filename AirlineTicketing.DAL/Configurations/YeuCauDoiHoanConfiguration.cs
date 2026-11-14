using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class YeuCauDoiHoanConfiguration : IEntityTypeConfiguration<YeuCauDoiHoan>
{
    public void Configure(EntityTypeBuilder<YeuCauDoiHoan> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaYeuCau).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.MaYeuCau).IsUnique();
        builder.Property(x => x.LyDo).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ThongTinBoSung).HasMaxLength(1000);
        builder.Property(x => x.LoaiYeuCau).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TrangThai).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.PhiDoi).HasPrecision(12, 2);
        builder.Property(x => x.PhiHoan).HasPrecision(12, 2);
        builder.Property(x => x.PhiDichVuKhongHoan).HasPrecision(12, 2);
        builder.Property(x => x.PhiHanhLyKhongHoan).HasPrecision(12, 2);
        builder.Property(x => x.SoTienHoan).HasPrecision(12, 2);
        builder.Property(x => x.PhuongThucHoan).HasMaxLength(100);
        builder.Property(x => x.NgayTao).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.GhiChuXuLy).HasMaxLength(500);
        builder.HasIndex(x => new { x.TrangThai, x.NgayTao });
        builder.HasIndex(x => new { x.DatVeId, x.LoaiYeuCau });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_YeuCau_Loai", "\"LoaiYeuCau\" IN ('DoiVe', 'HoanVe')");
            t.HasCheckConstraint("CK_YeuCau_TrangThai", "\"TrangThai\" IN ('ChoDuyet', 'DaDuyet', 'TuChoi', 'HoanTat')");
            t.HasCheckConstraint("CK_YeuCau_Phi", "\"PhiDoi\" >= 0 AND \"PhiHoan\" >= 0 AND \"PhiDichVuKhongHoan\" >= 0 AND \"PhiHanhLyKhongHoan\" >= 0 AND \"SoTienHoan\" >= 0");
        });

        builder.HasOne(x => x.DatVe).WithMany(x => x.YeuCaus)
            .HasForeignKey(x => x.DatVeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ChuyenBayMoi).WithMany(x => x.YeuCausDoiSang)
            .HasForeignKey(x => x.ChuyenBayMoiId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.NguoiXuLy).WithMany(x => x.YeuCausXuLy)
            .HasForeignKey(x => x.NguoiXuLyId).OnDelete(DeleteBehavior.SetNull);
    }
}
