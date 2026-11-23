using AirlineTicketing.DAL;

namespace AirlineTicketing.Web.Infrastructure;

public sealed class DemoFlightScheduleWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<DemoFlightScheduleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = TimeSpan.FromHours(12);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var added = await DbSeeder.RefreshFlightScheduleAsync(db, clock.GetUtcNow(), stoppingToken);
                if (added > 0)
                    logger.LogInformation("Đã bổ sung {Flights} chuyến bay demo.", added);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(error, "Không bổ sung được lịch bay demo.");
                nextRun = TimeSpan.FromMinutes(5);
            }
            await Task.Delay(nextRun, clock, stoppingToken);
        }
    }
}
