using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Controllers;

public class TicketsController(AppDbContext dbContext) : Controller
{
    [HttpGet("/ve-cua-toi")]
    public async Task<IActionResult> Index(string? tab, string? pnr, CancellationToken cancellationToken)
    {
        var active = NormalizeTab(tab);

        List<DatVe> bookings;
        try
        {
            bookings = await dbContext.DatVes
                .AsNoTracking()
                .Include(x => x.KhachHang)
                .Include(x => x.ThanhToans)
                .Include(x => x.Ves).ThenInclude(x => x.HanhKhach)
                .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
                .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
                .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.MayBay)
                .OrderByDescending(x => x.NgayDat)
                .Take(50)
                .ToListAsync(cancellationToken);
        }
        catch (Exception)
        {
            bookings = [];
        }

        var upcoming = bookings.Where(b => IsUpcoming(b)).ToList();
        var done = bookings.Where(b => IsDone(b)).ToList();
        var cancelled = bookings.Where(b => IsCancelled(b)).ToList();

        var tabs = new[]
        {
            new TicketTab("Sắp khởi hành", upcoming.Count, active == 0),
            new TicketTab("Đã hoàn tất", done.Count, active == 1),
            new TicketTab("Đã hủy / hoàn", cancelled.Count, active == 2)
        };

        var current = active switch { 1 => done, 2 => cancelled, _ => upcoming };
        var selected = !string.IsNullOrWhiteSpace(pnr)
            ? current.FirstOrDefault(b => string.Equals(b.MaDatVe, pnr, StringComparison.OrdinalIgnoreCase)) ?? current.FirstOrDefault()
            : current.FirstOrDefault();

        var items = current.Select(b => ToListItem(b, selected is not null && b.Id == selected.Id, active)).ToList();
        var detail = selected is not null ? ToDetail(selected) : EmptyDetail();

        return View(new TicketsViewModel { Tabs = tabs, Tickets = items, Detail = detail });
    }

    [HttpGet("/ve-cua-toi/chuyen-doi-gio")]
    public IActionResult ScheduleChanged() => RedirectToAction(nameof(Index));

    [HttpGet("/ve-cua-toi/chuyen-huy")]
    public IActionResult Cancelled() => RedirectToAction(nameof(Index), new { tab = "cancelled" });

    [HttpGet("/ve-cua-toi/yeu-cau-doi")]
    public IActionResult ChangeRequest() => RedirectToAction(nameof(Index));

    [HttpPost("/ve-cua-toi/yeu-cau-doi")]
    [ValidateAntiForgeryToken]
    public IActionResult ChangeRequest(IFormCollection form) => RedirectToAction(nameof(Index));

    [HttpGet("/ve-cua-toi/yeu-cau-hoan")]
    public IActionResult RefundRequest() => RedirectToAction(nameof(Index));

    [HttpPost("/ve-cua-toi/yeu-cau-hoan")]
    [ValidateAntiForgeryToken]
    public IActionResult RefundRequest(IFormCollection form) => RedirectToAction(nameof(Index));

    [HttpGet("/ve-cua-toi/yeu-cau")]
    public IActionResult Requests() => RedirectToAction(nameof(Index));

    private static int NormalizeTab(string? tab) => tab?.Trim().ToLowerInvariant() switch
    {
        "done" or "completed" => 1,
        "cancelled" or "canceled" or "refund" => 2,
        _ => 0
    };

    private static bool IsCancelled(DatVe b) =>
        b.TrangThai is TrangThaiDatVe.DaHuy or TrangThaiDatVe.HoanTien or TrangThaiDatVe.HetHan
        || b.Ves.Any(v => v.TrangThai is TrangThaiVe.DaHuy or TrangThaiVe.DaHoanTien);

    private static bool IsDone(DatVe b)
    {
        if (IsCancelled(b)) return false;
        var firstFlight = b.Ves.Select(v => v.ChuyenBay).FirstOrDefault();
        return firstFlight is not null && FlightMapping.ToVietnamTime(firstFlight.GioHaCanh) < DateTimeOffset.UtcNow.AddHours(7);
    }

    private static bool IsUpcoming(DatVe b) => !IsCancelled(b) && !IsDone(b);

    private static TicketListItem ToListItem(DatVe b, bool selected, int activeTab)
    {
        var ve = b.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var f = ve?.ChuyenBay;
        var dep = f is null ? (DateTimeOffset?)null : FlightMapping.ToVietnamTime(f.GioKhoiHanh);
        var route = f is null ? b.MaDatVe : $"{f.TuyenBay.SanBayDi.ThanhPho} → {f.TuyenBay.SanBayDen.ThanhPho}";
        var schedule = dep is null ? "—" : $"{dep:dd/MM/yyyy} · {dep:HH:mm} · {f!.SoHieu}";
        var paxCount = Math.Max(1, b.Ves.Count);
        var (statusText, statusTone) = StatusFor(b);
        var note = activeTab == 2
            ? (b.TrangThai == TrangThaiDatVe.HoanTien ? "Đã hoàn tiền" : "Đã hủy")
            : b.TrangThai == TrangThaiDatVe.ChoThanhToan ? "Chờ thanh toán" : "Xem chi tiết";

        return new TicketListItem(
            Pnr: b.MaDatVe,
            StatusText: statusText,
            StatusTone: statusTone,
            RouteText: route,
            ScheduleText: schedule,
            PassengerText: $"{paxCount} hành khách",
            PriceText: Money.Vnd(b.TongTien),
            NoteText: note,
            NoteTone: statusTone,
            IsSelected: selected);
    }

    private static (string text, string tone) StatusFor(DatVe b) => b.TrangThai switch
    {
        TrangThaiDatVe.DaXacNhan => ("Đã xác nhận", "success"),
        TrangThaiDatVe.ChoThanhToan => ("Chờ thanh toán", "warning"),
        TrangThaiDatVe.DaHuy => ("Đã hủy", "danger"),
        TrangThaiDatVe.HoanTien => ("Hoàn tiền", "info"),
        TrangThaiDatVe.HetHan => ("Hết hạn", "danger"),
        _ => (b.TrangThai.ToString(), "info")
    };

    private static TicketDetailViewModel ToDetail(DatVe b)
    {
        var ve = b.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var f = ve?.ChuyenBay;
        var flight = f is null ? EmptyFlight() : ToOffer(f, ve!);
        var paid = b.ThanhToans.Where(t => t.TrangThai == TrangThaiThanhToan.ThanhCong).Sum(t => t.SoTien);
        var paymentText = paid > 0 ? $"Đã thanh toán · {Money.Vnd(paid)}" : "Chưa thanh toán";

        return new TicketDetailViewModel
        {
            Flight = flight,
            Pnr = b.MaDatVe,
            PassengerName = ve?.HanhKhach?.HoTen ?? b.KhachHang?.HoTen ?? "Hành khách",
            SeatCode = string.IsNullOrWhiteSpace(ve?.SoGhe) ? "Chưa chọn" : ve.SoGhe,
            TicketNumber = string.IsNullOrWhiteSpace(ve?.MaVe) ? "Chưa phát hành" : ve.MaVe,
            BaggageText = $"{FlightMapping.CabinBaggage} · {FlightMapping.CheckedBaggage}",
            PaymentText = paymentText,
            BookedAtText = FlightMapping.ToVietnamTime(b.NgayDat).ToString("dd/MM/yyyy · HH:mm '(UTC+7)'"),
            ChangeFee = FlightMapping.ChangeFee,
            RefundFee = FlightMapping.RefundFee,
            ServiceFee = FlightMapping.ServiceFeePerPassenger,
            ExtraBaggageFee = 0m,
            CanManage = b.TrangThai is TrangThaiDatVe.DaXacNhan or TrangThaiDatVe.ChoThanhToan
        };
    }

    private static FlightOffer ToOffer(ChuyenBay f, Ve ve)
    {
        var dep = FlightMapping.ToVietnamTime(f.GioKhoiHanh);
        var arr = FlightMapping.ToVietnamTime(f.GioHaCanh);
        return new FlightOffer(
            Id: f.Id.ToString(),
            AirlineName: FlightMapping.AirlineNameFor(f.SoHieu),
            FlightNumber: f.SoHieu,
            Aircraft: f.MayBay?.TenMayBay ?? f.MayBay?.MaMayBay ?? string.Empty,
            FareName: string.IsNullOrWhiteSpace(ve.HangVe) ? "Phổ thông" : ve.HangVe,
            From: new AirportInfo(f.TuyenBay.SanBayDi.MaSanBay, f.TuyenBay.SanBayDi.ThanhPho, f.TuyenBay.SanBayDi.TenSanBay),
            To: new AirportInfo(f.TuyenBay.SanBayDen.MaSanBay, f.TuyenBay.SanBayDen.ThanhPho, f.TuyenBay.SanBayDen.TenSanBay),
            Departure: dep,
            Arrival: arr,
            PricePerPassenger: ve.GiaVe > 0 ? ve.GiaVe : f.GiaCoBan,
            Stops: "Bay thẳng",
            CabinBaggage: FlightMapping.CabinBaggage,
            CheckedBaggage: FlightMapping.CheckedBaggage,
            ChangeFee: FlightMapping.ChangeFee,
            RefundFee: FlightMapping.RefundFee);
    }

    private static FlightOffer EmptyFlight() => new(
        Id: "0", AirlineName: "—", FlightNumber: "—", Aircraft: "", FareName: "—",
        From: new AirportInfo("---", "—", "—"), To: new AirportInfo("---", "—", "—"),
        Departure: DateTimeOffset.UtcNow, Arrival: DateTimeOffset.UtcNow,
        PricePerPassenger: 0m, Stops: "—", CabinBaggage: "—", CheckedBaggage: "—",
        ChangeFee: 0m, RefundFee: 0m);

    private static TicketDetailViewModel EmptyDetail() => new()
    {
        Flight = EmptyFlight(),
        Pnr = "—",
        PassengerName = "Chưa có dữ liệu",
        SeatCode = "—",
        TicketNumber = "—",
        BaggageText = "—",
        PaymentText = "—",
        BookedAtText = "Nạp dữ liệu mẫu (docs/seed) hoặc tạo đặt chỗ để hiển thị vé.",
        CanManage = false
    };
}
