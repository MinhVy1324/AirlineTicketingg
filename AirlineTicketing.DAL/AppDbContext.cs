using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.DAL;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SanBay> SanBays => Set<SanBay>();
    public DbSet<HangHangKhong> HangHangKhongS => Set<HangHangKhong>();
    public DbSet<MayBay> MayBays => Set<MayBay>();
    public DbSet<TuyenBay> TuyenBays => Set<TuyenBay>();
    public DbSet<ChuyenBay> ChuyenBays => Set<ChuyenBay>();
    public DbSet<HangVe> HangVes => Set<HangVe>();
    public DbSet<KhachHang> KhachHangs => Set<KhachHang>();
    public DbSet<HanhKhach> HanhKhachs => Set<HanhKhach>();
    public DbSet<DatVe> DatVes => Set<DatVe>();
    public DbSet<Ve> Ves => Set<Ve>();
    public DbSet<ThanhToan> ThanhToans => Set<ThanhToan>();
    public DbSet<HoanTien> HoanTiens => Set<HoanTien>();
    public DbSet<LichSuDatVe> LichSuDatVes => Set<LichSuDatVe>();
    public DbSet<TaiKhoan> TaiKhoans => Set<TaiKhoan>();
    public DbSet<YeuCauDoiHoan> YeuCauDoiHoans => Set<YeuCauDoiHoan>();
    public DbSet<NhatKyHoatDong> NhatKyHoatDongs => Set<NhatKyHoatDong>();
    public DbSet<CauHinhHeThong> CauHinhHeThongs => Set<CauHinhHeThong>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
