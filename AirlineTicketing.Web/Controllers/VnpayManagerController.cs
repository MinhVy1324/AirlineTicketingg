using System.Globalization;
using System.Text.Json;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.Infrastructure.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AirlineTicketing.Web.Controllers;

public sealed record VnpayManagerPage(
    string? Merchant, string Tab, string Query, string Channel, string Status, string Day, int Page, int Pages,
    decimal Collected, decimal Refunded, int PendingCollections, int PendingRefunds,
    IReadOnlyList<ThanhToan> Payments, IReadOnlyList<HoanTien> Refunds, IReadOnlyList<ChungTuDoiSoat> Evidence,
    PaymentEvidenceForm? RejectedEvidence);

public sealed record PaymentEvidenceForm(int Id, string? Receipt = null, string? Amount = null,
    string? RefundNumber = null, string? Result = null, bool Confirmed = false, string? Field = null, string? Error = null);

[Authorize(Policy = "StaffOnly")]
public sealed class VnpayManagerController(AppDbContext db, VnpayOperations operations,
    PaymentReconciliationService reconciliation, IOptions<VnpayOptions> options) : Controller
{
    private const int PageSize = 15;

    [HttpGet("/quan-ly/thanh-toan-vnpay")]
    public async Task<IActionResult> Index(string? tab, string? q, string? kenh, string? trangThai, string? ngay, int trang, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var activeTab = tab == "hoan" ? "hoan" : "thu";
        var channel = kenh is "vnpay" or "qr" or "demo" or "other" ? kenh : "";
        var status = trangThai is "waiting" or "done" or "failed" ? trangThai : "";
        var query = (q ?? "").Trim().ToUpperInvariant();
        query = query[..Math.Min(query.Length, 100)];

        IQueryable<ThanhToan> payments = db.ThanhToans.AsNoTracking();
        payments = channel switch
        {
            "vnpay" => payments.Where(p => p.NhaCungCap == VnpayOptions.Provider),
            "qr" => payments.Where(p => p.NhaCungCap == VietQr.Provider),
            "demo" => payments.Where(p => p.NhaCungCap == "DoiVe Demo" || p.NhaCungCap.StartsWith("Sandbox ")),
            "other" => payments.Where(p => p.NhaCungCap != VnpayOptions.Provider && p.NhaCungCap != VietQr.Provider
                && p.NhaCungCap != "DoiVe Demo" && !p.NhaCungCap.StartsWith("Sandbox ")),
            _ => payments
        };

        if (query.Length > 0)
        {
            payments = payments.Where(p => p.DatVe.MaDatVe.Contains(query)
                || p.MaGiaoDich!.ToUpper().Contains(query) || p.MaGiaoDichNhaCungCap!.Contains(query)
                || p.HoanTiens.Any(r => r.MaGiaoDich!.ToUpper().Contains(query) || r.MaGiaoDichNhaCungCap!.Contains(query))
                || db.ChungTuDoiSoats.Any(e => e.ThanhToanId == p.Id
                    && (e.MaChungTu.Contains(query) || e.MaLenh.Contains(query))));
        }
        var matchedPayments = payments;
        var refunds = db.HoanTiens.AsNoTracking().Where(r => matchedPayments.Any(p => p.Id == r.ThanhToanId));
        var selectedDay = "";
        if (DateOnly.TryParseExact(ngay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            && day > DateOnly.MinValue && day < DateOnly.MaxValue)
        {
            selectedDay = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
            var end = start.AddDays(1);
            payments = payments.Where(p => (p.NgayHoanTat ?? p.NgayTao) >= start && (p.NgayHoanTat ?? p.NgayTao) < end);
            refunds = refunds.Where(r => (r.NgayHoanTat ?? r.NgayTao) >= start && (r.NgayHoanTat ?? r.NgayTao) < end);
        }


        var collected = await payments.Where(p => p.TrangThai == TrangThaiThanhToan.ThanhCong && p.DonViTienTe == "VND")
            .SumAsync(p => p.SoTien, ct);
        var refunded = await refunds.Where(r => r.TrangThai == TrangThaiHoanTien.ThanhCong && r.DonViTienTe == "VND")
            .SumAsync(r => r.SoTien, ct);
        var pendingCollections = await payments.CountAsync(p => p.TrangThai == TrangThaiThanhToan.ChoXuLy, ct);
        var pendingRefunds = await refunds.CountAsync(r => r.TrangThai == TrangThaiHoanTien.ChoXuLy, ct);

        payments = status switch
        {
            "waiting" => payments.Where(p => p.TrangThai == TrangThaiThanhToan.ChoXuLy),
            "done" => payments.Where(p => p.TrangThai == TrangThaiThanhToan.ThanhCong),
            "failed" => payments.Where(p => p.TrangThai == TrangThaiThanhToan.ThatBai),
            _ => payments
        };
        refunds = status switch
        {
            "waiting" => refunds.Where(r => r.TrangThai == TrangThaiHoanTien.ChoXuLy),
            "done" => refunds.Where(r => r.TrangThai == TrangThaiHoanTien.ThanhCong),
            "failed" => refunds.Where(r => r.TrangThai == TrangThaiHoanTien.ThatBai),
            _ => refunds
        };
        var total = activeTab == "thu" ? await payments.CountAsync(ct) : await refunds.CountAsync(ct);
        var pages = Math.Max(1, (total + PageSize - 1) / PageSize);
        var page = Math.Clamp(trang, 1, pages);

        List<ThanhToan> paymentRows = activeTab == "thu"
            ? await payments.Include(p => p.DatVe).Include(p => p.HoanTiens).OrderByDescending(p => p.Id)
                .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct)
            : [];
        List<HoanTien> refundRows = activeTab == "hoan"
            ? await refunds.Include(r => r.ThanhToan).ThenInclude(p => p.DatVe).OrderByDescending(r => r.Id)
                .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct)
            : [];

        var paymentIds = paymentRows.Select(p => p.Id).ToArray();
        var refundIds = refundRows.Select(r => r.Id).ToArray();
        var evidence = await db.ChungTuDoiSoats.AsNoTracking()
            .Where(e => e.HoanTienId.HasValue ? refundIds.Contains(e.HoanTienId.Value) : paymentIds.Contains(e.ThanhToanId))
            .OrderByDescending(e => e.Id).ToListAsync(ct);

        var rejected = TempData["RejectedPaymentEvidence"] is string json
            ? JsonSerializer.Deserialize<PaymentEvidenceForm>(json) : null;
        if (rejected is not null
            && !paymentRows.Any(p => p.Id == rejected.Id && p.NhaCungCap == VietQr.Provider && p.TrangThai == TrangThaiThanhToan.ChoXuLy)
            && !refundRows.Any(r => r.Id == rejected.Id && r.TrangThai == TrangThaiHoanTien.ChoXuLy
                && (r.NhaCungCap == VietQr.Provider || r.NhaCungCap == VnpayOptions.Provider && r.NgayGuiCongThanhToan.HasValue)))
        {
            TempData["PaymentMessage"] = rejected.Error;
            TempData["PaymentError"] = true;
            rejected = null;
        }
        return View(new VnpayManagerPage(options.Value.Ready ? options.Value.MerchantCode : null, activeTab, query, channel,
            status, selectedDay, page, pages, collected, refunded, pendingCollections, pendingRefunds, paymentRows, refundRows, evidence, rejected));
    }

    [HttpPost("/quan-ly/thanh-toan-vnpay/truy-van")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reconcile(int id, string? pnr, CancellationToken ct) =>
        ApplyAsync(() => operations.ReconcileAsync(id, ct), "thu", pnr);

    [HttpPost("/quan-ly/thanh-toan-vnpay/hoan")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Refund(int id, string? reference, string? pnr, CancellationToken ct) =>
        ApplyAsync(() => operations.SendRefundAsync(id, reference, Actor, ct), "hoan", pnr);

    [HttpPost("/quan-ly/thanh-toan-vnpay/doi-soat-thu")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RecordCollection(int id, string? reference, string? receipt, string? amount, string? pnr,
        CancellationToken ct) =>
        ApplyAsync(() => reconciliation.RecordTransferAsync(id, reference, receipt, amount, Actor, ct), "thu", pnr,
            new(id, Limit(receipt, 64), Limit(amount, 24)));

    [HttpPost("/quan-ly/thanh-toan-vnpay/doi-soat-hoan")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RecordRefund(int id, string? reference, string? receipt, string? amount,
        string? refundNumber, string? result, bool confirmed, string? pnr, CancellationToken ct) =>
        ApplyAsync(() =>
        {
            if (!confirmed || result is not ("success" or "failed"))
                throw new VnpayApiException("Chọn kết quả cuối và xác nhận đã kiểm tra chứng từ của lệnh hoàn.");
            return reconciliation.RecordRefundAsync(id, reference, receipt, amount, refundNumber?.Trim(), result == "success", Actor, ct);
        }, "hoan", pnr, new(id, Limit(receipt, 64), Limit(amount, 24), Limit(refundNumber, 15), Limit(result, 8), confirmed));

    [HttpPost("/quan-ly/thanh-toan-vnpay/lap-lai-lenh-hoan")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> PrepareRetry(int id, string? reference, string? pnr, CancellationToken ct) =>
        ApplyAsync(() => reconciliation.PrepareRefundRetryAsync(id, reference, Actor, ct), "hoan", pnr);

    private string Actor => User.TaiKhoanId() is int staffId ? $"Staff-{staffId}" : User.DisplayName();

    private static string? Limit(string? value, int length) => value is not null && value.Length <= length ? value : null;

    private async Task<IActionResult> ApplyAsync(Func<Task<string>> operation, string tab, string? pnr, PaymentEvidenceForm? form = null)
    {
        string? fragment = null;
        try { TempData["PaymentMessage"] = await operation(); }
        catch (VnpayApiException ex)
        {
            if (form is null)
            {
                TempData["PaymentMessage"] = ex.Message;
                TempData["PaymentError"] = true;
            }
            else
            {
                TempData["RejectedPaymentEvidence"] = JsonSerializer.Serialize(form with { Field = ex.Field, Error = ex.Message });
                fragment = $"doi-soat-{tab}-{form.Id}";
            }
        }
        var filters = Request.Query;
        return RedirectToAction(nameof(Index), null, new
        {
            tab,
            q = filters.ContainsKey("q") ? Limit(filters["q"].ToString(), 100) : Limit(pnr, 100),
            kenh = Limit(filters["kenh"].ToString(), 12),
            trangThai = Limit(filters["trangThai"].ToString(), 12),
            ngay = Limit(filters["ngay"].ToString(), 10),
            trang = int.TryParse(filters["trang"], out var page) && page > 1 ? (int?)page : null
        }, fragment);
    }
}
