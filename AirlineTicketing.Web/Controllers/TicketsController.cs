<<<<<<< Updated upstream
using AirlineTicketing.Web.Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
=======
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.Infrastructure.Payments;
using AirlineTicketing.Web.Infrastructure.Email;
using AirlineTicketing.Web.Infrastructure.Tickets;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using static AirlineTicketing.Web.Infrastructure.UiLanguage;
>>>>>>> Stashed changes

namespace AirlineTicketing.Web.Controllers;

[Authorize]
<<<<<<< Updated upstream
public class TicketsController : Controller
{
    [HttpGet("/ve-cua-toi")]
    public IActionResult Index(string? tab) => View("Index", DemoCatalog.Tickets(tab));
=======
public class TicketsController(
    AppDbContext dbContext,
    CauHinhHeThongBUS cauHinhHeThong,
    GuiVeDienTuService guiVeDienTu, EmailDeliveryQueue emailQueue,
    TicketAfterSalesService afterSales, FlightDisruptionService disruptions) : Controller
{
    [HttpGet("/ve-cua-toi")]
    public async Task<IActionResult> Index(string? tab, string? pnr, string? q, string? date, CancellationToken cancellationToken)
    {
        var active = NormalizeTab(tab);
        var search = q?.Trim();
        var pnrCode = pnr?.Trim().ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;
        var query = ScopedBookings().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLowerInvariant();
            query = query.Where(b => b.MaDatVe.ToLower().Contains(term) || b.Ves.Any(v =>
                v.ChuyenBay.TuyenBay.SanBayDen.MaSanBay.ToLower().Contains(term)
                || v.ChuyenBay.TuyenBay.SanBayDen.ThanhPho.ToLower().Contains(term)
                || v.ChuyenBay.TuyenBay.SanBayDen.TenSanBay.ToLower().Contains(term)));
        }

        date = date switch { "upcoming" => "upcoming", "past" => "past", _ => null };
        query = date switch
        {
            "upcoming" => query.Where(b => b.Ves.Any(v => v.ChuyenBay.GioKhoiHanh >= now)),
            "past" => query.Where(b => b.Ves.Any() && b.Ves.All(v => v.ChuyenBay.GioKhoiHanh < now)),
            _ => query
        };

        var bookings = await query.AsSplitQuery()
            .Include(x => x.KhachHang)
            .Include(x => x.BienDongs)
            .Include(x => x.ThanhToans).ThenInclude(x => x.HoanTiens)
            .Include(x => x.Ves).ThenInclude(x => x.HanhKhach)
            .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.MayBay)
            .OrderByDescending(x => pnrCode != null && x.MaDatVe == pnrCode)
            .ThenByDescending(x => x.NgayDat).ThenByDescending(x => x.Id)
            .Take(50).ToListAsync(cancellationToken);

        var upcoming = bookings.Where(IsUpcoming).ToList();
        var done = bookings.Where(IsDone).ToList();
        var cancelled = bookings.Where(IsCancelled).ToList();

        if (tab is null && pnrCode is not null)
        {
            var matching = bookings.FirstOrDefault(b => b.MaDatVe == pnrCode);
            if (matching is not null) active = IsCancelled(matching) ? 2 : IsDone(matching) ? 1 : 0;
        }

        string? activeTab = active switch { 1 => "done", 2 => "cancelled", _ => null };
        var tabs = new[]
        {
            new TicketTab(T("Sắp khởi hành", "Upcoming"), upcoming.Count, active == 0, Url.Action(nameof(Index), new { q = search, date })),
            new TicketTab(T("Đã hoàn tất", "Completed"), done.Count, active == 1, Url.Action(nameof(Index), new { tab = "done", q = search, date })),
            new TicketTab(T("Đã hủy / hoàn", "Cancelled or refunded"), cancelled.Count, active == 2, Url.Action(nameof(Index), new { tab = "cancelled", q = search, date }))
        };
        var current = active switch { 1 => done, 2 => cancelled, _ => upcoming };
        var selected = current.FirstOrDefault(b => b.MaDatVe == pnrCode) ?? current.FirstOrDefault();
        var items = current.Select(b => ToListItem(b, b.Id == selected?.Id, active) with
        {
            ActionHref = Url.Action(nameof(Index), new { tab = activeTab, pnr = b.MaDatVe, q = search, date })
        }).ToList();
        var detail = selected is null ? null : ToDetail(selected, await cauHinhHeThong.LayChinhSachAsync(cancellationToken));

        return View(new TicketsViewModel
        {
            Tabs = tabs, Tickets = items, Detail = detail, ActiveTab = activeTab, Search = search, DateFilter = date
        });
    }

    /// <summary>
    /// Tải vé điện tử PDF. Người được phép: chủ vé đã đăng nhập, nhân viên quản lý,
    /// hoặc chính phiên vừa đặt chỗ (session giữ mã đặt chỗ sau khi thanh toán).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("/ve/{pnr}/pdf")]
    public async Task<IActionResult> TicketPdf(string pnr, CancellationToken cancellationToken)
    {
        var booking = await FindBookingForTicketAsync(pnr, cancellationToken);
        if (booking is null || booking.TrangThai != TrangThaiDatVe.DaXacNhan) return NotFound();
        if (!CoQuyenXemVe(booking)) return NotFound();

        var pdf = await guiVeDienTu.LayPdfAsync(booking.Id, cancellationToken);
        if (pdf is null) return NotFound();

        Response.Headers.CacheControl = "no-store";
        return File(pdf, "application/pdf", $"ve-dien-tu-{booking.MaDatVe}.pdf");
    }

    [AllowAnonymous]
    [HttpPost("/ve/{pnr}/gui-lai-email")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AppRateLimits.ResendTicket)]
    public async Task<IActionResult> ResendTicketEmail(
        string pnr,
        string? quayLai,
        CancellationToken cancellationToken)
    {
        var booking = await FindBookingForTicketAsync(pnr, cancellationToken);
        if (booking is null || !CoQuyenXemVe(booking)) return NotFound();

        var nguoiThucHien = User.Identity?.IsAuthenticated == true ? User.DisplayName() : "Khách vãng lai";
        var queued = await emailQueue.QueueTicketResendAsync(booking.Id, nguoiThucHien, cancellationToken);
        TempData["TicketEmailMessage"] = queued
            ? T("Yêu cầu gửi vé đã vào hàng đợi. Bạn có thể tải PDF ngay.", "Your e-ticket email is queued. You can download the PDF now.")
            : T("Chưa thể gửi email cho đặt chỗ này.", "Email could not be sent for this booking.");
        TempData["TicketEmailTone"] = queued ? "info" : "danger";

        if (!string.IsNullOrWhiteSpace(quayLai) && Url.IsLocalUrl(quayLai))
        {
            return LocalRedirect(quayLai);
        }

        return User.Identity?.IsAuthenticated == true
            ? RedirectToAction(nameof(Index), new { pnr = booking.MaDatVe })
            : Redirect($"/dat-ve/thanh-cong?pnr={Uri.EscapeDataString(booking.MaDatVe)}");
    }
>>>>>>> Stashed changes

    [HttpGet("/ve-cua-toi/chuyen-doi-gio")]
    public IActionResult ScheduleChanged() => View(DemoCatalog.ScheduleChanged());

    [HttpGet("/ve-cua-toi/chuyen-huy")]
    public IActionResult Cancelled() => View(DemoCatalog.Cancelled());

    [HttpPost("/ve-cua-toi/bien-dong/{id:int}/chap-nhan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptSchedule(int id, string? pnr, CancellationToken cancellationToken)
    {
        try
        {
            await disruptions.AcceptAsync(id, ScopedBookings(), User.DisplayName(), cancellationToken);
            TempData["TicketMsg"] = T("Đã chấp nhận lịch bay mới. Bạn có thể tải vé khi mọi chặng đã được xử lý.", "New schedule accepted. You can download your ticket once all affected flights are resolved.");
        }
        catch (BookingValidationException error)
        {
            TempData["TicketMsg"] = CustomerErrorText(error.Message);
        }
        return RedirectToAction(nameof(Index), new { pnr });
    }

    [HttpGet("/ve-cua-toi/yeu-cau-doi")]
<<<<<<< Updated upstream
    public IActionResult ChangeRequest() => View(DemoCatalog.ChangeRequest());

    [HttpPost("/ve-cua-toi/yeu-cau-doi")]
    [ValidateAntiForgeryToken]
    public IActionResult ChangeRequest(IFormCollection form) => RedirectToAction(nameof(Requests));

    [HttpGet("/ve-cua-toi/yeu-cau-hoan")]
    public IActionResult RefundRequest() => View(DemoCatalog.RefundRequest());

    [HttpPost("/ve-cua-toi/yeu-cau-hoan")]
    [ValidateAntiForgeryToken]
    public IActionResult RefundRequest(IFormCollection form) => RedirectToAction(nameof(Requests));

    [HttpGet("/ve-cua-toi/yeu-cau")]
    public IActionResult Requests() => View(DemoCatalog.Requests());
=======
    public async Task<IActionResult> ChangeRequest(string? pnr, int? newFlightId, CancellationToken cancellationToken)
    {
        var booking = await FindBookingAsync(pnr, cancellationToken);
        if (booking is null) return RedirectToAction(nameof(Index));
        if (FlightDisruptionService.NeedsAttention(booking)) return DisruptedTicket(booking);
        if (JourneyBookingBuilder.UsesJourney(booking)) return GroupChangeUnavailable(booking);
        if (!TicketAfterSalesService.SupportsDemoChange(booking)
            || !TicketRules.CanRequestChangeOrRefund(booking, DateTimeOffset.UtcNow))
            return TicketMessage(booking, T("Đổi trực tuyến dành cho vé Phổ thông một chiều, một khách đã thanh toán mô phỏng và còn ít nhất 3 giờ trước khi bay.", "Online changes are available for a one-way Economy booking for one passenger paid through the demo, at least 3 hours before departure."));

        var ticket = booking.Ves.Single();
        var flight = ticket.ChuyenBay;
        var candidates = await afterSales.ChangeCandidates(flight).AsNoTracking()
            .Include(c => c.TuyenBay).ThenInclude(t => t.SanBayDi)
            .Include(c => c.TuyenBay).ThenInclude(t => t.SanBayDen)
            .Include(c => c.MayBay)
            .OrderBy(c => c.GioKhoiHanh).ThenBy(c => c.Id).Take(20).ToListAsync(cancellationToken);
        var selected = candidates.FirstOrDefault(c => c.Id == newFlightId) ?? candidates.FirstOrDefault();
        var policy = await cauHinhHeThong.LayChinhSachAsync(cancellationToken);
        return View(new ChangeRequestViewModel
        {
            Booking = ToBookingViewModel(booking, ticket, flight, policy),
            Candidates = candidates.Select(c => ToOffer(c, ticket, policy)).ToList(),
            NewFlight = selected is null ? null : ToOffer(selected, ticket, policy),
            CurrentFare = ticket.GiaVe,
            NewBaseFare = selected?.GiaCoBan ?? ticket.GiaVe,
            SeatBaggageText = $"{ticket.SoGhe ?? T("Chưa chọn ghế", "No seat selected")} · {T(FlightMapping.CabinBaggage, "12 kg carry-on")} · {T(FlightMapping.CheckedBaggage, "23 kg checked")}"
        });
    }

    [HttpPost("/ve-cua-toi/yeu-cau-doi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRequest(IFormCollection form, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await BookingCheckoutService.LockAsync(dbContext, cancellationToken);
        var booking = await FindBookingAsync(form["pnr"], cancellationToken);
        if (booking is null) return RedirectToAction(nameof(Index));
        if (FlightDisruptionService.NeedsAttention(booking)) return DisruptedTicket(booking);
        if (JourneyBookingBuilder.UsesJourney(booking)) return GroupChangeUnavailable(booking);
        if (!TicketAfterSalesService.SupportsDemoChange(booking)
            || !TicketRules.CanRequestChangeOrRefund(booking, DateTimeOffset.UtcNow))
            return TicketMessage(booking, T("Đặt chỗ chưa đủ điều kiện đổi vé trực tuyến trong bản mô phỏng.", "This booking is ineligible for an online change in the demo."));

        var open = await OpenRequestCodeAsync(booking.Id, cancellationToken);
        if (open is not null) return TrackRequest(open, T("Đặt chỗ đang có một yêu cầu cần xử lý.", "This booking already has a pending request."));
        if (!int.TryParse(form["newFlightId"], out var newFlightId)
            || !await afterSales.ChangeCandidates(booking.Ves.Single().ChuyenBay)
                .AnyAsync(c => c.Id == newFlightId, cancellationToken))
            return TicketMessage(booking, T("Chuyến thay thế phải cùng tuyến, còn mở bán và còn ít nhất 3 giờ trước khi bay.", "The replacement flight must be on the same route, available for booking, and at least 3 hours from departure."));

        var request = CreateRequest(booking, form, LoaiYeuCau.DoiVe);
        request.PhiDoi = (await cauHinhHeThong.LayChinhSachAsync(cancellationToken)).ChangeFee;
        request.ChuyenBayMoiId = newFlightId;
        dbContext.YeuCauDoiHoans.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TrackRequest(request.MaYeuCau, T("Đã gửi yêu cầu. Sau khi nhân viên duyệt, bạn có thể xem và xác nhận báo giá tại đây.", "Request submitted. You can review and confirm your quote here after staff approval."));
    }

    [HttpPost("/ve-cua-toi/yeu-cau-doi/{code}/xac-nhan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmChange(string code, string? expectedCharge, bool accepted, CancellationToken cancellationToken)
    {
        if (!accepted || !decimal.TryParse(expectedCharge, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var charge))
            return TrackRequest(code, T("Vui lòng xác nhận khoản thanh toán mô phỏng theo báo giá.", "Confirm the simulated payment amount on the quote."));
        try
        {
            await afterSales.CompleteChangeAsync(code, ScopedBookings(), charge, User.DisplayName(), cancellationToken);
            return TrackRequest(code, T("Đã đổi chuyến và phát hành vé mới. Bạn có thể tải vé cập nhật.", "Your flight was changed and a new ticket was issued. You can now download the updated ticket."));
        }
        catch (BookingValidationException error)
        {
            return TrackRequest(code, CustomerErrorText(error.Message));
        }
    }

    [HttpPost("/ve-cua-toi/yeu-cau-doi/{code}/huy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelChange(string code, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await BookingCheckoutService.LockAsync(dbContext, cancellationToken);
        var owned = ScopedBookings();
        var request = await dbContext.YeuCauDoiHoans.SingleOrDefaultAsync(x => x.MaYeuCau == code
            && x.LoaiYeuCau == LoaiYeuCau.DoiVe
            && (x.TrangThai == TrangThaiYeuCau.ChoDuyet || x.TrangThai == TrangThaiYeuCau.DaDuyet)
            && owned.Any(b => b.Id == x.DatVeId), cancellationToken);
        if (request is null) return TrackRequest(code, T("Yêu cầu không còn có thể hủy.", "This request can no longer be cancelled."));

        request.TrangThai = TrangThaiYeuCau.TuChoi;
        request.NgayXuLy = DateTimeOffset.UtcNow;
        request.GhiChuXuLy = "Khách đã hủy yêu cầu đổi. Vé hiện tại vẫn còn hiệu lực.";
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TrackRequest(code, request.GhiChuXuLy);
    }

    [HttpGet("/ve-cua-toi/yeu-cau-hoan")]
    public async Task<IActionResult> RefundRequest(string? pnr, CancellationToken cancellationToken)
    {
        var booking = await FindBookingAsync(pnr, cancellationToken);
        if (booking == null) return RedirectToAction(nameof(Index));
        if (FlightDisruptionService.NeedsAttention(booking)) return DisruptedTicket(booking);
        var ve = booking.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var f = ve?.ChuyenBay;
        if (f == null) return RedirectToAction(nameof(Index));

        var model = new RefundRequestViewModel
        {
            Booking = ToBookingViewModel(booking, ve, f,
                await cauHinhHeThong.LayChinhSachAsync(cancellationToken)),
            SeatBaggageText = $"{(string.IsNullOrWhiteSpace(ve?.SoGhe) ? T("Chưa chọn ghế", "No seat selected") : ve!.SoGhe)} · {T(FlightMapping.CabinBaggage, "12 kg carry-on")} · {T(FlightMapping.CheckedBaggage, "23 kg checked")}",
            RefundToText = RefundDestinationText(RefundMethod(booking)),
            DeadlineText = T(
                $"Yêu cầu cần gửi trước giờ khởi hành theo điều kiện vé. Giờ khởi hành: {FlightMapping.ToVietnamTime(f.GioKhoiHanh):dd/MM/yyyy HH:mm} (UTC+7).",
                $"Submit your request before departure, as required by the fare conditions. Your flight departs at {FlightMapping.ToVietnamTime(f.GioKhoiHanh):dd/MM/yyyy HH:mm} (UTC+7)."),
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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await BookingCheckoutService.LockAsync(dbContext, cancellationToken);
        var booking = await FindBookingAsync(form["pnr"], cancellationToken);
        if (booking is null) return RedirectToAction(nameof(Index));
        if (FlightDisruptionService.NeedsAttention(booking)) return DisruptedTicket(booking);
        if (!TicketRules.CanRequestChangeOrRefund(booking, DateTimeOffset.UtcNow))
            return TicketMessage(booking, T("Vé phải được xác nhận và yêu cầu hoàn cần gửi trước giờ bay ít nhất 3 tiếng.", "Refunds require a confirmed ticket and a request at least 3 hours before departure."));

        var open = await OpenRequestCodeAsync(booking.Id, cancellationToken);
        if (open is not null) return TrackRequest(open, T("Đặt chỗ đang có một yêu cầu cần xử lý.", "This booking already has a pending request."));
        var request = CreateRequest(booking, form, LoaiYeuCau.HoanVe);
        request.PhiHoan = (await cauHinhHeThong.LayChinhSachAsync(cancellationToken)).RefundFee;
        request.SoTienHoan = TicketAfterSalesService.RefundAmount(booking, request);
        if (request.SoTienHoan <= 0)
            return TicketMessage(booking, T("Đơn không có khoản tiền có thể hoàn sau khi trừ phí.", "No refundable balance remains after fees."));
        request.PhuongThucHoan = RefundMethod(booking);
        dbContext.YeuCauDoiHoans.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TrackRequest(request.MaYeuCau, T("Đã gửi yêu cầu hoàn vé. Bạn có thể theo dõi kết quả tại đây.", "Refund request submitted. You can track its status here."));
    }

    private Task<string?> OpenRequestCodeAsync(int bookingId, CancellationToken ct) =>
        dbContext.YeuCauDoiHoans.Where(r => r.DatVeId == bookingId
            && (r.TrangThai == TrangThaiYeuCau.ChoDuyet || r.TrangThai == TrangThaiYeuCau.DaDuyet))
            .OrderByDescending(r => r.NgayTao).Select(r => r.MaYeuCau).FirstOrDefaultAsync(ct);

    private static YeuCauDoiHoan CreateRequest(DatVe booking, IFormCollection form, LoaiYeuCau type)
    {
        var change = type == LoaiYeuCau.DoiVe;
        var reason = form["reason"].ToString().Trim();
        var note = form["note"].ToString().Trim();
        return new YeuCauDoiHoan
        {
            MaYeuCau = $"{(change ? "CH" : "RF")}-{Guid.NewGuid().ToString("N")[..16]}",
            DatVeId = booking.Id,
            LoaiYeuCau = type,
            LyDo = reason.Length == 0 ? (change ? "Yêu cầu đổi vé" : "Yêu cầu hoàn vé") : reason[..Math.Min(reason.Length, 300)],
            ThongTinBoSung = note[..Math.Min(note.Length, 1000)],
            TrangThai = TrangThaiYeuCau.ChoDuyet,
            PhiDichVuKhongHoan = Math.Max(0m, booking.PhiDichVu - booking.GiamGia),
            PhiHanhLyKhongHoan = booking.PhiHanhLy,
            NgayTao = DateTimeOffset.UtcNow
        };
    }

    private IActionResult TicketMessage(DatVe booking, string message)
    {
        TempData["TicketMsg"] = message;
        return RedirectToAction(nameof(Index), new { pnr = booking.MaDatVe });
    }

    private IActionResult DisruptedTicket(DatVe booking) =>
        TicketMessage(booking, T("Đặt chỗ đang chờ hỗ trợ do thay đổi lịch bay. Xem chặng bị ảnh hưởng trong Vé của tôi.", "This booking requires assistance after a schedule change. See the affected flights in My tickets."));

    private IActionResult TrackRequest(string code, string message)
    {
        TempData["TicketMsg"] = message;
        return RedirectToAction(nameof(Requests), new { id = code });
    }

    [HttpGet("/ve-cua-toi/yeu-cau")]
    public async Task<IActionResult> Requests(string? id, string? tab, [FromQuery(Name = "trang")] int? page,
        CancellationToken cancellationToken)
    {
        const int pageSize = 30;
        var owned = ScopedBookings();
        var requests = dbContext.YeuCauDoiHoans.AsNoTracking()
            .Where(r => owned.Any(b => b.Id == r.DatVeId));
        var counts = await requests.GroupBy(r => r.TrangThai)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
        var inProgress = counts.GetValueOrDefault(TrangThaiYeuCau.ChoDuyet) + counts.GetValueOrDefault(TrangThaiYeuCau.DaDuyet);
        var completed = counts.GetValueOrDefault(TrangThaiYeuCau.HoanTat) + counts.GetValueOrDefault(TrangThaiYeuCau.TuChoi);
        var total = counts.Values.Sum();
        tab = tab switch { "processing" => "processing", "done" => "done", _ => null };

        var filtered = tab switch
        {
            "processing" => requests.Where(r => r.TrangThai == TrangThaiYeuCau.ChoDuyet || r.TrangThai == TrangThaiYeuCau.DaDuyet),
            "done" => requests.Where(r => r.TrangThai == TrangThaiYeuCau.HoanTat || r.TrangThai == TrangThaiYeuCau.TuChoi),
            _ => requests
        };
        var filteredCount = tab switch { "processing" => inProgress, "done" => completed, _ => total };
        var pageCount = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)pageSize));
        var pageNumber = Math.Clamp(page ?? 1, 1, pageCount);
        var query = filtered.AsSplitQuery()
            .Include(r => r.DatVe).ThenInclude(d => d.KhachHang)
            .Include(r => r.DatVe).ThenInclude(d => d.Ves).ThenInclude(v => v.ChuyenBay).ThenInclude(c => c.TuyenBay).ThenInclude(t => t.SanBayDi)
            .Include(r => r.DatVe).ThenInclude(d => d.Ves).ThenInclude(v => v.ChuyenBay).ThenInclude(c => c.TuyenBay).ThenInclude(t => t.SanBayDen)
            .Include(r => r.ChuyenBayMoi)
            .Include(r => r.DatVe).ThenInclude(d => d.Ves).ThenInclude(v => v.HanhKhach);
        var list = await query.OrderByDescending(r => r.NgayTao).ThenByDescending(r => r.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var tabs = new[]
        {
            new TicketTab(T("Tất cả", "All"), total, tab is null, Url.Action(nameof(Requests))),
            new TicketTab(T("Đang xử lý", "In progress"), inProgress, tab == "processing", Url.Action(nameof(Requests), new { tab = "processing" })),
            new TicketTab(T("Đã hoàn tất", "Completed"), completed, tab == "done", Url.Action(nameof(Requests), new { tab = "done" }))
        };
        var updatedAt = FlightMapping.ToVietnamTime(DateTimeOffset.UtcNow).ToString("dd/MM/yyyy");
        if (list.Count == 0)
            return View(new RequestTrackingViewModel { Tabs = tabs, ActiveTab = tab, UpdatedAtText = updatedAt });

        var selected = string.IsNullOrWhiteSpace(id) ? null : list.FirstOrDefault(r => r.MaYeuCau == id)
            ?? await query.SingleOrDefaultAsync(r => r.MaYeuCau == id, cancellationToken);
        var sel = selected ?? list[0];
        var rows = list.Select(r =>
        {
            var ve = r.DatVe.Ves.OrderBy(v => v.Id).FirstOrDefault();
            var route = ve is null ? r.DatVe.MaDatVe
                : $"{ve.ChuyenBay.SoHieu} · {ve.ChuyenBay.TuyenBay.SanBayDi.MaSanBay} → {ve.ChuyenBay.TuyenBay.SanBayDen.MaSanBay}";
            return new RequestRow(r.MaYeuCau, r.DatVe.MaDatVe,
                $"{(r.LoaiYeuCau == LoaiYeuCau.HoanVe ? T("Hoàn vé", "Refund") : T("Đổi vé", "Change"))} · {FlightMapping.ToVietnamTime(r.NgayTao):dd/MM/yyyy}",
                route, ReqStatusText(r.TrangThai), ReqTone(r.TrangThai), r.Id == sel.Id);
        }).ToList();

        var selVe = sel.DatVe?.Ves.FirstOrDefault();
        var selFlight = selVe?.ChuyenBay;
        var change = sel.LoaiYeuCau == LoaiYeuCau.DoiVe;
        var approved = sel.TrangThai == TrangThaiYeuCau.DaDuyet;

        return View(new RequestTrackingViewModel
        {
            Tabs = tabs,
            ActiveTab = tab,
            PageNumber = pageNumber,
            PageCount = pageCount,
            Requests = rows,
            UpdatedAtText = updatedAt,
            DetailCode = sel.MaYeuCau,
            DetailPnr = sel.DatVe?.MaDatVe ?? "—",
            DetailPassenger = selVe?.HanhKhach?.HoTen ?? sel.DatVe?.KhachHang?.HoTen ?? "—",
            DetailTicketNumber = selVe?.MaVe ?? "—",
            DetailStatusText = ReqStatusText(sel.TrangThai),
            DetailStatusTone = ReqTone(sel.TrangThai),
            FlightText = selFlight == null ? "—" : $"{selFlight.SoHieu} · {selFlight.GioKhoiHanh.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy HH:mm}",
            ReasonText = sel.LyDo switch
            {
                "Thay đổi kế hoạch cá nhân" => T("Thay đổi kế hoạch cá nhân", "Personal plans changed"),
                "Công tác thay đổi" => T("Công tác thay đổi", "Work plans changed"),
                "Thay đổi hành trình" => T("Thay đổi hành trình", "Itinerary changed"),
                "Lý do khác" => T("Lý do khác", "Other"),
                _ => sel.LyDo
            },
            Events = BuildRequestEvents(sel),
            IsChange = change,
            IsCompleted = sel.TrangThai == TrangThaiYeuCau.HoanTat,
            ChangeTargetText = sel.ChuyenBayMoi is { } target
                ? $"{target.SoHieu} · {FlightMapping.ToVietnamTime(target.GioKhoiHanh):dd/MM/yyyy HH:mm}"
                : null,
            ChangeCharge = change && approved && sel.GiaVeMoi is decimal newFare && selVe is not null
                ? TicketAfterSalesService.ExtraCharge(selVe.GiaVe, newFare, sel.PhiDoi)
                : null,
            CanCancelChange = change && (approved || sel.TrangThai == TrangThaiYeuCau.ChoDuyet),
            PaidTotal = sel.DatVe?.TongTien ?? 0m,
            RefundFee = sel.PhiHoan,
            ChangeFeePaid = sel.DatVe?.PhiDoiDaThu ?? 0m,
            ServiceFee = sel.PhiDichVuKhongHoan,
            BaggageFee = sel.PhiHanhLyKhongHoan,
            RefundTotal = sel.SoTienHoan,
            RefundToText = RefundDestinationText(sel.PhuongThucHoan ?? "Nguồn thanh toán gốc")
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
        if (IsCancelled(b) || FlightDisruptionService.NeedsAttention(b)) return false;
        return b.Ves.Count != 0 && b.Ves.All(v => v.ChuyenBay.GioHaCanh < DateTimeOffset.UtcNow);
    }

    private static bool IsUpcoming(DatVe b) => !IsCancelled(b) && !IsDone(b);

    private static TicketListItem ToListItem(DatVe b, bool selected, int activeTab)
    {
        var ve = b.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var f = ve?.ChuyenBay;
        var dep = f is null ? (DateTimeOffset?)null : FlightMapping.ToVietnamTime(f.GioKhoiHanh);
        var route = f is null ? b.MaDatVe : $"{f.TuyenBay.SanBayDi.ThanhPho} → {f.TuyenBay.SanBayDen.ThanhPho}";
        var schedule = dep is null ? "—" : $"{dep:dd/MM/yyyy} · {dep:HH:mm} · {f!.SoHieu}";
        var paxCount = Math.Max(1, b.Ves.Select(x => x.HanhKhachId).Distinct().Count());
        var (statusText, statusTone) = StatusFor(b);
        if (FlightDisruptionService.NeedsAttention(b)) (statusText, statusTone) = (T("Cần xử lý lịch bay", "Schedule support required"), "warning");
        var note = activeTab == 2
            ? (b.TrangThai == TrangThaiDatVe.HoanTien ? T("Đã hoàn tiền", "Refunded") : T("Đã hủy", "Cancelled"))
            : b.TrangThai == TrangThaiDatVe.ChoThanhToan ? T("Chờ thanh toán", "Awaiting payment") : T("Xem chi tiết", "View details");

        return new TicketListItem(
            Pnr: b.MaDatVe,
            StatusText: statusText,
            StatusTone: statusTone,
            RouteText: route,
            ScheduleText: schedule,
            PassengerText: T($"{paxCount} hành khách", paxCount == 1 ? "1 passenger" : $"{paxCount} passengers"),
            PriceText: Money.Vnd(b.TongTien),
            NoteText: note,
            NoteTone: statusTone,
            IsSelected: selected,
            ActionText: T("Xem đặt chỗ", "View booking"));
    }

    private static (string text, string tone) StatusFor(DatVe b) => b.TrangThai switch
    {
        TrangThaiDatVe.DaXacNhan => (T("Đã xác nhận", "Confirmed"), "success"),
        TrangThaiDatVe.ChoThanhToan => (T("Chờ thanh toán", "Awaiting payment"), "warning"),
        TrangThaiDatVe.DaHuy => (T("Đã hủy", "Cancelled"), "danger"),
        TrangThaiDatVe.HoanTien => (T("Hoàn tiền", "Refunded"), "info"),
        TrangThaiDatVe.HetHan => (T("Hết hạn", "Expired"), "danger"),
        _ => (b.TrangThai.ToString(), "info")
    };

    private static TicketDetailViewModel ToDetail(DatVe b, ChinhSachDatVe chinhSach)
    {
        var ve = b.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var f = ve?.ChuyenBay;
        var flight = f is null ? EmptyFlight() : ToOffer(f, ve!, chinhSach);
        var paid = b.ThanhToans.Where(t => t.TrangThai == TrangThaiThanhToan.ThanhCong
            && !VnpayBookingService.IsRefundOnly(t)).Sum(t => t.SoTien);
        var paymentText = paid > 0
            ? T($"Đã thanh toán · {Money.Vnd(paid)}", $"Paid · {Money.Vnd(paid)}")
            : T("Chưa thanh toán", "Unpaid");
        var disrupted = FlightDisruptionService.NeedsAttention(b);
        var canAccept = disrupted && !FlightDisruptionService.HasRefundInProgress(b);

        return new TicketDetailViewModel
        {
            Journey = JourneyBookingBuilder.UsesJourney(b) ? JourneyBookingBuilder.FromBooking(b) : null,
            Flight = flight,
            Pnr = b.MaDatVe,
            PassengerName = ve?.HanhKhach?.HoTen ?? b.KhachHang?.HoTen ?? T("Hành khách", "Passenger"),
            SeatCode = string.IsNullOrWhiteSpace(ve?.SoGhe) ? T("Chưa chọn", "Not selected") : ve.SoGhe,
            TicketNumber = string.IsNullOrWhiteSpace(ve?.MaVe) ? T("Chưa phát hành", "Not issued") : ve.MaVe,
            BaggageText = $"{T(FlightMapping.CabinBaggage, "12 kg carry-on")} · {T(FlightMapping.CheckedBaggage, "23 kg checked")}",
            PaymentText = paymentText,
            BookedAtText = FlightMapping.ToVietnamTime(b.NgayDat).ToString("dd/MM/yyyy · HH:mm '(UTC+7)'"),
            ChangeFee = ve is { PhiDoi: > 0 } ? ve.PhiDoi : chinhSach.ChangeFee,
            RefundFee = ve is { PhiHoan: > 0 } ? ve.PhiHoan : chinhSach.RefundFee,
            ServiceFee = b.PhiDichVu - b.GiamGia,
            ExtraBaggageFee = 0m,
            NeedsScheduleSupport = disrupted,
            Disruptions = b.BienDongs.OrderBy(x => x.NgayXuLy != null).ThenByDescending(x => x.NgayTao).ThenByDescending(x => x.Id)
                .Take(8).Select(x => new FlightDisruptionNotice(x.Id,
                    $"{x.SoHieuCu} · {(x.BiHuy ? T("Chuyến bị hủy", "Flight cancelled") : T("Thay đổi lịch bay", "Schedule change"))}",
                    FlightDisruptionService.DateText(x.KhoiHanhCu, x.HaCanhCu),
                    x.KhoiHanhMoi is { } start && x.HaCanhMoi is { } end
                        ? $"{x.SoHieuMoi} · {FlightDisruptionService.DateText(start, end)}" : null,
                    x.KetQua, canAccept && !x.BiHuy && x.NgayXuLy is null)).ToArray(),
            CanManage = !disrupted && TicketRules.CanRequestChangeOrRefund(b, DateTimeOffset.UtcNow),
            HasIssuedTicket = !disrupted && b.TrangThai == TrangThaiDatVe.DaXacNhan
        };
    }

    private static FlightOffer ToOffer(ChuyenBay f, Ve ve, ChinhSachDatVe chinhSach)
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
            ChangeFee: chinhSach.ChangeFee,
            RefundFee: chinhSach.RefundFee);
    }

    private static FlightOffer EmptyFlight() => new(
        Id: "0", AirlineName: "—", FlightNumber: "—", Aircraft: "", FareName: "—",
        From: new AirportInfo("---", "—", "—"), To: new AirportInfo("---", "—", "—"),
        Departure: DateTimeOffset.UtcNow, Arrival: DateTimeOffset.UtcNow,
        PricePerPassenger: 0m, Stops: "—", CabinBaggage: "—", CheckedBaggage: "—",
        ChangeFee: 0m, RefundFee: 0m);

    /// <summary>
    /// Chỉ trả về đặt chỗ thuộc khách đang đăng nhập. Nhân viên không có hồ sơ khách hàng
    /// sẽ không thấy đặt chỗ nào ở khu vực "Vé của tôi".
    /// </summary>
    private async Task<DatVe?> FindBookingForTicketAsync(string pnr, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pnr)) return null;
        var code = pnr.Trim().ToUpperInvariant();

        return await dbContext.DatVes.AsNoTracking()
            .Include(x => x.KhachHang)
            .FirstOrDefaultAsync(x => x.MaDatVe == code, ct);
    }

    /// <summary>Chủ vé (theo mã khách hàng/email), nhân viên, hoặc phiên vừa đặt chỗ.</summary>
    private bool CoQuyenXemVe(DatVe booking)
    {
        if (User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager)) return true;

        var khachHangId = User.KhachHangId();
        if (khachHangId.HasValue && booking.KhachHangId == khachHangId.Value) return true;

        var email = User.FindFirstValue(ClaimTypes.Email)?.Trim();
        if (!string.IsNullOrWhiteSpace(email))
        {
            if (string.Equals(booking.KhachHang.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase)
                || string.Equals(booking.EmailLienHe?.Trim(), email, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return HttpContext.Session.GetInt32(BookingSession.BookingId) == booking.Id;
    }

    private IQueryable<DatVe> ScopedBookings()
    {
        IQueryable<DatVe> query = dbContext.DatVes;

        var khachHangId = User.KhachHangId();
        if (khachHangId.HasValue)
        {
            return query.Where(x => x.KhachHangId == khachHangId.Value);
        }

        var email = User.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(email))
        {
            return query.Where(x => x.KhachHang.Email.ToLower() == email
                || (x.EmailLienHe != null && x.EmailLienHe.ToLower() == email));
        }

        return query.Where(x => false);
    }

    private async Task<DatVe?> FindBookingAsync(string? pnr, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pnr)) return null;
        try
        {
            return await ScopedBookings().AsNoTracking().AsSplitQuery()
                .Include(x => x.KhachHang)
                .Include(x => x.BienDongs.Where(change => change.NgayXuLy == null))
                .Include(x => x.ThanhToans).ThenInclude(x => x.HoanTiens)
                .Include(x => x.Ves).ThenInclude(x => x.HanhKhach)
                .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
                .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
                .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.MayBay)
                .FirstOrDefaultAsync(x => x.MaDatVe == pnr.Trim(), ct);
        }
        catch { return null; }
    }

    private static BookingViewModel ToBookingViewModel(DatVe b, Ve? ve, ChuyenBay f, ChinhSachDatVe chinhSach)
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
            Journey = JourneyBookingBuilder.UsesJourney(b) ? JourneyBookingBuilder.FromBooking(b) : null,
            Flight = ToOffer(f, firstVe, chinhSach),
            Criteria = new FlightSearchCriteria
            {
                OriginCity = f.TuyenBay.SanBayDi.ThanhPho, OriginCode = f.TuyenBay.SanBayDi.MaSanBay,
                DestinationCity = f.TuyenBay.SanBayDen.ThanhPho, DestinationCode = f.TuyenBay.SanBayDen.MaSanBay,
                DepartDateText = dep.ToString("dd/MM/yyyy")
            },
            Price = new PriceSummary
            {
                BaseFare = b.Ves.Sum(t => t.GiaVe),
                SeatFee = Math.Max(0m, b.TongTien + b.GiamGia - b.Ves.Sum(t => t.GiaVe) - b.ThuePhi - b.PhiDichVu - b.PhiHanhLy - b.PhiDoiDaThu),
                ChangeFeePaid = b.PhiDoiDaThu,
                Taxes = b.ThuePhi, ServiceFee = b.PhiDichVu, BaggageFee = b.PhiHanhLy,
                Discount = b.GiamGia, PromotionCode = b.MaKhuyenMai
            },
            Passenger = pax,
            SeatCode = string.IsNullOrWhiteSpace(firstVe.SoGhe) ? "—" : firstVe.SoGhe!,
            Pnr = b.MaDatVe,
            TicketNumber = firstVe.MaVe ?? "—",
            TotalText = Money.Vnd(b.TongTien),
            Email = b.EmailLienHe ?? pax.Email,
            Phone = b.SoDienThoaiLienHe ?? pax.Phone
        };
    }

    private IActionResult GroupChangeUnavailable(DatVe booking)
    {
        TempData["TicketMsg"] = T("Đổi vé cho đơn nhiều hành khách, khứ hồi hoặc Thương gia cần liên hệ hỗ trợ để kiểm tra toàn bộ hành trình. Bản demo chưa hỗ trợ đổi trực tuyến cho các đơn này.", "Contact support to review the itinerary and request a change for bookings with multiple passengers, return flights, or Business tickets.");
        return RedirectToAction(nameof(Index), new { pnr = booking.MaDatVe });
    }

    private static string ReqStatusText(TrangThaiYeuCau s) => s switch { TrangThaiYeuCau.ChoDuyet => T("Chờ duyệt", "Awaiting approval"), TrangThaiYeuCau.DaDuyet => T("Đã duyệt · Chờ xử lý", "Approved · Awaiting processing"), TrangThaiYeuCau.TuChoi => T("Từ chối", "Declined"), TrangThaiYeuCau.HoanTat => T("Hoàn tất", "Completed"), _ => s.ToString() };

    private static string RefundDestinationText(string value) => IsEnglish ? value switch
    {
        "Nguồn thanh toán mô phỏng" => "the simulated payment method",
        "Nguồn thanh toán gốc" => "the original payment method",
        _ => value
    } : value;

    private static string CustomerErrorText(string message) => IsEnglish ? message switch
    {
        "Lịch mới không phù hợp với hành trình. Vui lòng liên hệ hỗ trợ." => "The new schedule does not fit your itinerary. Contact support.",
        "Thông báo không còn chờ xử lý." => "This notification is no longer pending.",
        "Nhóm tuổi của hành khách thay đổi ở ngày bay mới. Vui lòng liên hệ hỗ trợ." => "A passenger changes age category on the new travel date. Contact support.",
        "Không tìm thấy yêu cầu đổi vé." => "The change request was not found.",
        "Yêu cầu chưa có báo giá đã duyệt." => "There is no approved quote for this request.",
        "Chuyến trên vé đã thay đổi. Vui lòng liên hệ hỗ trợ." => "The flight on your ticket changed. Contact support.",
        "Số tiền cần xác nhận đã thay đổi. Vui lòng tải lại báo giá." => "The amount to confirm has changed. Reload the quote.",
        "Chuyến mới đã hết ghế tiêu chuẩn. Vé cũ vẫn còn hiệu lực." => "Standard seats on the new flight are sold out. Your original ticket is still valid.",
        "Chỗ trên chuyến mới vừa thay đổi. Vé cũ vẫn còn hiệu lực." => "Availability on the new flight has changed. Your original ticket is still valid.",
        _ => BookingUiText.Text(message)
    } : message;
    private static string ReqTone(TrangThaiYeuCau s) => s switch { TrangThaiYeuCau.ChoDuyet => "warning", TrangThaiYeuCau.DaDuyet => "info", TrangThaiYeuCau.HoanTat => "success", TrangThaiYeuCau.TuChoi => "danger", _ => "neutral" };
    private static string RefundMethod(DatVe booking)
    {
        var captures = TicketAfterSalesService.Captures(booking).ToArray();
        if (captures.Length > 0 && captures.All(TicketAfterSalesService.IsDemoCapture))
            return "Nguồn thanh toán mô phỏng";
        return captures.FirstOrDefault()?.NhaCungCap ?? "Nguồn thanh toán gốc";
    }

    private static IReadOnlyList<OutcomeEvent> BuildRequestEvents(YeuCauDoiHoan r)
    {
        var vn = TimeSpan.FromHours(7);
        var list = new List<OutcomeEvent> { new(r.NgayTao.ToOffset(vn).ToString("dd/MM/yyyy HH:mm"), T("Đã tiếp nhận", "Received"), T($"Ghi nhận yêu cầu {r.MaYeuCau}.", $"Request {r.MaYeuCau} received.")) };
        if (r.TrangThai != TrangThaiYeuCau.ChoDuyet && r.NgayXuLy.HasValue)
            list.Add(new(r.NgayXuLy.Value.ToOffset(vn).ToString("dd/MM/yyyy HH:mm"), ReqStatusText(r.TrangThai), string.IsNullOrWhiteSpace(r.GhiChuXuLy) ? T("Yêu cầu đã được cập nhật.", "The request was updated.") : r.GhiChuXuLy!));
        else if (r.TrangThai == TrangThaiYeuCau.ChoDuyet)
            list.Add(new(T("Chưa có", "Pending"), T("Đang chờ xử lý", "Awaiting processing"), T("Bộ phận vận hành sẽ kiểm tra điều kiện và phản hồi.", "Our staff will review the conditions and respond.")));
        return list;
    }
>>>>>>> Stashed changes
}
