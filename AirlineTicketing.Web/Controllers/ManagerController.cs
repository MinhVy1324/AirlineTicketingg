<<<<<<< Updated upstream
using AirlineTicketing.Web.Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

[Authorize(Roles = "Admin,NhanVien")]
public class ManagerController : Controller
{
    [HttpGet("/quan-ly")]
    public IActionResult Index() => RedirectToAction("Index", "Dashboard");

    [HttpGet("/quan-ly/lich-bay")]
    public IActionResult Schedules([FromQuery(Name = "hop-thoai")] string? hopThoai) =>
        View(DemoCatalog.ManagerSchedules(hopThoai));

    [HttpPost("/quan-ly/lich-bay")]
    [ValidateAntiForgeryToken]
    public IActionResult Schedules(IFormCollection form) => RedirectToAction(nameof(Schedules));

    [HttpPost("/quan-ly/lich-bay/huy-chuyen")]
    [ValidateAntiForgeryToken]
    public IActionResult CancelFlight(IFormCollection form) => RedirectToAction(nameof(Schedules));
=======
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.Infrastructure.Payments;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static AirlineTicketing.Web.Infrastructure.UiLanguage;

namespace AirlineTicketing.Web.Controllers;

[Authorize(Policy = "StaffOnly")]
public class ManagerController(
    AppDbContext dbContext,
    DatVeBUS datVeBUS,
    TicketAfterSalesService afterSales,
    FlightDisruptionService disruptions,
    ThongKeBUS thongKe) : Controller
{
    private const string AllText = "Tất cả";
    private const string AllAirlines = "Tất cả hãng";
    private const string AllRoutes = "Tất cả tuyến";

    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    private static readonly string[] StatusChoices = ["Mở bán", "Đang bay", "Hoàn tất", "Đã hủy"];

    [HttpGet("/quan-ly")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var nowVn = now.ToOffset(Vn);
        var dayStart = new DateTimeOffset(nowVn.Year, nowVn.Month, nowVn.Day, 0, 0, 0, Vn);
        var dayEnd = dayStart.AddDays(1);
        var dayStartUtc = dayStart.ToUniversalTime();
        var dayEndUtc = dayEnd.ToUniversalTime();

        var todayFlights = await dbContext.ChuyenBays.AsNoTracking()
            .Include(x => x.MayBay)
            .Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .Where(x => x.GioKhoiHanh >= dayStartUtc && x.GioKhoiHanh < dayEndUtc)
            .OrderBy(x => x.GioKhoiHanh)
            .ToListAsync(cancellationToken);

        var usage = await LoadSeatUsageAsync(todayFlights.Select(x => x.Id), cancellationToken);

        var activeFlights = todayFlights.Where(x => x.TrangThai != TrangThaiChuyenBay.BiHuy).ToList();
        var canceledFlights = todayFlights.Count(x => x.TrangThai == TrangThaiChuyenBay.BiHuy);
        var completedFlights = activeFlights.Count(x =>
            x.TrangThai == TrangThaiChuyenBay.HoanTat || x.GioHaCanh <= now);

        var capacity = activeFlights.Sum(x => x.MayBay?.SoGhe ?? 0);
        var soldSeats = 0;
        var heldSeats = 0;
        foreach (var flight in activeFlights)
        {
            var (daBan, dangGiu) = usage.GetValueOrDefault(flight.Id);
            soldSeats += daBan;
            heldSeats += dangGiu;
        }

        var occupiedSeats = soldSeats + heldSeats;
        var loadFactor = capacity <= 0 ? 0m : occupiedSeats * 100m / capacity;

        var holds = await dbContext.DatVes.AsNoTracking()
            .Include(x => x.KhachHang)
            .Where(x => x.TrangThai == TrangThaiDatVe.ChoThanhToan)
            .OrderBy(x => x.HetHanGiuCho)
            .ToListAsync(cancellationToken);
        var expiringSoon = holds.Count(x => x.HetHanGiuCho > now && x.HetHanGiuCho <= now.AddMinutes(10));
        var expiredHolds = holds.Count(x => x.HetHanGiuCho <= now);

        var pendingRequestCounts = await dbContext.YeuCauDoiHoans.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiYeuCau.ChoDuyet)
            .GroupBy(x => x.LoaiYeuCau)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var pendingChanges = pendingRequestCounts.FirstOrDefault(x => x.Key == LoaiYeuCau.DoiVe)?.Count ?? 0;
        var pendingRefunds = pendingRequestCounts.FirstOrDefault(x => x.Key == LoaiYeuCau.HoanVe)?.Count ?? 0;
        var pendingRequests = pendingChanges + pendingRefunds;

        var pendingTransfers = await dbContext.ThanhToans.AsNoTracking()
            .CountAsync(x => x.TrangThai == TrangThaiThanhToan.ChoXuLy, cancellationToken);

        var attentionFlights = activeFlights
            .Select(flight =>
            {
                var (daBan, dangGiu) = usage.GetValueOrDefault(flight.Id);
                var flightCapacity = flight.MayBay?.SoGhe ?? 0;
                return (Flight: flight, DaBan: daBan, DangGiu: dangGiu, Remaining: flightCapacity - daBan - dangGiu);
            })
            .Where(x => x.Remaining <= 10 || (x.Flight.GioKhoiHanh <= now && x.Flight.GioHaCanh > now))
            .Take(5)
            .ToList();

        var attentionNotes = new List<string>();
        if (canceledFlights > 0)
        {
            attentionNotes.Add(T($"{canceledFlights} chuyến đã hủy, cần thông báo hành khách.", $"{canceledFlights} flights were cancelled. Notify the affected passengers."));
        }

        foreach (var item in attentionFlights.Where(x => x.Remaining <= 10 && x.Flight.GioKhoiHanh > now).Take(2))
        {
            attentionNotes.Add(T($"{item.Flight.SoHieu}: còn {Math.Max(0, item.Remaining)} ghế.", $"{item.Flight.SoHieu}: {Math.Max(0, item.Remaining)} seats remaining."));
        }

        foreach (var item in attentionFlights.Where(x =>
                     x.Flight.GioKhoiHanh < now
                     && x.Flight.TrangThai == TrangThaiChuyenBay.DuKien).Take(2))
        {
            attentionNotes.Add(T($"{item.Flight.SoHieu}: đã qua giờ khởi hành nhưng chưa cập nhật trạng thái.", $"{item.Flight.SoHieu}: departure time has passed and status needs an update."));
        }

        var rows = attentionFlights
            .Select(item => ToOverviewFlightRow(item.Flight, item.DaBan, item.DangGiu, now))
            .Concat(todayFlights
                .Where(x => x.TrangThai == TrangThaiChuyenBay.BiHuy)
                .Take(2)
                .Select(x => ToOverviewFlightRow(x, 0, 0, now)))
            .Take(5)
            .ToArray();

        var changePercent = pendingRequests == 0
            ? 0
            : (int)Math.Round(pendingChanges * 100m / pendingRequests, MidpointRounding.AwayFromZero);
        var refundPercent = pendingRequests == 0
            ? 0
            : 100 - changePercent;

        var shift = nowVn.Hour switch
        {
            >= 6 and < 14 => T("Ca sáng · 06:00–14:00", "Morning shift · 06:00–14:00"),
            >= 14 and < 22 => T("Ca chiều · 14:00–22:00", "Afternoon shift · 14:00–22:00"),
            _ => T("Ca đêm · 22:00–06:00", "Night shift · 22:00–06:00")
        };

        var model = new ManagerOverviewViewModel
        {
            Subtitle = T($"{Money.LongDate(nowVn)} · Theo dõi chuyến bay, đặt chỗ và yêu cầu trong ngày.",
                $"{nowVn.DayOfWeek}, {Money.Date(nowVn)} · Monitor today's flights, bookings and requests."),
            ShiftText = shift,
            UpdatedText = T($"Cập nhật lúc {nowVn:HH:mm} · Giờ Việt Nam (UTC+7)", $"Updated at {nowVn:HH:mm} · Vietnam time (UTC+7)"),
            Kpis = new[]
            {
                new ManagerKpi(
                    T("Chuyến bay hôm nay", "Today's flights"),
                    activeFlights.Count.ToString("N0", ViCulture),
                    T($"{completedFlights} hoàn tất · {activeFlights.Count - completedFlights} chưa khởi hành", $"{completedFlights} completed · {activeFlights.Count - completedFlights} not departed") +
                    (canceledFlights > 0 ? T($" · {canceledFlights} đã hủy", $" · {canceledFlights} cancelled") : string.Empty),
                    "info",
                    "plane"),
                new ManagerKpi(
                    T("Đặt chỗ chờ thanh toán", "Bookings awaiting payment"),
                    holds.Count.ToString("N0", ViCulture),
                    expiringSoon > 0
                        ? T($"{expiringSoon} đơn hết hạn trong 10 phút", $"{expiringSoon} bookings expire within 10 minutes")
                        : expiredHolds > 0
                            ? T($"{expiredHolds} đơn đã quá hạn giữ chỗ", $"{expiredHolds} booking holds have expired")
                            : T("Chưa có đơn sắp hết hạn", "No holds expiring soon"),
                    expiringSoon > 0 || expiredHolds > 0 ? "warning" : "success",
                    "clock"),
                new ManagerKpi(
                    T("Tỷ lệ lấp đầy hôm nay", "Today's load factor"),
                    PercentText(loadFactor),
                    T($"{occupiedSeats.ToString("N0", ViCulture)} / {capacity.ToString("N0", ViCulture)} ghế đã bán hoặc đang giữ",
                        $"{occupiedSeats.ToString("N0", ViCulture)} / {capacity.ToString("N0", ViCulture)} seats sold or held"),
                    "success",
                    "users"),
                new ManagerKpi(
                    T("Yêu cầu cần xử lý", "Requests to process"),
                    pendingRequests.ToString("N0", ViCulture),
                    T($"{pendingChanges} đổi vé · {pendingRefunds} hoàn vé", $"{pendingChanges} changes · {pendingRefunds} refunds"),
                    pendingRequests > 0 ? "warning" : "success",
                    "inbox")
            },
            AttentionTitle = attentionNotes.Count > 0
                ? T($"{attentionNotes.Count} điểm cần chú ý trong ca trực", $"{attentionNotes.Count} items need attention this shift")
                : T("Không có điểm cần chú ý trong ca trực", "No items need attention this shift"),
            AttentionText = attentionNotes.Count > 0
                ? string.Join(" ", attentionNotes)
                : T("Các chuyến trong ngày đang đúng lịch. Tiếp tục theo dõi đặt chỗ chờ thanh toán và yêu cầu đổi / hoàn.", "Today's flights are on schedule. Monitor pending payments and change or refund requests."),
            FlightsSubtitle = T($"Lịch khai thác ngày {dayStart:dd/MM/yyyy}", $"Operations schedule for {dayStart:dd/MM/yyyy}"),
            Flights = rows,
            Workload = new[]
            {
                new ManagerWorkloadRow(
                    T("Đổi vé chờ duyệt", "Changes awaiting approval"),
                    T($"{pendingChanges} yêu cầu", $"{pendingChanges} requests"),
                    changePercent,
                    pendingChanges > 0 ? "warning" : "neutral"),
                new ManagerWorkloadRow(
                    T("Hoàn vé chờ duyệt", "Refunds awaiting approval"),
                    T($"{pendingRefunds} yêu cầu", $"{pendingRefunds} requests"),
                    refundPercent,
                    pendingRefunds > 0 ? "success" : "neutral")
            },
            WorkloadDetailLabel = T("Chờ đối soát thanh toán", "Payments awaiting reconciliation"),
            WorkloadDetailValue = T($"{pendingTransfers} giao dịch", $"{pendingTransfers} transactions"),
            Holds = holds.Take(5).Select(hold => new ManagerHoldRow(
                hold.MaDatVe,
                hold.KhachHang.HoTen,
                Money.Vnd(hold.TongTien),
                HoldExpiryText(hold.HetHanGiuCho, nowVn))).ToArray(),
            Checklist = new[]
            {
                new ManagerChecklistItem(
                    pendingTransfers == 0
                        ? T("Đối soát thanh toán ca trước", "Reconcile payments from the previous shift")
                        : T($"Đối soát thanh toán ca trước (còn {pendingTransfers} giao dịch)", $"Reconcile payments from the previous shift ({pendingTransfers} remaining)"),
                    pendingTransfers == 0),
                new ManagerChecklistItem(T("Kiểm tra lịch khai thác hôm nay", "Review today's flight schedule"), todayFlights.Count > 0),
                new ManagerChecklistItem(
                    pendingRequests == 0
                        ? T("Xử lý yêu cầu đổi / hoàn", "Process change and refund requests")
                        : T($"Xử lý {pendingRequests} yêu cầu đổi / hoàn", $"Process {pendingRequests} change or refund requests"),
                    pendingRequests == 0),
                new ManagerChecklistItem(
                    expiredHolds == 0
                        ? T("Không còn đặt chỗ quá hạn giữ chỗ", "All expired booking holds are resolved")
                        : T($"Xử lý {expiredHolds} đặt chỗ đã quá hạn giữ chỗ", $"Clear {expiredHolds} expired booking holds"),
                    expiredHolds == 0)
            }
        };

        return View(model);
    }

    private async Task<IReadOnlyDictionary<int, (int DaBan, int DangGiu)>> LoadSeatUsageAsync(
        IEnumerable<int> flightIds,
        CancellationToken cancellationToken)
    {
        var ids = flightIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, (int DaBan, int DangGiu)>();
        }

        var rows = await dbContext.Ves.AsNoTracking()
            .Where(x => ids.Contains(x.ChuyenBayId)
                && x.SoGhe != "" && (x.TrangThai == TrangThaiVe.DaPhatHanh || x.TrangThai == TrangThaiVe.TamGiu))
            .GroupBy(x => new { x.ChuyenBayId, x.TrangThai })
            .Select(group => new
            {
                group.Key.ChuyenBayId,
                group.Key.TrangThai,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.ChuyenBayId)
            .ToDictionary(
                group => group.Key,
                group => (
                    DaBan: group.Where(x => x.TrangThai == TrangThaiVe.DaPhatHanh).Sum(x => x.Count),
                    DangGiu: group.Where(x => x.TrangThai == TrangThaiVe.TamGiu).Sum(x => x.Count)));
    }

    private static ManagerFlightRow ToOverviewFlightRow(
        ChuyenBay flight,
        int soldSeats,
        int heldSeats,
        DateTimeOffset now)
    {
        var capacity = flight.MayBay?.SoGhe ?? 0;
        var remaining = capacity - soldSeats - heldSeats;
        var (condition, tone) = flight.TrangThai switch
        {
            TrangThaiChuyenBay.BiHuy => (T("Đã hủy", "Cancelled"), "danger"),
            TrangThaiChuyenBay.HoanTat => (T("Hoàn tất", "Completed"), "neutral"),
            TrangThaiChuyenBay.DangBay => (T("Đang bay", "In flight"), "info"),
            _ when flight.GioKhoiHanh <= now && flight.GioHaCanh > now => (T("Đang bay", "In flight"), "info"),
            _ when remaining <= 0 => (T("Hết chỗ", "Sold out"), "danger"),
            _ when remaining <= 10 => (T($"Gần hết chỗ · còn {remaining}", $"Almost full · {remaining} seats left"), "warning"),
            _ => (T("Đúng lịch", "On schedule"), "success")
        };

        return new ManagerFlightRow(
            $"{flight.SoHieu} · {flight.TuyenBay.SanBayDi.MaSanBay} → {flight.TuyenBay.SanBayDen.MaSanBay}",
            $"{Money.Time(flight.GioKhoiHanh.ToOffset(Vn))}–{Money.Time(flight.GioHaCanh.ToOffset(Vn))}",
            $"{soldSeats} / {capacity}",
            condition,
            tone);
    }

    private static string HoldExpiryText(DateTimeOffset expires, DateTimeOffset nowVn)
    {
        if (expires == DateTimeOffset.MinValue)
        {
            return "—";
        }

        var value = expires.ToOffset(Vn);
        if (value <= nowVn)
        {
            return T($"Quá hạn {value:HH:mm}", $"Expired at {value:HH:mm}");
        }

        return value.Date == nowVn.Date
            ? T($"{value:HH:mm} hôm nay", $"{value:HH:mm} today")
            : value.Date == nowVn.Date.AddDays(1)
                ? T($"{value:HH:mm} ngày mai", $"{value:HH:mm} tomorrow")
                : $"{value:dd/MM HH:mm}";
    }

    private static string PercentText(decimal percent) =>
        percent.ToString("0.0", ViCulture) + "%";

    [HttpGet("/quan-ly/lich-bay")]
    public async Task<IActionResult> Schedules(
        string? ngay,
        string? tuyen,
        [FromQuery(Name = "trang-thai")] string? trangThai,
        int? chon,
        [FromQuery(Name = "hop-thoai")] string? hopThoai,
        [FromQuery(Name = "tao-moi")] string? taoMoi,
        CancellationToken cancellationToken)
    {
        var date = FlightMapping.TryParseDate(ngay, out var parsedDate)
            ? parsedDate
            : DateOnly.FromDateTime(FlightMapping.ToVietnamTime(DateTimeOffset.UtcNow).DateTime);
        var dateText = FlightMapping.DisplayDate(date);

        var danhMuc = FlightServices.DanhMuc(dbContext);
        var chuyenBay = FlightServices.ChuyenBay(dbContext);

        var routes = await danhMuc.LayTuyenBayAsync(cancellationToken: cancellationToken);
        var aircraft = await danhMuc.LayMayBayAsync(cancellationToken: cancellationToken);
        var airlines = await danhMuc.LayHangHangKhongAsync(true, cancellationToken);

        var routeId = ResolveRouteId(routes, tuyen);
        var flights = await chuyenBay.LayLichBayAsync(date, routeId, trangThai, cancellationToken);

        var isNew = string.Equals(taoMoi, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(taoMoi, "true", StringComparison.OrdinalIgnoreCase);

        ChuyenBayDTO? selected = null;
        if (!isNew)
        {
            selected = chon is > 0
                ? await chuyenBay.LayChuyenBayAsync(chon.Value, cancellationToken)
                : flights.FirstOrDefault();

            selected ??= flights.FirstOrDefault();
        }

        var model = new ManagerSchedulesViewModel
        {
            Subtitle = T("Quản lý lịch khai thác và giá cơ bản. Không thay đổi dữ liệu danh mục nền.", "Manage flight schedules and base fares."),
            DateValue = dateText,
            RouteValue = routeId is null
                ? AllText
                : routes.FirstOrDefault(item => item.Id == routeId) is { } route
                    ? $"{route.SanBayDi.MaSanBay} → {route.SanBayDen.MaSanBay}"
                    : AllText,
            AirlineStatusValue = string.IsNullOrWhiteSpace(trangThai) ? AllText : trangThai,
            ListTitle = T($"Lịch khai thác · {dateText}", $"Flight schedule · {dateText}"),
            Rows = flights.Select(item => new ManagerScheduleRow(
                $"{item.SoHieu} · {item.MaMayBay}",
                $"{item.MaSanBayDi} → {item.MaSanBayDen}",
                $"{FlightMapping.DisplayTime(item.GioKhoiHanh)}–{FlightMapping.DisplayTime(item.GioHaCanh)}",
                ChuyenBayBUS.HienThiTrangThai(ParseStatus(item.TrangThai)),
                StatusTone(item.TrangThai),
                $"{item.TongSoGhe} / {item.SoGheDaBan} / {item.SoGheDangGiu} / {item.SoGheTrong}",
                Money.Vnd(item.GiaCoBan),
                item.Id == selected?.Id,
                item.Id,
                $"/quan-ly/lich-bay?ngay={Uri.EscapeDataString(dateText)}&chon={item.Id}")).ToArray(),
            ResultText = T($"{flights.Count} chuyến trong ngày {dateText}", $"{flights.Count} flights on {dateText}"),
            RouteOptions = BuildRouteOptions(routes, routeId),
            StatusOptions = BuildStatusOptions(trangThai),
            EditorRouteOptions = routes.Select(item => new ManagerSelectOption(
                item.Id.ToString(CultureInfo.InvariantCulture),
                $"{item.SanBayDi.MaSanBay} → {item.SanBayDen.MaSanBay}",
                item.Id == selected?.TuyenBayId)).ToArray(),
            EditorAircraftOptions = aircraft.Select(item => new ManagerSelectOption(
                item.Id.ToString(CultureInfo.InvariantCulture),
                T($"{item.MaMayBay} · {item.TenMayBay} ({item.SoGhe} ghế)", $"{item.MaMayBay} · {item.TenMayBay} ({item.SoGhe} seats)"),
                item.Id == selected?.MayBayId)).ToArray(),
            EditorAirlineOptions = airlines.Select(item => new ManagerSelectOption(
                item.Id.ToString(CultureInfo.InvariantCulture),
                $"{item.MaHang} · {item.TenHang}",
                item.Id == selected?.HangHangKhongId)).ToArray(),
            EditorIsNew = selected is null,
            SelectedFlightId = selected?.Id ?? 0,
            EditorRouteId = selected?.TuyenBayId ?? routes.FirstOrDefault()?.Id ?? 0,
            EditorAircraftId = selected?.MayBayId ?? aircraft.FirstOrDefault()?.Id ?? 0,
            EditorAirlineId = selected?.HangHangKhongId ?? airlines.FirstOrDefault()?.Id ?? 0,
            EditorFlightNumber = selected?.SoHieu ?? string.Empty,
            EditorTitle = selected is null ? T("Thêm lịch khai thác", "Add flight schedule") : T($"Chỉnh sửa chuyến · {selected.SoHieu}", $"Edit flight · {selected.SoHieu}"),
            EditorSubtitle = selected is null
                ? T("Tạo chuyến mới cho tuyến và tàu bay đang khai thác", "Create a new flight on an active route and aircraft")
                : T($"{dateText} · {FlightMapping.AirlineNameFor(selected.SoHieu)} · Đã bán {selected.SoGheDaBan} ghế",
                    $"{dateText} · {FlightMapping.AirlineNameFor(selected.SoHieu)} · {selected.SoGheDaBan} seats sold"),
            EditorDateValue = selected is null ? dateText : FlightMapping.DisplayDate(selected.GioKhoiHanh),
            EditorTimeValue = selected is null
                ? "08:00 / 10:10"
                : $"{FlightMapping.DisplayTime(selected.GioKhoiHanh)} / {FlightMapping.DisplayTime(selected.GioHaCanh)}",
            EditorStatusValue = selected is null
                ? "Mở bán"
                : ChuyenBayBUS.HienThiTrangThai(ParseStatus(selected.TrangThai)),
            Fares = selected is null
                ? Array.Empty<ManagerFareRow>()
                : new[]
                {
                    new ManagerFareRow(
                        T("Phổ thông", "Economy"),
                        selected.TongSoGhe.ToString(CultureInfo.InvariantCulture),
                        (selected.SoGheDaBan + selected.SoGheDangGiu).ToString(CultureInfo.InvariantCulture),
                        Money.Vnd(selected.GiaCoBan))
                },
            EconomyPriceValue = selected is null ? string.Empty : Money.Vnd(selected.GiaCoBan),
            BusinessPriceValue = "—",
            EditorNote = selected is null
                ? "Giá cơ bản chưa gồm thuế/phí. Hệ thống kiểm tra trùng số hiệu và trùng lịch tàu bay trước khi lưu."
                : $"Giá cơ bản chưa gồm thuế/phí. Số ghế đang giữ: {selected.SoGheDangGiu} · " +
                  $"Còn trống: {selected.SoGheTrong}.",
            Message = TempData["ScheduleMessage"] as string,
            MessageTone = TempData["ScheduleTone"] as string ?? "info"
        };

        if (selected is not null)
        {
            model.CancelDialog = BuildCancelDialog(selected, dateText);
            model.PriceDialog = BuildPriceDialog(selected, dateText);
            model.ShowCancelDialog = string.Equals(hopThoai, "huy-chuyen", StringComparison.OrdinalIgnoreCase);
            model.ShowPriceDialog = string.Equals(hopThoai, "doi-gia", StringComparison.OrdinalIgnoreCase);
        }

        return View(model);
    }

    [HttpPost("/quan-ly/lich-bay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedules(IFormCollection form, CancellationToken cancellationToken)
    {
        var id = ParseInt(form["chuyen-bay-id"]);
        if (!FlightMapping.TryParseDate(form["ngay-bay"], out var date))
        {
            TempData["ScheduleMessage"] = "Ngày bay không hợp lệ. Nhập theo dạng ngày/tháng/năm.";
            TempData["ScheduleTone"] = "danger";
            return RedirectToAction(nameof(Schedules), new { chon = id > 0 ? id : (int?)null });
        }
        var dateText = FlightMapping.DisplayDate(date);

        if (!TryParseTimeRange(form["gio-bay"], out var start, out var end))
        {
            TempData["ScheduleMessage"] = "Giờ bay không hợp lệ. Nhập theo dạng 08:00 / 10:10.";
            TempData["ScheduleTone"] = "danger";
            return RedirectToAction(nameof(Schedules), new { ngay = dateText, chon = id > 0 ? id : (int?)null });
        }

        if (end <= start)
        {
            end = end.Add(TimeSpan.FromDays(1));
        }

        var midnight = date.ToDateTime(TimeOnly.MinValue);
        var departure = new DateTimeOffset(midnight.Add(start), ChuyenBayBUS.VietnamOffset);
        var arrival = new DateTimeOffset(midnight.Add(end), ChuyenBayBUS.VietnamOffset);
        var price = ParseMoney(form["gia-pho-thong"]);
        var status = string.IsNullOrWhiteSpace(form["trang-thai"]) ? "Mở bán" : form["trang-thai"].ToString();

        var chuyenBay = FlightServices.ChuyenBay(dbContext);
        KetQuaXuLy result;

        if (id == 0)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await BookingCheckoutService.LockAsync(dbContext, cancellationToken);
            result = await chuyenBay.TaoChuyenBayAsync(new TaoChuyenBayDTO
            {
                SoHieu = form["so-hieu"].ToString(),
                TuyenBayId = ParseInt(form["tuyen-bay-id"]),
                MayBayId = ParseInt(form["may-bay-id"]),
                HangHangKhongId = ParseInt(form["hang-hang-khong-id"]),
                GioKhoiHanh = departure,
                GioHaCanh = arrival,
                GiaCoBan = price,
                TrangThai = status
            }, cancellationToken);
            if (result.ThanhCong)
                await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            var current = await chuyenBay.LayChuyenBayAsync(id, cancellationToken);
            if (current is null)
            {
                TempData["ScheduleMessage"] = "Không tìm thấy chuyến bay cần cập nhật.";
                TempData["ScheduleTone"] = "danger";
                return RedirectToAction(nameof(Schedules), new { ngay = dateText });
            }

            result = await disruptions.UpdateScheduleAsync(new CapNhatChuyenBayDTO
            {
                Id = id,
                SoHieu = string.IsNullOrWhiteSpace(form["so-hieu"]) ? current.SoHieu : form["so-hieu"].ToString(),
                TuyenBayId = current.TuyenBayId,
                MayBayId = current.MayBayId,
                GioKhoiHanh = departure,
                GioHaCanh = arrival,
                GiaCoBan = price,
                TrangThai = status
            }, User.DisplayName(), cancellationToken);
        }

        TempData["ScheduleMessage"] = result.ThanhCong ? "Đã lưu lịch khai thác. Xem các đặt chỗ bị ảnh hưởng ở mục Biến động chuyến bay." : result.Loi;
        TempData["ScheduleTone"] = result.ThanhCong ? "success" : "danger";

        return RedirectToAction(nameof(Schedules), new
        {
            ngay = dateText,
            chon = id > 0 ? id : (int?)null
        });
    }

    [HttpPost("/quan-ly/lich-bay/huy-chuyen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelFlight(IFormCollection form, CancellationToken cancellationToken)
    {
        var id = ParseInt(form["chuyen-bay-id"]);
        var result = await disruptions.CancelAsync(id, form["ly-do"], form["thong-bao"], User.DisplayName(), cancellationToken);

        TempData["ScheduleMessage"] = result.ThanhCong ? "Đã hủy chuyến. Mở mục Biến động chuyến bay để xử lý vé bị ảnh hưởng." : result.Loi;
        TempData["ScheduleTone"] = result.ThanhCong ? "success" : "danger";

        return RedirectToAction(nameof(Schedules), new { ngay = form["ngay"].ToString() });
    }
>>>>>>> Stashed changes

    [HttpPost("/quan-ly/lich-bay/doi-gia")]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePrice(IFormCollection form) => RedirectToAction(nameof(Schedules));

    [HttpGet("/quan-ly/dat-ve")]
    public IActionResult Bookings(string? q) => View(DemoCatalog.ManagerBookings());

    [HttpPost("/quan-ly/dat-ve")]
    [ValidateAntiForgeryToken]
    public IActionResult Bookings(IFormCollection form) => RedirectToAction(nameof(Bookings));

<<<<<<< Updated upstream
=======
    [HttpPost("/quan-ly/dat-ve/xac-nhan-thanh-toan")]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmTransferPayment([FromForm] string? pnr) =>
        RedirectToAction(nameof(VnpayManagerController.Index), "VnpayManager", new { q = pnr, kenh = "qr" });

>>>>>>> Stashed changes
    [HttpGet("/quan-ly/doi-hoan")]
    public IActionResult ChangeRefunds(
        string? tab,
        [FromQuery(Name = "hop-thoai")] string? hopThoai) =>
        View(DemoCatalog.ManagerChangeRefunds(tab, hopThoai));

<<<<<<< Updated upstream
    [HttpPost("/quan-ly/doi-hoan/duyet")]
    [ValidateAntiForgeryToken]
    public IActionResult ApproveRefund(IFormCollection form) => RedirectToAction(nameof(ChangeRefunds));
=======
        var total = statusCounts.Sum(x => x.Count);
        var pending = CountOf(statusCounts.Select(x => (x.Key, x.Count)), TrangThaiYeuCau.ChoDuyet);
        var approved = CountOf(statusCounts.Select(x => (x.Key, x.Count)), TrangThaiYeuCau.DaDuyet);
        var completed = CountOf(statusCounts.Select(x => (x.Key, x.Count)), TrangThaiYeuCau.HoanTat)
            + CountOf(statusCounts.Select(x => (x.Key, x.Count)), TrangThaiYeuCau.TuChoi);

        var pendingSplit = await dbContext.YeuCauDoiHoans.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiYeuCau.ChoDuyet)
            .GroupBy(x => x.LoaiYeuCau)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var pendingChanges = pendingSplit.FirstOrDefault(x => x.Key == LoaiYeuCau.DoiVe)?.Count ?? 0;
        var pendingRefunds = pendingSplit.FirstOrDefault(x => x.Key == LoaiYeuCau.HoanVe)?.Count ?? 0;

        IQueryable<YeuCauDoiHoan> query = dbContext.YeuCauDoiHoans.AsNoTracking()
            .Include(x => x.DatVe).ThenInclude(x => x.KhachHang)
            .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .Include(x => x.DatVe).ThenInclude(x => x.ThanhToans).ThenInclude(x => x.HoanTiens)
            .Include(x => x.ChuyenBayMoi)
            .Include(x => x.NguoiXuLy);

        query = activeTab switch
        {
            "cho-duyet" => query.Where(x => x.TrangThai == TrangThaiYeuCau.ChoDuyet),
            "da-duyet" => query.Where(x => x.TrangThai == TrangThaiYeuCau.DaDuyet),
            "hoan-tat" => query.Where(x =>
                x.TrangThai == TrangThaiYeuCau.HoanTat || x.TrangThai == TrangThaiYeuCau.TuChoi),
            _ => query
        };

        var keyword = q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(x =>
                x.MaYeuCau.ToLower().Contains(keyword)
                || x.DatVe.MaDatVe.ToLower().Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(loai))
        {
            if (loai.Contains("Hoàn", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.LoaiYeuCau == LoaiYeuCau.HoanVe);
            }
            else if (loai.Contains("Đổi", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.LoaiYeuCau == LoaiYeuCau.DoiVe);
            }
        }

        if (FlightMapping.TryParseDate(ngay, out var dateFilter))
        {
            var start = new DateTimeOffset(dateFilter.ToDateTime(TimeOnly.MinValue), Vn);
            var end = start.AddDays(1);
            var startUtc = start.ToUniversalTime();
            var endUtc = end.ToUniversalTime();
            query = query.Where(x => x.NgayTao >= startUtc && x.NgayTao < endUtc);
        }

        var list = await query
            .OrderByDescending(x => x.NgayTao)
            .Take(200)
            .ToListAsync(cancellationToken);

        var selected = id is > 0
            ? list.FirstOrDefault(x => x.Id == id.Value)
            : null;
        selected ??= list.FirstOrDefault();

        var rows = list.Select(item =>
        {
            var (statusText, statusTone) = RequestStatus(item.TrangThai, item.LoaiYeuCau);
            return new ManagerRequestRow(
                item.MaYeuCau,
                item.DatVe.MaDatVe,
                item.DatVe.KhachHang.HoTen,
                item.LoaiYeuCau == LoaiYeuCau.HoanVe ? T("Hoàn vé", "Refund") : T("Đổi vé", "Ticket change"),
                item.NgayTao.ToOffset(Vn).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                statusText,
                statusTone,
                selected != null && item.Id == selected.Id,
                item.Id,
                Url.Action(nameof(ChangeRefunds), null, new { tab = activeTab, id = item.Id, q, loai, ngay }, null, null, "chi-tiet-yeu-cau"));
        }).ToArray();

        var model = new ManagerChangeRefundsViewModel
        {
            Tabs = new[]
            {
                new ManagerQueueTab(T($"Tất cả ({total})", $"All ({total})"), "/quan-ly/doi-hoan", activeTab == "all"),
                new ManagerQueueTab(T($"Chờ duyệt ({pending})", $"Pending approval ({pending})"), "/quan-ly/doi-hoan?tab=cho-duyet", activeTab == "cho-duyet"),
                new ManagerQueueTab(T($"Đã duyệt ({approved})", $"Approved ({approved})"), "/quan-ly/doi-hoan?tab=da-duyet", activeTab == "da-duyet"),
                new ManagerQueueTab(T($"Hoàn tất ({completed})", $"Completed ({completed})"), "/quan-ly/doi-hoan?tab=hoan-tat", activeTab == "hoan-tat")
            },
            QueueSubtitle = T($"Chờ duyệt: {pendingChanges} đổi vé · {pendingRefunds} hoàn vé",
                $"Awaiting approval: {pendingChanges} changes · {pendingRefunds} refunds"),
            Rows = rows,
            ResultText = rows.Length == 0
                ? T("Không có yêu cầu phù hợp bộ lọc", "No requests match the filters")
                : T($"Hiển thị {rows.Length} yêu cầu", $"Showing {rows.Length} requests"),
            SearchValue = q?.Trim(),
            TypeValue = string.IsNullOrWhiteSpace(loai) ? AllText : loai,
            DateValue = ngay ?? string.Empty,
            Message = TempData["RefundMessage"] as string,
            MessageTone = TempData["RefundTone"] as string ?? "info"
        };

        if (selected == null)
        {
            return View(model);
        }

        ViewData["SelectedRequestId"] = selected.Id;
        BuildRequestDetail(model, selected, hopThoai, pendingChanges, pendingRefunds);
        return View(model);
    }

    /// <summary>
    /// Duyệt yêu cầu đổi/hoàn. Với hoàn vé, tạo bản ghi <see cref="HoanTien"/> ở trạng thái
    /// chờ xử lý: duyệt không đồng nghĩa tiền đã về tài khoản khách.
    /// </summary>
    [HttpPost("/quan-ly/doi-hoan/duyet")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRefund(IFormCollection form, CancellationToken cancellationToken)
    {
        var id = ParseInt(form["yeu-cau-id"]);
        var reason = NormalizeReason(form["ly-do"].ToString(), "Đã kiểm tra điều kiện vé và duyệt yêu cầu.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await BookingCheckoutService.LockAsync(dbContext, cancellationToken);
        var request = await LoadRequestForDecisionAsync(id, cancellationToken);
        if (request == null)
        {
            return RefundMessage("Không tìm thấy yêu cầu cần duyệt.", "danger", "cho-duyet");
        }

        if (request.TrangThai != TrangThaiYeuCau.ChoDuyet)
        {
            return RefundMessage($"Yêu cầu {request.MaYeuCau} đã được xử lý trước đó.", "info", "cho-duyet", request.Id);
        }

        var now = DateTimeOffset.UtcNow;
        var booking = request.DatVe;

        try
        {
            if (request.LoaiYeuCau == LoaiYeuCau.HoanVe)
                afterSales.PrepareRefund(request, now);
            else
                await afterSales.PrepareChangeAsync(request, cancellationToken);
        }
        catch (BookingValidationException error)
        {
            return RefundMessage(error.Message, "danger", "cho-duyet", request.Id);
        }

        var previous = request.TrangThai;
        request.TrangThai = TrangThaiYeuCau.DaDuyet;
        request.NgayXuLy = now;
        request.NguoiXuLyId = User.TaiKhoanId();
        request.GhiChuXuLy = Truncate(reason, 500);

        dbContext.LichSuDatVes.Add(new LichSuDatVe
        {
            DatVeId = booking.Id,
            HanhDong = request.LoaiYeuCau == LoaiYeuCau.HoanVe ? "DuyetYeuCauHoan" : "DuyetYeuCauDoi",
            TrangThaiTruoc = previous.ToString(),
            TrangThaiSau = request.TrangThai.ToString(),
            LyDo = Truncate(reason, 300),
            NguoiThucHien = User.DisplayName(),
            ThoiDiem = now,
            DuLieuBoSung = JsonSerializer.Serialize(new
            {
                request.MaYeuCau,
                Loai = request.LoaiYeuCau.ToString(),
                request.SoTienHoan
            })
        });

        AddAuditLog(
            request.LoaiYeuCau == LoaiYeuCau.HoanVe ? "Duyệt yêu cầu hoàn" : "Duyệt yêu cầu đổi",
            request.MaYeuCau,
            $"PNR {booking.MaDatVe}. {reason}",
            now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RefundMessage(
            request.LoaiYeuCau == LoaiYeuCau.HoanVe
                ? $"Đã duyệt hoàn {request.MaYeuCau}. Lệnh hoàn {Money.Vnd(request.SoTienHoan)} đang chờ chi trả."
                : $"Đã duyệt báo giá {request.MaYeuCau}. Khách có thể xác nhận trong Vé của tôi.",
            "success",
            "da-duyet",
            request.Id);
    }
>>>>>>> Stashed changes

    [HttpPost("/quan-ly/doi-hoan/hoan-tat-demo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteDemoRefund(int id, bool confirmed, CancellationToken cancellationToken)
    {
        if (!confirmed)
            return RefundMessage("Vui lòng xác nhận thao tác hoàn tiền mô phỏng.", "warning", "da-duyet", id);
        try
        {
            await afterSales.CompleteDemoRefundAsync(id, User.TaiKhoanId(), User.DisplayName(), cancellationToken);
            return RefundMessage("Đã hoàn tiền mô phỏng và giải phóng ghế của đặt chỗ.", "success", "hoan-tat", id);
        }
        catch (BookingValidationException error)
        {
            return RefundMessage(error.Message, "danger", "da-duyet", id);
        }
    }

    [HttpPost("/quan-ly/doi-hoan/tu-choi")]
    [ValidateAntiForgeryToken]
<<<<<<< Updated upstream
    public IActionResult RejectRefund(IFormCollection form) => RedirectToAction(nameof(ChangeRefunds));

    [HttpGet("/quan-ly/bao-cao")]
    public IActionResult Reports() => RedirectToAction("Index", "BaoCao");

    [HttpPost("/quan-ly/bao-cao")]
    [ValidateAntiForgeryToken]
    public IActionResult Reports(IFormCollection form) => RedirectToAction("Index", "BaoCao");

    [HttpPost("/quan-ly/bao-cao/xuat")]
    [ValidateAntiForgeryToken]
    public IActionResult ExportReport(IFormCollection form) => RedirectToAction(nameof(Reports));
=======
    public async Task<IActionResult> RejectRefund(IFormCollection form, CancellationToken cancellationToken)
    {
        var id = ParseInt(form["yeu-cau-id"]);
        var reason = NormalizeReason(form["ly-do"].ToString(), "Không đáp ứng điều kiện đổi / hoàn vé.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await BookingCheckoutService.LockAsync(dbContext, cancellationToken);
        var request = await LoadRequestForDecisionAsync(id, cancellationToken);
        if (request == null)
        {
            return RefundMessage("Không tìm thấy yêu cầu cần từ chối.", "danger", "cho-duyet");
        }

        if (request.TrangThai != TrangThaiYeuCau.ChoDuyet)
        {
            return RefundMessage($"Yêu cầu {request.MaYeuCau} đã được xử lý trước đó.", "info", "cho-duyet", request.Id);
        }

        var now = DateTimeOffset.UtcNow;
        var previous = request.TrangThai;
        request.TrangThai = TrangThaiYeuCau.TuChoi;
        request.NgayXuLy = now;
        request.NguoiXuLyId = User.TaiKhoanId();
        request.GhiChuXuLy = Truncate(reason, 500);

        dbContext.LichSuDatVes.Add(new LichSuDatVe
        {
            DatVeId = request.DatVeId,
            HanhDong = "TuChoiYeuCau",
            TrangThaiTruoc = previous.ToString(),
            TrangThaiSau = request.TrangThai.ToString(),
            LyDo = Truncate(reason, 300),
            NguoiThucHien = User.DisplayName(),
            ThoiDiem = now,
            DuLieuBoSung = JsonSerializer.Serialize(new { request.MaYeuCau, Loai = request.LoaiYeuCau.ToString() })
        });

        AddAuditLog(
            "Từ chối yêu cầu đổi/hoàn",
            request.MaYeuCau,
            $"PNR {request.DatVe.MaDatVe}. {reason}",
            now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RefundMessage($"Đã từ chối yêu cầu {request.MaYeuCau}.", "info", "hoan-tat", request.Id);
    }

    private IActionResult RefundMessage(string message, string tone, string tab, int? id = null)
    {
        TempData["RefundMessage"] = message;
        TempData["RefundTone"] = tone;
        return RedirectToAction(nameof(ChangeRefunds), new { tab, id });
    }

    private Task<YeuCauDoiHoan?> LoadRequestForDecisionAsync(int id, CancellationToken cancellationToken) =>
        dbContext.YeuCauDoiHoans
            .Include(x => x.DatVe).ThenInclude(x => x.BienDongs.Where(change => change.NgayXuLy == null))
            .Include(x => x.DatVe).ThenInclude(x => x.KhachHang)
            .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.HanhKhach)
            .Include(x => x.DatVe).ThenInclude(x => x.ThanhToans)
                .ThenInclude(x => x.HoanTiens)
            .Include(x => x.ChuyenBayMoi!).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.ChuyenBayMoi!).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    private static string NormalizeRefundTab(string? tab) => tab?.Trim().ToLowerInvariant() switch
    {
        "cho-duyet" => "cho-duyet",
        "da-duyet" => "da-duyet",
        "hoan-tat" => "hoan-tat",
        _ => "all"
    };

    private static int CountOf(IEnumerable<(TrangThaiYeuCau Status, int Count)> counts, TrangThaiYeuCau status) =>
        counts.Where(x => x.Status == status).Sum(x => x.Count);

    private static (string Text, string Tone) RequestStatus(TrangThaiYeuCau status, LoaiYeuCau type) => status switch
    {
        TrangThaiYeuCau.ChoDuyet => (T("Chờ duyệt", "Pending approval"), "warning"),
        TrangThaiYeuCau.DaDuyet => type == LoaiYeuCau.HoanVe
            ? (T("Đã duyệt · Chờ hoàn tiền", "Approved · Awaiting refund"), "info")
            : (T("Đã duyệt · Chờ đổi vé", "Approved · Awaiting change"), "info"),
        TrangThaiYeuCau.HoanTat => (T("Hoàn tất", "Completed"), "success"),
        TrangThaiYeuCau.TuChoi => (T("Từ chối", "Rejected"), "danger"),
        _ => (status.ToString(), "neutral")
    };

    private static void BuildRequestDetail(
        ManagerChangeRefundsViewModel model,
        YeuCauDoiHoan request,
        string? hopThoai,
        int pendingChanges,
        int pendingRefunds)
    {
        var booking = request.DatVe;
        var ve = booking.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var flight = ve?.ChuyenBay;
        var route = flight == null
            ? booking.MaDatVe
            : $"{flight.TuyenBay.SanBayDi.MaSanBay} → {flight.TuyenBay.SanBayDen.MaSanBay}";
        var paid = TicketAfterSalesService.Captures(booking).Sum(x => x.SoTien);
        if (paid <= 0)
        {
            paid = booking.TongTien;
        }

        model.DetailTitle = T($"Chi tiết yêu cầu · {request.MaYeuCau}", $"Request details · {request.MaYeuCau}");
        model.DetailSubtitle =
            $"{booking.KhachHang.HoTen} · {booking.MaDatVe} · Vé {(string.IsNullOrWhiteSpace(ve?.MaVe) ? "chưa phát hành" : ve.MaVe)}";
        model.DetailFields = new[]
        {
            new AdminDetailField(T("Chuyến trên vé", "Ticketed flight"), flight == null ? "—" : $"{flight.SoHieu} · {route}"),
            new AdminDetailField(T("Khởi hành", "Departure"), flight == null
                ? "—"
                : $"{FlightMapping.DisplayDate(flight.GioKhoiHanh)} · {FlightMapping.DisplayTime(flight.GioKhoiHanh)} (UTC+7)"),
            new AdminDetailField(T("Loại vé", "Cabin"), UiLanguage.Label(string.IsNullOrWhiteSpace(ve?.HangVe) ? "Phổ thông" : ve.HangVe)),
            new AdminDetailField(T("Lý do của khách", "Customer's reason"), request.LyDo),
            new AdminDetailField(T("Thông tin bổ sung", "Additional information"), string.IsNullOrWhiteSpace(request.ThongTinBoSung) ? "—" : request.ThongTinBoSung)
        };

        model.IsRefundRequest = request.LoaiYeuCau == LoaiYeuCau.HoanVe;
        model.CanDecide = request.TrangThai == TrangThaiYeuCau.ChoDuyet;
        model.CanCompleteDemoRefund = TicketAfterSalesService.CanCompleteDemoRefund(request);
        if (request.TrangThai == TrangThaiYeuCau.DaDuyet
            && booking.ThanhToans.Any(p => p.NhaCungCap is VnpayOptions.Provider or VietQr.Provider
                && p.HoanTiens.Any(r => r.YeuCauDoiHoanId == request.Id)))
            model.RefundLedgerBookingCode = booking.MaDatVe;
        if (request.LoaiYeuCau == LoaiYeuCau.HoanVe)
        {
            model.ProcessTitle = $"Quy trình hoàn vé · {pendingRefunds} yêu cầu đang chờ";
            model.ProcessText =
                "Kiểm tra điều kiện vé → xác nhận phí hoàn và phần không hoàn → duyệt để tạo lệnh hoàn đang chờ chi trả → " +
                "xác nhận chi trả mô phỏng hoặc nhận kết quả từ cổng thanh toán. " +
                $"Hiện còn {pendingChanges} yêu cầu đổi vé chờ duyệt.";
        }
        else
        {
            model.ProcessTitle = $"Quy trình đổi vé · {pendingChanges} yêu cầu đang chờ";
            model.ProcessText =
                "Kiểm tra chỗ trên chuyến mới → tính phí đổi + chênh lệch giá → khách xác nhận và thanh toán thêm → xuất lại vé. " +
                $"Hiện còn {pendingRefunds} yêu cầu hoàn vé chờ duyệt.";
        }

        model.RefundMethodValue = !string.IsNullOrWhiteSpace(request.PhuongThucHoan)
            ? request.PhuongThucHoan
            : PaymentMethodText(booking.ThanhToans.OrderByDescending(x => x.NgayTao).FirstOrDefault());

        if (request.LoaiYeuCau == LoaiYeuCau.HoanVe)
        {
            var amount = request.SoTienHoan > 0
                ? request.SoTienHoan
                : TicketAfterSalesService.RefundAmount(booking, request);

            model.RefundTitle = "Tính hoàn tiền dự kiến";
            model.RefundRows = new[]
            {
                new ManagerRefundRow("Tổng đã thanh toán", Money.Vnd(paid)),
                new ManagerRefundRow("Phí hoàn vé", "−" + Money.Vnd(request.PhiHoan)),
                new ManagerRefundRow("Phí đổi vé đã thu", "−" + Money.Vnd(booking.PhiDoiDaThu)),
                new ManagerRefundRow("Phí dịch vụ không hoàn", "−" + Money.Vnd(request.PhiDichVuKhongHoan)),
                new ManagerRefundRow("Hành lý thêm không hoàn", "−" + Money.Vnd(request.PhiHanhLyKhongHoan)),
                new ManagerRefundRow("Dự kiến hoàn", Money.Vnd(amount), true)
            };
            model.ConditionText =
                $"Điều kiện: phí hoàn {Money.Vnd(request.PhiHoan)}; không hoàn phí dịch vụ {Money.Vnd(request.PhiDichVuKhongHoan)}" +
                (request.PhiHanhLyKhongHoan > 0
                    ? $" và hành lý mua thêm {Money.Vnd(request.PhiHanhLyKhongHoan)}."
                    : ".");
            model.ApproveText = "Phê duyệt hoàn vé";
        }
        else
        {
            var newFlight = request.ChuyenBayMoi;
            var oldFare = ve?.GiaVe ?? flight?.GiaCoBan ?? 0m;
            var newFare = request.GiaVeMoi ?? newFlight?.GiaCoBan ?? oldFare;
            var charge = TicketAfterSalesService.ExtraCharge(oldFare, newFare, request.PhiDoi);

            model.RefundTitle = request.TrangThai == TrangThaiYeuCau.HoanTat ? "Kết quả đổi vé" : "Chi phí đổi vé";
            model.RefundRows = new[]
            {
                new ManagerRefundRow("Tổng đã thanh toán", Money.Vnd(paid)),
                new ManagerRefundRow("Phí đổi vé", Money.Vnd(request.PhiDoi)),
                new ManagerRefundRow("Giá vé chuyến mới", Money.Vnd(newFare)),
                new ManagerRefundRow("Cần thu thêm", Money.Vnd(charge), true)
            };
            if (request.TrangThai == TrangThaiYeuCau.HoanTat)
                model.RefundRows = [new ManagerRefundRow("Phát hành lại", request.GhiChuXuLy ?? "Đã phát hành vé mới.")];
            model.ConditionText = newFlight == null
                ? $"Điều kiện: phí đổi {Money.Vnd(request.PhiDoi)}. Chưa chọn được chuyến mới, vận hành cần liên hệ khách."
                : $"Điều kiện: phí đổi {Money.Vnd(request.PhiDoi)}; chuyến mới {newFlight.SoHieu} " +
                  $"{FlightMapping.DisplayDate(newFlight.GioKhoiHanh)} {FlightMapping.DisplayTime(newFlight.GioKhoiHanh)}. " +
                  "Khách thanh toán chênh lệch trước khi xuất lại vé.";
            model.ApproveText = "Phê duyệt đổi vé";
        }

        var (statusText, statusTone) = RequestStatus(request.TrangThai, request.LoaiYeuCau);
        model.EligibleTitle = request.TrangThai switch
        {
            TrangThaiYeuCau.ChoDuyet => request.LoaiYeuCau == LoaiYeuCau.HoanVe
                ? "Yêu cầu hoàn vé đang chờ duyệt"
                : "Yêu cầu đổi vé đang chờ duyệt",
            TrangThaiYeuCau.DaDuyet => request.LoaiYeuCau == LoaiYeuCau.DoiVe ? "Đã duyệt · Chờ khách xác nhận" : "Đã duyệt · Chờ hoàn tiền",
            TrangThaiYeuCau.TuChoi => "Yêu cầu đã bị từ chối",
            _ => "Yêu cầu đã hoàn tất"
        };
        model.EligibleTone = statusTone == "warning" ? "warning" : statusTone == "danger" ? "danger" : "success";
        model.EligibleText = request.TrangThai == TrangThaiYeuCau.ChoDuyet
            ? (request.LoaiYeuCau == LoaiYeuCau.HoanVe
                ? "Vé đã thanh toán và chưa sử dụng. Kiểm tra điều kiện vé trước khi duyệt tạo lệnh hoàn."
                : "Kiểm tra chỗ trống trên chuyến mới và xác nhận mức phí đổi với khách trước khi duyệt.")
            : $"Trạng thái hiện tại: {statusText}. {request.GhiChuXuLy ?? "Không có ghi chú xử lý."}";

        if (model.CanDecide && request.LoaiYeuCau == LoaiYeuCau.HoanVe
            && string.Equals(hopThoai, "duyet-hoan", StringComparison.OrdinalIgnoreCase))
        {
            var amount = request.SoTienHoan > 0
                ? request.SoTienHoan
                : TicketAfterSalesService.RefundAmount(booking, request);
            var amountText = Money.Vnd(amount);

            model.ShowApproveDialog = true;
            model.ApproveDialog = new ManagerApproveRefundDialogViewModel
            {
                Title = "Duyệt và tạo lệnh hoàn vé?",
                Subtitle = $"{request.MaYeuCau} / {booking.MaDatVe} · {booking.KhachHang.HoTen} · Trạng thái: Chờ duyệt",
                TicketValue = string.IsNullOrWhiteSpace(ve?.MaVe) ? "Chưa phát hành" : ve.MaVe,
                FlightValue = flight == null
                    ? route
                    : $"{flight.SoHieu} · {route} · {FlightMapping.DisplayDate(flight.GioKhoiHanh)} {FlightMapping.DisplayTime(flight.GioKhoiHanh)}",
                TotalValue = Money.Vnd(paid),
                FeeValue = "−" + Money.Vnd(request.PhiHoan),
                ServiceValue = "−" + Money.Vnd(request.PhiDichVuKhongHoan),
                BaggageValue = "−" + Money.Vnd(request.PhiHanhLyKhongHoan),
                AmountValue = amountText,
                MethodValue = model.RefundMethodValue,
                ReasonValue = "Vé đủ điều kiện; khách xác nhận mức phí hoàn.",
                ConfirmCheckText = $"Tôi đã kiểm tra số tiền {amountText} và nguồn nhận hoàn {model.RefundMethodValue}.",
                CancelHref = $"/quan-ly/doi-hoan?tab={NormalizeRefundTab(null)}&id={request.Id}"
            };
        }

        if (request.TrangThai != TrangThaiYeuCau.ChoDuyet)
        {
            model.ReasonValue = request.GhiChuXuLy ?? "Yêu cầu đã được xử lý.";
        }
    }

    private static string PaymentMethodText(ThanhToan? payment)
    {
        if (payment == null)
        {
            return "Nguồn thanh toán gốc";
        }

        var name = payment.PhuongThuc switch
        {
            PhuongThucThanhToan.TienMat => "Tiền mặt",
            PhuongThucThanhToan.TheNganHang => "Thẻ ngân hàng",
            PhuongThucThanhToan.ChuyenKhoan => "Chuyển khoản",
            PhuongThucThanhToan.ViDienTu => "Ví điện tử",
            _ => "Nguồn thanh toán gốc"
        };

        return string.IsNullOrWhiteSpace(payment.MaGiaoDich)
            ? name
            : $"{name} · {payment.MaGiaoDich}";
    }

    private static string NormalizeReason(string? value, string fallback)
    {
        var reason = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(reason) ? fallback : reason;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private void AddAuditLog(string action, string target, string detail, DateTimeOffset at)
    {
        dbContext.NhatKyHoatDongs.Add(new NhatKyHoatDong
        {
            MaNhatKy = NhatKyHoatDong.TaoMa(),
            TaiKhoanId = User.TaiKhoanId(),
            TenNguoiDung = User.DisplayName(),
            VaiTro = User.FindFirstValue(ClaimTypes.Role) ?? "Manager",
            HanhDong = Truncate(action, 200),
            DoiTuong = Truncate(target, 100),
            KetQua = KetQuaNhatKy.ThanhCong,
            ChiTiet = Truncate(detail, 1000),
            DiaChiIp = HttpContext?.Connection?.RemoteIpAddress?.ToString(),
            ThoiGian = at
        });
    }

    [HttpGet("/quan-ly/bao-cao")]
    public async Task<IActionResult> Reports(
        string? ky,
        string? hang,
        string? tuyen,
        string? chuyen,
        CancellationToken cancellationToken) =>
        View(await BuildReportsAsync(ky, hang, tuyen, chuyen, cancellationToken));

    [HttpPost("/quan-ly/bao-cao")]
    [ValidateAntiForgeryToken]
    public IActionResult Reports(IFormCollection form) =>
        RedirectToAction(nameof(Reports), new
        {
            ky = form["ky"].ToString(),
            hang = form["hang"].ToString(),
            tuyen = form["tuyen"].ToString(),
            chuyen = form["chuyen"].ToString()
        });

    [HttpPost("/quan-ly/bao-cao/xuat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportReport(IFormCollection form, CancellationToken cancellationToken)
    {
        var model = await BuildReportsAsync(
            form["ky"].ToString(),
            form["hang"].ToString(),
            form["tuyen"].ToString(),
            form["chuyen"].ToString(),
            cancellationToken);
        if (model.PeriodError is not null)
            return View("Reports", model);

        var sb = new StringBuilder();
        sb.AppendLine("Bao cao kinh doanh - AeroViet");
        sb.AppendLine($"Ky,{Csv(model.PeriodValue)}");
        sb.AppendLine($"Hang,{Csv(model.AirlineValue)}");
        sb.AppendLine($"Tuyen,{Csv(model.RouteValue)}");
        sb.AppendLine($"Chuyen,{Csv(model.FlightValue)}");
        sb.AppendLine($"Xuat luc,{Csv(DateTimeOffset.UtcNow.ToOffset(Vn).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))}");
        sb.AppendLine();
        sb.AppendLine("Chi so,Gia tri,Ghi chu");
        foreach (var kpi in model.Kpis)
        {
            sb.AppendLine($"{Csv(kpi.Label)},{Csv(kpi.Value)},{Csv(kpi.Meta)}");
        }

        sb.AppendLine();
        sb.AppendLine("Tuyen bay,Ve da xuat,Doanh thu gop,Tien da hoan,Doanh thu sau hoan");
        foreach (var row in model.Routes)
        {
            sb.AppendLine($"{Csv(row.Route)},{Csv(row.Tickets)},{Csv(row.Gross)},{Csv(row.Refunded)},{Csv(row.Net)}");
        }

        sb.AppendLine();
        sb.AppendLine("Chuyen bay,Khoi hanh (UTC+7),Tuyen bay,Ve da xuat,Doanh thu gop,Tien da hoan,Doanh thu sau hoan");
        foreach (var row in model.Flights)
        {
            sb.AppendLine($"{Csv(row.Flight)},{Csv(row.Departure)},{Csv(row.Route)},{Csv(row.Tickets)},{Csv(row.Gross)},{Csv(row.Refunded)},{Csv(row.Net)}");
        }

        sb.AppendLine();
        sb.AppendLine("Hang hang khong,Doanh thu,Ty trong");
        foreach (var airline in model.Airlines)
        {
            sb.AppendLine($"{Csv(airline.Name)},{Csv(airline.Amount)},{Csv(airline.ShareText)}");
        }

        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();

        return File(bytes, "text/csv; charset=utf-8", $"bao-cao-{DateTimeOffset.UtcNow:yyyyMMddHHmm}.csv");
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        var first = text.TrimStart().FirstOrDefault();
        if (first is '=' or '+' or '-' or '@' || text.StartsWith('\t') || text.StartsWith('\r'))
            text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private async Task<ManagerReportsViewModel> BuildReportsAsync(
        string? periodText,
        string? airlineText,
        string? routeText,
        string? flightText,
        CancellationToken ct)
    {
        var nowVn = DateTimeOffset.UtcNow.ToOffset(Vn);
        var from = new DateTimeOffset(nowVn.Year, nowVn.Month, 1, 0, 0, 0, Vn);
        var to = from.AddMonths(1);
        string? periodError = null;
        if (TryParsePeriod(periodText, out var parsedFrom, out var parsedTo) && parsedTo > parsedFrom)
            (from, to) = (parsedFrom, parsedTo);
        else if (!string.IsNullOrWhiteSpace(periodText))
            periodError = T("Kỳ báo cáo chưa hợp lệ. Nhập ngày, khoảng ngày hoặc tháng theo dạng MM/yyyy.", "Invalid reporting period. Enter a date, date range or month in MM/yyyy format.");

        var airline = IsAllValue(airlineText, AllAirlines) ? null : airlineText!.Trim();
        var route = IsAllValue(routeText, AllRoutes) ? null : routeText!.Trim();
        var flight = flightText?.Trim();
        if (string.IsNullOrEmpty(flight)) flight = null;
        var airlineNames = await dbContext.HangHangKhongS.AsNoTracking()
            .Where(x => x.DangHoatDong).OrderBy(x => x.TenHang).Select(x => x.TenHang).ToListAsync(ct);
        var routes = await dbContext.TuyenBays.AsNoTracking()
            .Include(x => x.SanBayDi).Include(x => x.SanBayDen).Where(x => x.DangKhaiThac)
            .OrderBy(x => x.SanBayDi.MaSanBay).ThenBy(x => x.SanBayDen.MaSanBay).ToListAsync(ct);

        var monthStart = new DateTimeOffset(from.Year, from.Month, 1, 0, 0, 0, Vn);
        var trendMonths = Enumerable.Range(0, 6).Select(offset => monthStart.AddMonths(offset - 5)).ToArray();
        var earliest = DateTimeOffset.MinValue.ToOffset(Vn);
        var duration = to - from;
        var previousFrom = from - earliest < duration ? earliest : from - duration;
        var report = await thongKe.LayBaoCaoAsync(from, to, previousFrom, trendMonths[0],
            monthStart.AddMonths(1), airline, route, flight, ct);
        var gross = report.DaThu;
        var bars = trendMonths.Select(month =>
        {
            var amount = report.XuHuongThu.Where(x => x.GhiNhanLuc >= month && x.GhiNhanLuc < month.AddMonths(1)).Sum(x => x.SoTien);
            var billions = amount / 1_000_000_000m;
            return new ManagerRevenueBar($"T{month:MM}", billions.ToString("0.0#", ViCulture), billions, month == monthStart);
        }).ToArray();

        var tones = new[] { "primary", "success", "muted", "info" };
        var airlines = report.TheoChuyen.GroupBy(x => x.Chuyen.Hang)
            .Select(group => new { Name = group.Key, Amount = group.Sum(x => x.DaThu) })
            .Where(group => group.Amount > 0).OrderByDescending(group => group.Amount).Select((group, index) =>
            {
                var share = group.Amount * 100m / gross;
                return new ManagerAirlineRevenue(group.Name, Money.Vnd(group.Amount), T($"{PercentText(share)} tổng đã thu", $"{PercentText(share)} of total collected"),
                    (int)Math.Round(share, MidpointRounding.AwayFromZero), tones[index % tones.Length]);
            }).ToArray();

        var routeRows = report.TheoChuyen.GroupBy(x => x.Chuyen.Tuyen).OrderBy(group => group.Key)
            .Select(group => FormatReportRow(group.Key, group.Sum(x => x.VeDaXuat), group.Sum(x => x.DaThu), group.Sum(x => x.DaHoan)))
            .ToList();
        if (routeRows.Count > 0) routeRows.Add(FormatReportRow(T("Tổng cộng", "Total"), report.VeDaXuat, gross, report.DaHoan, true));
        var flightRows = report.TheoChuyen.Select(row => new ManagerFlightRevenue(
            row.Chuyen.SoHieu ?? T("Chưa gắn chuyến", "Unassigned flight"),
            row.Chuyen.KhoiHanh is { } date ? $"{FlightMapping.DisplayDate(date)} {FlightMapping.DisplayTime(date)}" : "—",
            row.Chuyen.Tuyen, row.VeDaXuat.ToString("N0", ViCulture), Money.Vnd(row.DaThu), Money.Vnd(row.DaHoan),
            Money.Vnd(row.ThuRong))).ToArray();

        var canceled = report.SoDatChoDaHuy;
        var bookings = report.SoDatChoTrongKy;
        var cancelRate = bookings == 0 ? 0m : canceled * 100m / bookings;
        return new ManagerReportsViewModel
        {
            PeriodValue = $"{from:dd/MM/yyyy} – {to.AddDays(-1):dd/MM/yyyy}",
            PeriodError = periodError,
            AirlineValue = airline ?? AllAirlines,
            RouteValue = route ?? AllRoutes,
            FlightValue = flight ?? string.Empty,
            AirlineOptions = new[] { new ManagerSelectOption(string.Empty, T(AllAirlines, "All airlines"), airline is null) }
                .Concat(airlineNames.Select(name => new ManagerSelectOption(name, name, name == airline))).ToArray(),
            RouteOptions = new[] { new ManagerSelectOption(string.Empty, T(AllRoutes, "All routes"), route is null) }
                .Concat(routes.Select(ThongKeBUS.TenTuyen).Distinct().Select(label => new ManagerSelectOption(label, label, label == route))).ToArray(),
            Kpis =
            [
                new(T("Doanh thu gộp", "Gross revenue"), Money.Vnd(gross), GrowthText(gross, report.DaThuKyTruoc),
                    gross >= report.DaThuKyTruoc ? "success" : "warning", "chart"),
                new(T("Thu ròng", "Net collections"), Money.Vnd(report.ThuRong), T($"Đã hoàn {Money.Vnd(report.DaHoan)} · {report.SoGiaoDichHoan} giao dịch", $"Refunded {Money.Vnd(report.DaHoan)} · {report.SoGiaoDichHoan} transactions"),
                    report.ThuRong >= 0 ? "success" : "warning", "wallet"),
                new(T("Vé đã xuất", "Tickets issued"), report.VeDaXuat.ToString("N0", ViCulture), T($"Kỳ trước: {report.VeDaXuatKyTruoc} vé", $"Previous period: {report.VeDaXuatKyTruoc} tickets"), "success", "ticket"),
                new(T("Tỷ lệ hủy đặt chỗ", "Booking cancellation rate"), PercentText(cancelRate), T($"{canceled} / {bookings} đặt chỗ toàn hệ thống", $"{canceled} / {bookings} bookings system-wide"),
                    cancelRate > 5m ? "warning" : "success", "x")
            ],
            TrendSubtitle = T("6 tháng · Đơn vị: tỷ VND", "6 months · Unit: billion VND"),
            Bars = bars,
            AirlineSubtitle = T($"Kỳ báo cáo · Tổng đã thu {Money.Vnd(gross)}", $"Reporting period · Total collected {Money.Vnd(gross)}"),
            Airlines = airlines,
            RoutesSubtitle = T("Thu và hoàn theo ngày ghi nhận (UTC+7) · VND", "Collections and refunds by recording date (UTC+7) · VND"),
            Routes = routeRows,
            Flights = flightRows,
            Note = T("Thu và hoàn tính theo giao dịch thành công bằng VND. Đơn nhiều chặng được phân bổ theo giá và phí trên vé của từng chuyến.", "Totals use successful transactions in VND. Multi-segment bookings are allocated by the fare and fees of each flight ticket.")
        };
    }

    private static ManagerRouteRevenue FormatReportRow(string route, int tickets, decimal gross, decimal refunded, bool isTotal = false) =>
        new(route, tickets.ToString("N0", ViCulture), Money.Vnd(gross), Money.Vnd(refunded), Money.Vnd(gross - refunded), isTotal);


    private static bool IsAllValue(string? value, string allText) =>
        string.IsNullOrWhiteSpace(value)
        || string.Equals(value.Trim(), allText, StringComparison.OrdinalIgnoreCase)
        || value.Contains(AllText, StringComparison.OrdinalIgnoreCase);

    private static string GrowthText(decimal current, decimal previous)
    {
        if (previous <= 0)
        {
            return current > 0 ? T("Kỳ trước không có doanh thu", "No revenue in the previous period") : T("Chưa có doanh thu trong kỳ", "No revenue during this period");
        }

        var percent = (current - previous) * 100m / previous;
        return $"{(percent >= 0 ? "↑" : "↓")} {Math.Abs(percent).ToString("0.#", ViCulture)}% so với kỳ trước";
    }

    private static bool TryParsePeriod(
        string? value,
        out DateTimeOffset from,
        out DateTimeOffset toExclusive)
    {
        from = default;
        toExclusive = default;
        var text = (value ?? string.Empty).Trim();
        if (text.Length is 0 or > 42)
        {
            return false;
        }

        var dateMatches = Regex.Matches(text, @"\d{1,2}/\d{1,2}/\d{4}");
        if (dateMatches.Count is 1 or 2)
        {
            var first = dateMatches[0].Value;
            if (!FlightMapping.TryParseDate(first, out var startDate))
            {
                return false;
            }

            var endDate = startDate;
            if (dateMatches.Count >= 2)
            {
                if (!FlightMapping.TryParseDate(dateMatches[1].Value, out endDate))
                {
                    return false;
                }
            }

            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }
            if (startDate.Year <= 1 || endDate.Year >= 9999) return false;

            from = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), Vn);
            toExclusive = new DateTimeOffset(endDate.ToDateTime(TimeOnly.MinValue), Vn).AddDays(1);
            return true;
        }

        var yearMonth = Regex.Match(text, @"^(?<year>\d{4})[-/](?<month>\d{1,2})$");
        var monthYear = Regex.Match(text, @"^(?<month>\d{1,2})/(?<year>\d{4})$");
        var match = yearMonth.Success ? yearMonth : monthYear;
        if (match.Success
            && int.TryParse(match.Groups["year"].Value, out var year)
            && year is >= 2 and <= 9998
            && int.TryParse(match.Groups["month"].Value, out var month)
            && month is >= 1 and <= 12)
        {
            from = new DateTimeOffset(year, month, 1, 0, 0, 0, Vn);
            toExclusive = from.AddMonths(1);
            return true;
        }

        return false;
    }

    private static ManagerCancelFlightDialogViewModel BuildCancelDialog(ChuyenBayDTO flight, string dateText) => new()
    {
        Title = $"Xác nhận hủy chuyến {flight.SoHieu}?",
        Subtitle = $"Lịch bay & tồn chỗ / {flight.SoHieu} · {ChuyenBayBUS.HienThiTrangThai(ParseStatus(flight.TrangThai))}",
        FlightValue = $"{flight.SoHieu} · {FlightMapping.AirlineNameFor(flight.SoHieu)} · " +
                      $"{flight.MaSanBayDi} → {flight.MaSanBayDen}",
        DepartureValue = $"{FlightMapping.DisplayDate(flight.GioKhoiHanh)} · {FlightMapping.DisplayTime(flight.GioKhoiHanh)} (UTC+7)",
        SeatsValue = $"{flight.SoGheDaBan} / {flight.SoGheDangGiu}",
        ImpactTitle = $"Ảnh hưởng đến hành khách trên {flight.SoGheDaBan} ghế đã bán",
        MessageValue = $"Chuyến {flight.SoHieu} ngày {FlightMapping.DisplayDate(flight.GioKhoiHanh)} đã bị hủy. AeroViet sẽ hỗ trợ phương án cho đặt chỗ bị ảnh hưởng.",
        CancelHref = $"/quan-ly/lich-bay?ngay={Uri.EscapeDataString(dateText)}&chon={flight.Id}"
    };

    private static ManagerPriceDialogViewModel BuildPriceDialog(ChuyenBayDTO flight, string dateText)
    {
        var next = decimal.Round(flight.GiaCoBan * 1.1m, 0, MidpointRounding.AwayFromZero);
        var delta = next - flight.GiaCoBan;
        var percent = flight.GiaCoBan == 0 ? 0 : decimal.Round(delta / flight.GiaCoBan * 100, 0);

        return new ManagerPriceDialogViewModel
        {
            Subtitle = $"{flight.SoHieu} · {flight.MaSanBayDi} → {flight.MaSanBayDen} · " +
                       $"{dateText} · {FlightMapping.DisplayTime(flight.GioKhoiHanh)} (UTC+7)",
            CurrentValue = Money.Vnd(flight.GiaCoBan),
            NewValue = Money.Vnd(next),
            NewAmount = next,
            DeltaValue = $"+{Money.Vnd(delta)} / khách (+{percent}%)",
            CancelHref = $"/quan-ly/lich-bay?ngay={Uri.EscapeDataString(dateText)}&chon={flight.Id}"
        };
    }

    private static IReadOnlyList<ManagerSelectOption> BuildRouteOptions(
        IReadOnlyList<TuyenBayDTO> routes,
        int? routeId) =>
        new[] { new ManagerSelectOption(string.Empty, AllText, routeId is null) }
            .Concat(routes.Select(route => new ManagerSelectOption(
                $"{route.SanBayDi.MaSanBay}-{route.SanBayDen.MaSanBay}",
                $"{route.SanBayDi.MaSanBay} → {route.SanBayDen.MaSanBay}",
                route.Id == routeId)))
            .ToArray();

    private static IReadOnlyList<ManagerSelectOption> BuildStatusOptions(string? status)
    {
        var current = string.IsNullOrWhiteSpace(status) ? AllText : status;
        return new[] { new ManagerSelectOption(string.Empty, AllText, current == AllText) }
            .Concat(StatusChoices.Select(item => new ManagerSelectOption(item, item, item == current)))
            .ToArray();
    }

    private static int? ResolveRouteId(IReadOnlyList<TuyenBayDTO> routes, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == AllText)
        {
            return null;
        }

        var normalized = value
            .Replace("→", "-", StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        return routes.FirstOrDefault(route =>
                $"{route.SanBayDi.MaSanBay}-{route.SanBayDen.MaSanBay}" == normalized)?.Id
            ?? routes.FirstOrDefault(route =>
                normalized.Contains(route.SanBayDi.MaSanBay, StringComparison.Ordinal)
                && normalized.Contains(route.SanBayDen.MaSanBay, StringComparison.Ordinal))?.Id;
    }

    private static bool TryParseTimeRange(string? value, out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(['/', '-', '–', '→'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        return TimeSpan.TryParse(parts[0], CultureInfo.InvariantCulture, out start)
            && TimeSpan.TryParse(parts[1], CultureInfo.InvariantCulture, out end)
            && start >= TimeSpan.Zero && start < TimeSpan.FromDays(1)
            && end >= TimeSpan.Zero && end < TimeSpan.FromDays(1);
    }

    private static AirlineTicketing.Domain.TrangThaiChuyenBay ParseStatus(string value) =>
        Enum.TryParse<AirlineTicketing.Domain.TrangThaiChuyenBay>(value, ignoreCase: true, out var status)
            ? status
            : value.Trim().ToLowerInvariant() switch
            {
                "mở bán" or "đang mở bán" => AirlineTicketing.Domain.TrangThaiChuyenBay.DuKien,
                "đang bay" => AirlineTicketing.Domain.TrangThaiChuyenBay.DangBay,
                "hoàn tất" => AirlineTicketing.Domain.TrangThaiChuyenBay.HoanTat,
                "đã hủy" or "hủy" => AirlineTicketing.Domain.TrangThaiChuyenBay.BiHuy,
                _ => AirlineTicketing.Domain.TrangThaiChuyenBay.DuKien
            };

    private static string StatusTone(string status) => ParseStatus(status) switch
    {
        AirlineTicketing.Domain.TrangThaiChuyenBay.DuKien => "success",
        AirlineTicketing.Domain.TrangThaiChuyenBay.DangBay => "info",
        AirlineTicketing.Domain.TrangThaiChuyenBay.HoanTat => "neutral",
        _ => "warning"
    };

    private static int ParseInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : 0;

    private static decimal ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : 0;
    }
>>>>>>> Stashed changes
}
