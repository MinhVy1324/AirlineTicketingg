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
    public async Task<IActionResult> ChangeRequest(string? pnr, CancellationToken cancellationToken)
    {
        var booking = await FindBookingAsync(pnr, cancellationToken);
        if (booking == null) return RedirectToAction(nameof(Index));
        var ve = booking.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var f = ve?.ChuyenBay;
        if (f == null) return RedirectToAction(nameof(Index));

        var alt = await dbContext.ChuyenBays.AsNoTracking()
            .Include(c => c.TuyenBay).ThenInclude(t => t.SanBayDi)
            .Include(c => c.TuyenBay).ThenInclude(t => t.SanBayDen)
            .Include(c => c.MayBay)
            .Where(c => c.TuyenBayId == f.TuyenBayId && c.Id != f.Id && c.GioKhoiHanh > DateTimeOffset.UtcNow && c.TrangThai != TrangThaiChuyenBay.BiHuy)
            .OrderBy(c => c.GioKhoiHanh).FirstOrDefaultAsync(cancellationToken) ?? f;

        var model = new ChangeRequestViewModel
        {
            Booking = ToBookingViewModel(booking, ve, f),
            NewFlight = alt == f ? ToOffer(f, ve!) : ToOffer(alt, ve!),
            NewBaseFare = (alt == f ? f.GiaCoBan : alt.GiaCoBan),
            SeatBaggageText = $"{(string.IsNullOrWhiteSpace(ve?.SoGhe) ? "Chưa chọn ghế" : ve!.SoGhe)} · {FlightMapping.CabinBaggage} · {FlightMapping.CheckedBaggage}",
            AvailabilityText = alt == f ? "Chưa tìm chuyến thay thế tự động" : "Có chuyến thay thế tham khảo",
            AvailabilityNote = alt == f ? "Vui lòng ghi rõ chuyến/ngày mong muốn trong yêu cầu. Bộ phận vận hành sẽ kiểm tra chỗ trống." : "Chuyến thay thế chỉ mang tính tham khảo. Bộ phận vận hành sẽ xác nhận chỗ và mức phí trước khi thu tiền.",
            Reason = "Thay đổi kế hoạch cá nhân"
        };
        ViewData["BookingPnr"] = booking.MaDatVe;
        ViewData["CurrentFlightId"] = f.Id;
        ViewData["NewFlightId"] = alt.Id;
        return View(model);
    }

    [HttpPost("/ve-cua-toi/yeu-cau-doi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRequest(IFormCollection form, CancellationToken cancellationToken)
    {
        var pnr = (form["pnr"].ToString() ?? Request.Query["pnr"].ToString() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(pnr)) pnr = (ViewData["BookingPnr"]?.ToString() ?? "").Trim();
        var booking = await FindBookingAsync(string.IsNullOrWhiteSpace(pnr) ? null : pnr, cancellationToken);
        if (booking == null) return RedirectToAction(nameof(Index));

        var existing = await dbContext.YeuCauDoiHoans.AnyAsync(r => r.DatVeId == booking.Id && r.LoaiYeuCau == LoaiYeuCau.DoiVe && r.TrangThai == TrangThaiYeuCau.ChoDuyet, cancellationToken);
        if (!existing)
        {
            var reason = (form["reason"].ToString() ?? "Thay đổi kế hoạch").Trim();
            int.TryParse(form["newFlightId"].ToString(), out var newFlightId);
            dbContext.YeuCauDoiHoans.Add(new YeuCauDoiHoan
            {
                MaYeuCau = $"CH-{DateTimeOffset.UtcNow:yyMMddHHmm}-{Random.Shared.Next(10,99)}",
                DatVeId = booking.Id,
                LoaiYeuCau = LoaiYeuCau.DoiVe,
                LyDo = string.IsNullOrWhiteSpace(reason) ? "Thay đổi kế hoạch" : reason,
                ThongTinBoSung = (form["note"].ToString() ?? "").Trim(),
                TrangThai = TrangThaiYeuCau.ChoDuyet,
                PhiDoi = FlightMapping.ChangeFee,
                PhiDichVuKhongHoan = booking.PhiDichVu,
                PhiHanhLyKhongHoan = booking.PhiHanhLy,
                ChuyenBayMoiId = newFlightId > 0 ? newFlightId : null,
                NgayTao = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        TempData["TicketMsg"] = "Đã ghi nhận yêu cầu đổi vé. Bộ phận vận hành sẽ kiểm tra và phản hồi.";
        return RedirectToAction(nameof(Requests));
    }

    [HttpGet("/ve-cua-toi/yeu-cau-hoan")]
    public async Task<IActionResult> RefundRequest(string? pnr, CancellationToken cancellationToken)
    {
        var booking = await FindBookingAsync(pnr, cancellationToken);
        if (booking == null) return RedirectToAction(nameof(Index));
        var ve = booking.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var f = ve?.ChuyenBay;
        if (f == null) return RedirectToAction(nameof(Index));

        var model = new RefundRequestViewModel
        {
            Booking = ToBookingViewModel(booking, ve, f),
            SeatBaggageText = $"{(string.IsNullOrWhiteSpace(ve?.SoGhe) ? "Chưa chọn ghế" : ve!.SoGhe)} · {FlightMapping.CabinBaggage} · {FlightMapping.CheckedBaggage}",
            RefundToText = FormatRefundMethod(booking.ThanhToans.OrderByDescending(t => t.NgayTao).FirstOrDefault()),
            DeadlineText = $"Yêu cầu cần gửi trước giờ khởi hành theo điều kiện vé. Giờ khởi hành: {FlightMapping.ToVietnamTime(f.GioKhoiHanh):dd/MM/yyyy HH:mm} (UTC+7).",
            Reason = "Thay đổi kế hoạch cá nhân",
            AdditionalInfo = ""
        };
        ViewData["BookingPnr"] = booking.MaDatVe;
        return View(model);
    }

    [HttpPost("/ve-cua-toi/yeu-cau-hoan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefundRequest(IFormCollection form, CancellationToken cancellationToken)
    {
        var pnr = (form["pnr"].ToString() ?? Request.Query["pnr"].ToString() ?? "").Trim();
        var booking = await FindBookingAsync(string.IsNullOrWhiteSpace(pnr) ? null : pnr, cancellationToken);
        if (booking == null) return RedirectToAction(nameof(Index));

        var existing = await dbContext.YeuCauDoiHoans.AnyAsync(r => r.DatVeId == booking.Id && r.LoaiYeuCau == LoaiYeuCau.HoanVe && r.TrangThai == TrangThaiYeuCau.ChoDuyet, cancellationToken);
        if (!existing)
        {
            var reason = (form["reason"].ToString() ?? "Yêu cầu hoàn vé").Trim();
            var refundFee = FlightMapping.RefundFee;
            var service = booking.PhiDichVu;
            var bag = booking.PhiHanhLy;
            dbContext.YeuCauDoiHoans.Add(new YeuCauDoiHoan
            {
                MaYeuCau = $"RF-{DateTimeOffset.UtcNow:yyMMddHHmm}-{Random.Shared.Next(10,99)}",
                DatVeId = booking.Id,
                LoaiYeuCau = LoaiYeuCau.HoanVe,
                LyDo = string.IsNullOrWhiteSpace(reason) ? "Yêu cầu hoàn vé" : reason,
                ThongTinBoSung = (form["note"].ToString() ?? "").Trim(),
                TrangThai = TrangThaiYeuCau.ChoDuyet,
                PhiHoan = refundFee,
                PhiDichVuKhongHoan = service,
                PhiHanhLyKhongHoan = bag,
                SoTienHoan = Math.Max(0m, booking.TongTien - refundFee - service - bag),
                PhuongThucHoan = FormatRefundMethod(booking.ThanhToans.OrderByDescending(t => t.NgayTao).FirstOrDefault()),
                NgayTao = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        TempData["TicketMsg"] = "Đã ghi nhận yêu cầu hoàn vé. Bộ phận vận hành sẽ kiểm tra và phản hồi.";
        return RedirectToAction(nameof(Requests));
    }

    [HttpGet("/ve-cua-toi/yeu-cau")]
    public async Task<IActionResult> Requests(string? id, CancellationToken cancellationToken)
    {
        List<YeuCauDoiHoan> list;
        try
        {
            IQueryable<YeuCauDoiHoan> q = dbContext.YeuCauDoiHoans.AsNoTracking()
                .Include(r => r.DatVe).ThenInclude(d => d.KhachHang)
                .Include(r => r.DatVe).ThenInclude(d => d.Ves).ThenInclude(v => v.ChuyenBay).ThenInclude(c => c.TuyenBay).ThenInclude(t => t.SanBayDi)
                .Include(r => r.DatVe).ThenInclude(d => d.Ves).ThenInclude(v => v.ChuyenBay).ThenInclude(c => c.TuyenBay).ThenInclude(t => t.SanBayDen)
                .Include(r => r.DatVe).ThenInclude(d => d.ThanhToans)
                .OrderByDescending(r => r.NgayTao);

            var khId = User.KhachHangId();
            if (khId.HasValue) q = q.Where(r => r.DatVe.KhachHangId == khId.Value).OrderByDescending(r => r.NgayTao);
            list = await q.Take(30).ToListAsync(cancellationToken);
        }
        catch (Exception) { list = []; }

        if (list.Count == 0)
        {
            return View(new RequestTrackingViewModel
            {
                Tabs = new[] { new TicketTab("Tất cả", 0, true), new TicketTab("Đang xử lý", 0, false), new TicketTab("Đã hoàn tất", 0, false) },
                Requests = Array.Empty<RequestRow>(),
                DetailCode = "—", DetailPnr = "—", DetailPassenger = "—", DetailTicketNumber = "—",
                DetailStatusText = "Chưa có yêu cầu", DetailStatusTone = "neutral",
                FlightText = "Gửi yêu cầu đổi/hoàn từ trang Vé của tôi để theo dõi tại đây.",
                ReasonText = "—", Events = Array.Empty<OutcomeEvent>(),
                OtherCode = "—", OtherText = "Chưa có yêu cầu nào trong hệ thống."
            });
        }

        var selected = !string.IsNullOrWhiteSpace(id) ? list.FirstOrDefault(r => r.MaYeuCau == id) ?? list.FirstOrDefault() : list.FirstOrDefault();
        var rows = list.Select(r =>
        {
            var ve = r.DatVe?.Ves.FirstOrDefault();
            var route = ve?.ChuyenBay == null ? r.DatVe?.MaDatVe ?? "—" : $"{ve.ChuyenBay.SoHieu} · {ve.ChuyenBay.TuyenBay.SanBayDi.MaSanBay} → {ve.ChuyenBay.TuyenBay.SanBayDen.MaSanBay}";
            return new RequestRow(r.MaYeuCau, r.DatVe?.MaDatVe ?? "—", $"{(r.LoaiYeuCau == LoaiYeuCau.HoanVe ? "Hoàn vé" : "Đổi vé")} · {r.NgayTao.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy}", route, ReqStatusText(r.TrangThai), ReqTone(r.TrangThai), selected != null && r.Id == selected.Id);
        }).ToList();

        var sel = selected!;
        var selVe = sel.DatVe?.Ves.FirstOrDefault();
        var selFlight = selVe?.ChuyenBay;
        var processing = list.Count(r => r.TrangThai == TrangThaiYeuCau.ChoDuyet || r.TrangThai == TrangThaiYeuCau.DaDuyet);
        var done = list.Count(r => r.TrangThai == TrangThaiYeuCau.HoanTat || r.TrangThai == TrangThaiYeuCau.TuChoi);
        var paid = sel.DatVe?.TongTien ?? 0m;

        return View(new RequestTrackingViewModel
        {
            Tabs = new[]
            {
                new TicketTab("Tất cả", list.Count, true),
                new TicketTab("Đang xử lý", processing, false),
                new TicketTab("Đã hoàn tất", done, false)
            },
            Requests = rows,
            UpdatedAtText = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy"),
            DetailCode = sel.MaYeuCau,
            DetailPnr = sel.DatVe?.MaDatVe ?? "—",
            DetailPassenger = selVe?.HanhKhach?.HoTen ?? sel.DatVe?.KhachHang?.HoTen ?? "—",
            DetailTicketNumber = selVe?.MaVe ?? "—",
            DetailStatusText = ReqStatusText(sel.TrangThai),
            DetailStatusTone = ReqTone(sel.TrangThai),
            FlightText = selFlight == null ? "—" : $"{selFlight.SoHieu} · {selFlight.GioKhoiHanh.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy HH:mm}",
            ReasonText = sel.LyDo,
            Events = BuildRequestEvents(sel),
            PaidTotal = paid,
            RefundFee = sel.PhiHoan > 0 ? sel.PhiHoan : FlightMapping.RefundFee,
            ServiceFee = sel.PhiDichVuKhongHoan,
            BaggageFee = sel.PhiHanhLyKhongHoan,
            RefundToText = sel.PhuongThucHoan ?? "Nguồn thanh toán gốc",
            OtherCode = rows.Count > 1 ? rows.First(r => r.Code != sel.MaYeuCau).Code : "—",
            OtherText = rows.Count > 1 ? $"{rows.First(r => r.Code != sel.MaYeuCau).Pnr} · {rows.First(r => r.Code != sel.MaYeuCau).TypeText}" : "Không có yêu cầu khác.",
            OtherStatusText = rows.Count > 1 ? rows.First(r => r.Code != sel.MaYeuCau).StatusText : ""
        });
    }

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

    private async Task<DatVe?> FindBookingAsync(string? pnr, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pnr)) return null;
        try
        {
            return await dbContext.DatVes.AsNoTracking()
                .Include(x => x.KhachHang)
                .Include(x => x.ThanhToans)
                .Include(x => x.Ves).ThenInclude(x => x.HanhKhach)
                .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
                .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
                .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.MayBay)
                .FirstOrDefaultAsync(x => x.MaDatVe == pnr.Trim(), ct);
        }
        catch { return null; }
    }

    private static BookingViewModel ToBookingViewModel(DatVe b, Ve? ve, ChuyenBay f)
    {
        var dep = FlightMapping.ToVietnamTime(f.GioKhoiHanh);
        var passenger = ve?.HanhKhach;
        var pax = passenger == null ? new PassengerFormModel() : new PassengerFormModel
        {
            LastName = passenger.HoTen ?? "",
            FirstName = "",
            Email = b.EmailLienHe ?? passenger.Email ?? "",
            Phone = b.SoDienThoaiLienHe ?? passenger.SoDienThoai ?? "",
            IdentityNumber = passenger.SoHoChieu ?? "",
            DateOfBirth = passenger.NgaySinh?.ToString("dd/MM/yyyy") ?? "",
            Nationality = passenger.QuocTich ?? "Việt Nam"
        };
        var firstVe = ve ?? b.Ves.First();
        return new BookingViewModel
        {
            Flight = ToOffer(f, firstVe),
            Criteria = new FlightSearchCriteria
            {
                OriginCity = f.TuyenBay.SanBayDi.ThanhPho, OriginCode = f.TuyenBay.SanBayDi.MaSanBay,
                DestinationCity = f.TuyenBay.SanBayDen.ThanhPho, DestinationCode = f.TuyenBay.SanBayDen.MaSanBay,
                DepartDateText = dep.ToString("dd/MM/yyyy")
            },
            Price = new PriceSummary { BaseFare = Math.Max(0m, b.TongTien - b.ThuePhi - b.PhiDichVu - b.PhiHanhLy), Taxes = b.ThuePhi, ServiceFee = b.PhiDichVu, BaggageFee = b.PhiHanhLy },
            Passenger = pax,
            SeatCode = string.IsNullOrWhiteSpace(firstVe.SoGhe) ? "—" : firstVe.SoGhe!,
            Pnr = b.MaDatVe,
            TicketNumber = firstVe.MaVe ?? "—",
            TotalText = Money.Vnd(b.TongTien),
            Email = b.EmailLienHe ?? pax.Email,
            Phone = b.SoDienThoaiLienHe ?? pax.Phone
        };
    }

    private static string ReqStatusText(TrangThaiYeuCau s) => s switch { TrangThaiYeuCau.ChoDuyet => "Chờ duyệt", TrangThaiYeuCau.DaDuyet => "Đã duyệt · Chờ xử lý", TrangThaiYeuCau.TuChoi => "Từ chối", TrangThaiYeuCau.HoanTat => "Hoàn tất", _ => s.ToString() };
    private static string ReqTone(TrangThaiYeuCau s) => s switch { TrangThaiYeuCau.ChoDuyet => "warning", TrangThaiYeuCau.DaDuyet => "info", TrangThaiYeuCau.HoanTat => "success", TrangThaiYeuCau.TuChoi => "danger", _ => "neutral" };
        private static string FormatRefundMethod(ThanhToan? t) => t == null ? "Nguồn thanh toán gốc" : (string.IsNullOrWhiteSpace(t.MaGiaoDich) ? t.PhuongThuc.ToString() : $"{t.PhuongThuc} · {t.MaGiaoDich}");

    private static IReadOnlyList<OutcomeEvent> BuildRequestEvents(YeuCauDoiHoan r)
    {
        var vn = TimeSpan.FromHours(7);
        var list = new List<OutcomeEvent> { new(r.NgayTao.ToOffset(vn).ToString("dd/MM/yyyy HH:mm"), "Đã tiếp nhận", $"Ghi nhận yêu cầu {r.MaYeuCau}.") };
        if (r.TrangThai != TrangThaiYeuCau.ChoDuyet && r.NgayXuLy.HasValue)
            list.Add(new(r.NgayXuLy.Value.ToOffset(vn).ToString("dd/MM/yyyy HH:mm"), ReqStatusText(r.TrangThai), string.IsNullOrWhiteSpace(r.GhiChuXuLy) ? "Yêu cầu đã được cập nhật." : r.GhiChuXuLy!));
        else if (r.TrangThai == TrangThaiYeuCau.ChoDuyet)
            list.Add(new("Chưa có", "Đang chờ xử lý", "Bộ phận vận hành sẽ kiểm tra điều kiện và phản hồi."));
        return list;
    }
}
