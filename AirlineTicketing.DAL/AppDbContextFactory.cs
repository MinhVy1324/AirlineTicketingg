using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AirlineTicketing.DAL;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
<<<<<<< Updated upstream
            ?? "Host=127.0.0.1;Port=5432;Database=airline_ticketing;Username=airline_app;Password=secret";
=======
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection before running EF Core design-time commands.");
>>>>>>> Stashed changes
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
