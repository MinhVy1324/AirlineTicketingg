using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.DAL;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SanBay> SanBays => Set<SanBay>();
    public DbSet<MayBay> MayBays => Set<MayBay>();
    public DbSet<ChuyenBay> ChuyenBays => Set<ChuyenBay>();
    public DbSet<DatVe> DatVes => Set<DatVe>();
    public DbSet<Ve> Ves => Set<Ve>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
