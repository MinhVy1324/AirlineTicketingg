using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Web.Infrastructure.Email;
using AirlineTicketing.Web.Infrastructure.Tickets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AirlineTicketing.Tests;

[Collection(PostgresCollection.Name)]
public sealed class EmailDeliveryTests
{
    [PostgresFact]
    public async Task ConcurrentWorkers_ClaimOnce_AndADifferentWorkerRetriesPersistedJob()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        await using var firstDb = Database();
        await using var secondDb = Database();
        var clock = new AdjustableClock();
        var protection = new EphemeralDataProtectionProvider();
        var email = new EmailMessage { To = host.Data.CustomerAEmail, Subject = "Test", HtmlBody = "<p>Test retry</p>" };
        await new EmailDeliveryQueue(db, protection, clock).QueueAccountAsync(email, clock.GetUtcNow().AddMinutes(29), default);
        var id = db.ChangeTracker.Entries<AirlineTicketing.Domain.EmailDeliveryJob>().Single().Entity.Id;
        var sender = new RecoveringSender();

        var claims = await Task.WhenAll(Processor(firstDb, sender, clock, protection).ProcessOneAsync(default),
            Processor(secondDb, sender, clock, protection).ProcessOneAsync(default));
        Assert.Equal(1, claims.Count(claimed => claimed));
        var failed = await db.EmailDeliveryJobs.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(1, failed.Attempts);
        Assert.Null(failed.CompletedAt);
        Assert.Equal(clock.GetUtcNow().AddMinutes(1), failed.NextAttemptAt);
        Assert.DoesNotContain(email.To, failed.ProtectedMessage!);

        clock.Advance(TimeSpan.FromMinutes(1));
        await using var restartedDb = Database();
        Assert.True(await Processor(restartedDb, sender, clock, protection).ProcessOneAsync(default));
        var sent = await db.EmailDeliveryJobs.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.True(sent.Delivered);
        Assert.NotNull(sent.CompletedAt);
        Assert.Null(sent.ProtectedMessage);
        Assert.Equal(2, sent.Attempts);
        Assert.Equal(new[] { id, id }, sender.Deliveries);
    }

    [PostgresFact]
    public async Task ExpiredLinkIsDiscardedWithoutSending()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var clock = new AdjustableClock();
        var protection = new EphemeralDataProtectionProvider();
        await new EmailDeliveryQueue(db, protection, clock).QueueAccountAsync(
            new EmailMessage { To = host.Data.CustomerAEmail, Subject = "Test", HtmlBody = "Expired" },
            clock.GetUtcNow().AddMinutes(1), default);
        clock.Advance(TimeSpan.FromMinutes(2));
        var sender = new RecoveringSender();
        await using var worker = Database();
        Assert.True(await Processor(worker, sender, clock, protection).ProcessOneAsync(default));
        var id = db.ChangeTracker.Entries<AirlineTicketing.Domain.EmailDeliveryJob>().Single().Entity.Id;
        var expired = await db.EmailDeliveryJobs.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.False(expired.Delivered);
        Assert.NotNull(expired.CompletedAt);
        Assert.Null(expired.ProtectedMessage);
        Assert.Empty(sender.Deliveries);
    }

    private static EmailDeliveryProcessor Processor(AppDbContext db, IEmailSender sender, TimeProvider clock,
        IDataProtectionProvider protection) => new(db, new EmailDeliveryQueue(db, protection, clock),
            new GuiVeDienTuService(db, new CauHinhHeThongBUS(db)), sender, clock, NullLogger<EmailDeliveryProcessor>.Instance);
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(PostgresTestConnection.Require()).Options);

    private sealed class AdjustableClock : TimeProvider
    {
        private DateTimeOffset now = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan time) => now += time;
    }

    private sealed class RecoveringSender : IEmailSender
    {
        public string TenKenh => "Test";
        public List<Guid> Deliveries { get; } = [];
        public Task GuiAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Deliveries.Add(message.DeliveryId!.Value);
            return Deliveries.Count == 1 ? Task.FromException(new IOException("Temporary email failure")) : Task.CompletedTask;
        }
    }
}
