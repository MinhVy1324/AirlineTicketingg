using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Controllers;

[Authorize(Policy = "StaffOnly")]
[Route("/quan-ly/bien-dong-chuyen-bay")]
public sealed class FlightDisruptionsController(FlightDisruptionService disruptions, TimeProvider clock) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? id, bool history, CancellationToken ct)
    {
        var query = disruptions.Changes().AsNoTracking();
        var list = await query.Where(x => history ? x.NgayXuLy != null : x.NgayXuLy == null)
            .OrderByDescending(x => x.NgayTao).ThenByDescending(x => x.Id).Take(50).ToListAsync(ct);
        var selected = id.HasValue
            ? list.FirstOrDefault(x => x.Id == id.Value) ?? await query.SingleOrDefaultAsync(x => x.Id == id.Value, ct)
            : list.FirstOrDefault();
        var canAct = selected is { NgayXuLy: null } && selected.DatVe.TrangThai == TrangThaiDatVe.DaXacNhan
            && !FlightDisruptionService.HasRefundInProgress(selected.DatVe);
        var candidates = canAct
            ? await disruptions.Alternatives(selected!).AsNoTracking().OrderBy(x => x.GioKhoiHanh).ThenBy(x => x.Id)
                .Take(20).ToListAsync(ct)
            : [];
        var now = clock.GetUtcNow();
        return View(new FlightDisruptionsViewModel(list.Select(Row).ToArray(), selected is null ? null : Row(selected),
            candidates.Select(f => new DisruptionAlternative(f.Id, f.SoHieu + " · " +
                FlightDisruptionService.DateText(f.GioKhoiHanh, f.GioHaCanh))).ToArray(), canAct,
            canAct && FlightDisruptionService.CanRefundDemo(selected!.DatVe, now), Money.Vnd(selected?.DatVe.TongTien ?? 0),
            history, TempData["DisruptionMessage"] as string, TempData["DisruptionError"] is true));
    }

    [HttpPost("{id:int}/chuyen-thay-the")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Rebook(int id, int flightId, bool confirmed, CancellationToken ct) =>
        ProcessAsync(id, confirmed, () => disruptions.RebookAsync(id, flightId, User.DisplayName(), ct),
            "Đã chuyển các vé trên chặng sang chuyến thay thế.");

    [HttpPost("{id:int}/hoan-demo")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Refund(int id, bool confirmed, CancellationToken ct) =>
        ProcessAsync(id, confirmed, () => disruptions.RefundDemoAsync(id, User.TaiKhoanId(), User.DisplayName(), ct),
            "Đã hoàn toàn bộ đặt chỗ bằng thanh toán mô phỏng.");

    private async Task<IActionResult> ProcessAsync(int id, bool confirmed, Func<Task> action, string message)
    {
        try
        {
            if (!confirmed)
                throw new BookingValidationException("confirmation", "Vui lòng xác nhận phương án đã thống nhất với khách.");
            await action();
            TempData["DisruptionMessage"] = message;
            return RedirectToAction(nameof(Index), new { id, history = true });
        }
        catch (BookingValidationException error)
        {
            TempData["DisruptionMessage"] = error.Message;
            TempData["DisruptionError"] = true;
            return RedirectToAction(nameof(Index), new { id });
        }
    }

    private static FlightDisruptionRow Row(BienDongDatVe change)
    {
        var route = change.ChuyenBay.TuyenBay;
        return new(change.Id, change.DatVe.MaDatVe, change.SoHieuCu, change.DatVe.KhachHang.HoTen,
            $"{route.SanBayDi.MaSanBay} → {route.SanBayDen.MaSanBay}", change.BiHuy,
            FlightDisruptionService.DateText(change.KhoiHanhCu, change.HaCanhCu),
            change.KhoiHanhMoi is { } start && change.HaCanhMoi is { } end
                ? $"{change.SoHieuMoi} · {FlightDisruptionService.DateText(start, end)}" : null, change.KetQua);
    }
}
