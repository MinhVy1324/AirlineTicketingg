using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.DAL;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SanBay> SanBays => Set<SanBay>();
    public DbSet<MayBay> MayBays => Set<MayBay>();
    public DbSet<TuyenBay> TuyenBays => Set<TuyenBay>();
    public DbSet<ChuyenBay> ChuyenBays => Set<ChuyenBay>();
    public DbSet<KhachHang> KhachHangs => Set<KhachHang>();
    public DbSet<HanhKhach> HanhKhachs => Set<HanhKhach>();
    public DbSet<DatVe> DatVes => Set<DatVe>();
    public DbSet<Ve> Ves => Set<Ve>();
    public DbSet<ThanhToan> ThanhToans => Set<ThanhToan>();
    public DbSet<HoanTien> HoanTiens => Set<HoanTien>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
