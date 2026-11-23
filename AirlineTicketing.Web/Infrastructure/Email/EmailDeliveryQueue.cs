using System.Text.Json;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure.Tickets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Infrastructure.Email;

public sealed class EmailDeliveryQueue(AppDbContext db, IDataProtectionProvider protection, TimeProvider clock)
{
    private readonly IDataProtector protector = protection.CreateProtector("AeroViet.EmailDelivery.v1");

    public static void StageTicket(AppDbContext db, int bookingId, string actor, DateTimeOffset now)
    {
        db.EmailDeliveryJobs.Add(new EmailDeliveryJob
        {
            BookingId = bookingId, Actor = actor.Length <= 150 ? actor : actor[..150],
            CreatedAt = now, NextAttemptAt = now, ExpiresAt = now.AddDays(1)
        });
    }

    internal static async Task StageUpdatedTicketAsync(AppDbContext db, int bookingId, string actor,
        DateTimeOffset now, CancellationToken ct)
    {
        await CancelPendingTicketAsync(db, bookingId, now, "Đã thay bằng email vé cập nhật.", ct);
        StageTicket(db, bookingId, actor, now);
    }

    internal static Task<int> CancelPendingTicketAsync(AppDbContext db, int bookingId, DateTimeOffset now,
        string reason, CancellationToken ct) =>
        db.EmailDeliveryJobs.Where(job => job.BookingId == bookingId && job.CompletedAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(job => job.CompletedAt, now)
                .SetProperty(job => job.LeaseId, (Guid?)null)
                .SetProperty(job => job.LastError, reason), ct);

    public async Task QueueAccountAsync(EmailMessage message, DateTimeOffset expiresAt, CancellationToken ct)
    {
        StageMessage(message, clock.GetUtcNow(), expiresAt);
        await db.SaveChangesAsync(ct);
    }

    internal void StageMessage(EmailMessage message, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        db.EmailDeliveryJobs.Add(new EmailDeliveryJob
        {
            ProtectedMessage = protector.Protect(JsonSerializer.Serialize(message)),
            CreatedAt = now, NextAttemptAt = now, ExpiresAt = expiresAt
        });
    }

    public async Task<bool> QueueTicketResendAsync(int bookingId, string actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var booking = await db.DatVes.AsNoTracking().Include(x => x.KhachHang)
            .Include(x => x.BienDongs.Where(change => change.NgayXuLy == null))
            .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay)
            .SingleOrDefaultAsync(x => x.Id == bookingId, ct);
        if (booking is null || booking.TrangThai != TrangThaiDatVe.DaXacNhan
            || FlightDisruptionService.NeedsAttention(booking) || !GuiVeDienTuService.HasDeliverableAddress(booking))
            return false;
        if (!await db.EmailDeliveryJobs.AnyAsync(x => x.BookingId == bookingId && x.CompletedAt == null, ct))
        {
            StageTicket(db, bookingId, actor, clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return true;
    }

    internal EmailMessage ReadMessage(string ciphertext) =>
        JsonSerializer.Deserialize<EmailMessage>(protector.Unprotect(ciphertext))
        ?? throw new InvalidOperationException("Invalid email queue payload.");
}
