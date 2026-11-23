using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure.Tickets;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Infrastructure.Email;

public sealed class EmailDeliveryWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var poll = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Email:PollSeconds", 5), 1, 60));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (await scope.ServiceProvider.GetRequiredService<EmailDeliveryProcessor>().ProcessOneAsync(ct))
                    continue;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Không xử lý được hàng đợi email."); }
            await Task.Delay(poll, ct);
        }
    }
}

public sealed class EmailDeliveryProcessor(AppDbContext db, EmailDeliveryQueue queue, GuiVeDienTuService tickets,
    IEmailSender sender, TimeProvider clock, ILogger<EmailDeliveryProcessor> logger)
{
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    public async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        var job = await ClaimAsync(ct);
        if (job is null) return false;
        if (job.CompletedAt.HasValue) return true;

        EmailMessage? message;
        try
        {
            message = job.BookingId is int bookingId
                ? await tickets.PrepareAsync(bookingId, ct)
                : queue.ReadMessage(job.ProtectedMessage!);
            if (message is null || clock.GetUtcNow() >= job.ExpiresAt)
            {
                Finish(job, false, "Địa chỉ, đặt chỗ hoặc liên kết không còn hợp lệ.");
                await db.SaveChangesAsync(ct);
                return true;
            }

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var remaining = job.ExpiresAt - clock.GetUtcNow();
            deadline.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(remaining.TotalSeconds, 0, 20)));
            await sender.GuiAsync(message with { DeliveryId = job.Id }, deadline.Token);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var category = ex.GetType().Name;
            logger.LogWarning("Email {JobId} gửi lỗi ở lần {Attempt} ({ErrorType}).", job.Id, job.Attempts, category);
            if (job.Attempts > RetryDelays.Length || clock.GetUtcNow() >= job.ExpiresAt)
                Finish(job, false, category);
            else
            {
                job.LastError = category;
                job.LeaseId = null;
                var next = clock.GetUtcNow() + RetryDelays[job.Attempts - 1];
                job.NextAttemptAt = next < job.ExpiresAt ? next : job.ExpiresAt;
            }
            await db.SaveChangesAsync(ct);
            return true;
        }

        Finish(job, true, null);
        if (job.BookingId is int id)
        {
            db.LichSuDatVes.Add(new LichSuDatVe
            {
                DatVeId = id, HanhDong = GuiVeDienTuService.HanhDongGuiVe,
                TrangThaiSau = TrangThaiDatVe.DaXacNhan.ToString(),
                LyDo = $"Đã gửi vé điện tử qua {sender.TenKenh}",
                NguoiThucHien = job.Actor, ThoiDiem = clock.GetUtcNow()
            });
        }
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<EmailDeliveryJob?> ClaimAsync(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = clock.GetUtcNow();
        var rows = await db.EmailDeliveryJobs.FromSqlInterpolated($"""
            SELECT * FROM "EmailDeliveryJobs"
            WHERE "CompletedAt" IS NULL AND "NextAttemptAt" <= {now}
            ORDER BY "NextAttemptAt", "Id"
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        var job = rows.SingleOrDefault();
        if (job is null) return null;
        if (job.Attempts > RetryDelays.Length || job.ExpiresAt <= now)
            Finish(job, false, job.LastError ?? "Đã hết hạn gửi hoặc hết số lần thử.");
        else
        {
            job.Attempts++;
            job.LeaseId = Guid.NewGuid();
            job.NextAttemptAt = now.AddMinutes(2);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return job;
    }

    private void Finish(EmailDeliveryJob job, bool delivered, string? error)
    {
        job.CompletedAt = clock.GetUtcNow();
        job.Delivered = delivered;
        job.LastError = error;
        job.LeaseId = null;
        job.ProtectedMessage = null;
    }
}
