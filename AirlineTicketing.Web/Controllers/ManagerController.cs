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
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Controllers;

[Authorize(Policy = "StaffOnly")]
public class ManagerController(AppDbContext dbContext, DatVeBUS datVeBUS) : Controller
{
    private const string DefaultDateText = "20/11/2026";
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
            attentionNotes.Add($"{canceledFlights} chuyến đã hủy, cần thông báo hành khách.");
        }

        foreach (var item in attentionFlights.Where(x => x.Remaining <= 10 && x.Flight.GioKhoiHanh > now).Take(2))
        {
            attentionNotes.Add($"{item.Flight.SoHieu}: còn {Math.Max(0, item.Remaining)} ghế.");
        }

        foreach (var item in attentionFlights.Where(x =>
                     x.Flight.GioKhoiHanh < now
                     && x.Flight.TrangThai == TrangThaiChuyenBay.DuKien).Take(2))
        {
            attentionNotes.Add($"{item.Flight.SoHieu}: đã qua giờ khởi hành nhưng chưa cập nhật trạng thái.");
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
            >= 6 and < 14 => "Ca sáng · 06:00–14:00",
            >= 14 and < 22 => "Ca chiều · 14:00–22:00",
            _ => "Ca đêm · 22:00–06:00"
        };

        var model = new ManagerOverviewViewModel
        {
            Subtitle = $"{Money.LongDate(nowVn)} · Theo dõi chuyến bay, đặt chỗ và yêu cầu trong ngày.",
            ShiftText = shift,
            UpdatedText = $"Cập nhật lúc {nowVn:HH:mm} · Giờ Việt Nam (UTC+7)",
            Kpis = new[]
            {
                new ManagerKpi(
                    "Chuyến bay hôm nay",
                    activeFlights.Count.ToString("N0", ViCulture),
                    $"{completedFlights} hoàn tất · {activeFlights.Count - completedFlights} chưa khởi hành" +
                    (canceledFlights > 0 ? $" · {canceledFlights} đã hủy" : string.Empty),
                    "info",
                    "plane"),
                new ManagerKpi(
                    "Đặt chỗ chờ thanh toán",
                    holds.Count.ToString("N0", ViCulture),
                    expiringSoon > 0
                        ? $"{expiringSoon} đơn hết hạn trong 10 phút"
                        : expiredHolds > 0
                            ? $"{expiredHolds} đơn đã quá hạn giữ chỗ"
                            : "Chưa có đơn sắp hết hạn",
                    expiringSoon > 0 || expiredHolds > 0 ? "warning" : "success",
                    "clock"),
                new ManagerKpi(
                    "Tỷ lệ lấp đầy hôm nay",
                    PercentText(loadFactor),
                    $"{occupiedSeats.ToString("N0", ViCulture)} / {capacity.ToString("N0", ViCulture)} ghế đã bán hoặc đang giữ",
                    "success",
                    "users"),
                new ManagerKpi(
                    "Yêu cầu cần xử lý",
                    pendingRequests.ToString("N0", ViCulture),
                    $"{pendingChanges} đổi vé · {pendingRefunds} hoàn vé",
                    pendingRequests > 0 ? "warning" : "success",
                    "inbox")
            },
            AttentionTitle = attentionNotes.Count > 0
                ? $"{attentionNotes.Count} điểm cần chú ý trong ca trực"
                : "Không có điểm cần chú ý trong ca trực",
            AttentionText = attentionNotes.Count > 0
                ? string.Join(" ", attentionNotes)
                : "Các chuyến trong ngày đang đúng lịch. Tiếp tục theo dõi đặt chỗ chờ thanh toán và yêu cầu đổi / hoàn.",
            FlightsSubtitle = $"Lịch khai thác ngày {dayStart:dd/MM/yyyy}",
            Flights = rows,
            Workload = new[]
            {
                new ManagerWorkloadRow(
                    "Đổi vé chờ duyệt",
                    $"{pendingChanges} yêu cầu",
                    changePercent,
                    pendingChanges > 0 ? "warning" : "neutral"),
                new ManagerWorkloadRow(
                    "Hoàn vé chờ duyệt",
                    $"{pendingRefunds} yêu cầu",
                    refundPercent,
                    pendingRefunds > 0 ? "success" : "neutral")
            },
            WorkloadDetailLabel = "Chờ đối soát thanh toán",
            WorkloadDetailValue = $"{pendingTransfers} giao dịch",
            Holds = holds.Take(5).Select(hold => new ManagerHoldRow(
                hold.MaDatVe,
                hold.KhachHang.HoTen,
                Money.Vnd(hold.TongTien),
                HoldExpiryText(hold.HetHanGiuCho, nowVn))).ToArray(),
            Checklist = new[]
            {
                new ManagerChecklistItem(
                    pendingTransfers == 0
                        ? "Đối soát thanh toán ca trước"
                        : $"Đối soát thanh toán ca trước (còn {pendingTransfers} giao dịch)",
                    pendingTransfers == 0),
                new ManagerChecklistItem("Kiểm tra lịch khai thác hôm nay", todayFlights.Count > 0),
                new ManagerChecklistItem(
                    pendingRequests == 0
                        ? "Xử lý yêu cầu đổi / hoàn"
                        : $"Xử lý {pendingRequests} yêu cầu đổi / hoàn",
                    pendingRequests == 0),
                new ManagerChecklistItem(
                    expiredHolds == 0
                        ? "Không còn đặt chỗ quá hạn giữ chỗ"
                        : $"Xử lý {expiredHolds} đặt chỗ đã quá hạn giữ chỗ",
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
                && (x.TrangThai == TrangThaiVe.DaPhatHanh || x.TrangThai == TrangThaiVe.TamGiu))
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
            TrangThaiChuyenBay.BiHuy => ("Đã hủy", "danger"),
            TrangThaiChuyenBay.HoanTat => ("Hoàn tất", "neutral"),
            TrangThaiChuyenBay.DangBay => ("Đang bay", "info"),
            _ when flight.GioKhoiHanh <= now && flight.GioHaCanh > now => ("Đang bay", "info"),
            _ when remaining <= 0 => ("Hết chỗ", "danger"),
            _ when remaining <= 10 => ($"Gần hết chỗ · còn {remaining}", "warning"),
            _ => ("Đúng lịch", "success")
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
            return $"Quá hạn {value:HH:mm}";
        }

        return value.Date == nowVn.Date
            ? $"{value:HH:mm} hôm nay"
            : value.Date == nowVn.Date.AddDays(1)
                ? $"{value:HH:mm} ngày mai"
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
            : FlightMapping.TryParseDate(DefaultDateText, out var fallback)
                ? fallback
                : DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
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
            Subtitle = "Quản lý lịch khai thác và giá cơ bản. Không thay đổi dữ liệu danh mục nền.",
            DateValue = dateText,
            RouteValue = routeId is null
                ? AllText
                : routes.FirstOrDefault(item => item.Id == routeId) is { } route
                    ? $"{route.SanBayDi.MaSanBay} → {route.SanBayDen.MaSanBay}"
                    : AllText,
            AirlineStatusValue = string.IsNullOrWhiteSpace(trangThai) ? AllText : trangThai,
            ListTitle = $"Lịch khai thác · {dateText}",
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
            ResultText = $"{flights.Count} chuyến trong ngày {dateText}",
            RouteOptions = BuildRouteOptions(routes, routeId),
            StatusOptions = BuildStatusOptions(trangThai),
            EditorRouteOptions = routes.Select(item => new ManagerSelectOption(
                item.Id.ToString(CultureInfo.InvariantCulture),
                $"{item.SanBayDi.MaSanBay} → {item.SanBayDen.MaSanBay}",
                item.Id == selected?.TuyenBayId)).ToArray(),
            EditorAircraftOptions = aircraft.Select(item => new ManagerSelectOption(
                item.Id.ToString(CultureInfo.InvariantCulture),
                $"{item.MaMayBay} · {item.TenMayBay} ({item.SoGhe} ghế)",
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
            EditorTitle = selected is null ? "Thêm lịch khai thác" : $"Chỉnh sửa chuyến · {selected.SoHieu}",
            EditorSubtitle = selected is null
                ? "Tạo chuyến mới cho tuyến và tàu bay đang khai thác"
                : $"{dateText} · {FlightMapping.AirlineNameFor(selected.SoHieu)} · Đã bán {selected.SoGheDaBan} ghế",
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
                        "Phổ thông",
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
        var date = FlightMapping.TryParseDate(form["ngay-bay"], out var parsedDate)
            ? parsedDate
            : DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
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

        var departure = new DateTimeOffset(date.ToDateTime(TimeOnly.FromTimeSpan(start)), ChuyenBayBUS.VietnamOffset);
        var arrival = new DateTimeOffset(date.ToDateTime(TimeOnly.FromTimeSpan(end)), ChuyenBayBUS.VietnamOffset);
        var price = ParseMoney(form["gia-pho-thong"]);
        var status = string.IsNullOrWhiteSpace(form["trang-thai"]) ? "Mở bán" : form["trang-thai"].ToString();

        var chuyenBay = FlightServices.ChuyenBay(dbContext);
        KetQuaXuLy result;

        if (id == 0)
        {
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

            result = await chuyenBay.CapNhatChuyenBayAsync(new CapNhatChuyenBayDTO
            {
                Id = id,
                SoHieu = string.IsNullOrWhiteSpace(form["so-hieu"]) ? current.SoHieu : form["so-hieu"].ToString(),
                TuyenBayId = current.TuyenBayId,
                MayBayId = current.MayBayId,
                GioKhoiHanh = departure,
                GioHaCanh = arrival,
                GiaCoBan = price,
                TrangThai = status
            }, cancellationToken);
        }

        TempData["ScheduleMessage"] = result.ThanhCong ? "Đã lưu lịch khai thác." : result.Loi;
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
        var chuyenBay = FlightServices.ChuyenBay(dbContext);
        var result = await chuyenBay.HuyChuyenBayAsync(id, cancellationToken);

        TempData["ScheduleMessage"] = result.ThanhCong ? "Đã hủy chuyến và dừng mở bán." : result.Loi;
        TempData["ScheduleTone"] = result.ThanhCong ? "success" : "danger";

        return RedirectToAction(nameof(Schedules), new { ngay = form["ngay"].ToString() });
    }

    [HttpPost("/quan-ly/lich-bay/doi-gia")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePrice(IFormCollection form, CancellationToken cancellationToken)
    {
        var id = ParseInt(form["chuyen-bay-id"]);
        var price = ParseMoney(form["gia-moi"]);
        var chuyenBay = FlightServices.ChuyenBay(dbContext);
        var result = await chuyenBay.DoiGiaAsync(id, price, cancellationToken);

        TempData["ScheduleMessage"] = result.ThanhCong ? "Đã cập nhật giá cơ bản." : result.Loi;
        TempData["ScheduleTone"] = result.ThanhCong ? "success" : "danger";

        return RedirectToAction(nameof(Schedules), new { ngay = form["ngay"].ToString(), chon = id });
    }

    [HttpGet("/quan-ly/dat-ve")]
    public async Task<IActionResult> Bookings(string? q, CancellationToken cancellationToken) =>
        View(new BookingAdminViewModel
        {
            TuKhoa = q,
            DatVes = await datVeBUS.DanhSachQuanLyAsync(q, cancellationToken: cancellationToken),
            Message = TempData["BookingMessage"] as string,
            MessageTone = TempData["BookingTone"] as string ?? "info"
        });

    [HttpPost("/quan-ly/dat-ve")]
    [ValidateAntiForgeryToken]
    public IActionResult Bookings(IFormCollection form) => RedirectToAction(nameof(Bookings));

    /// <summary>
    /// Nhân viên đối soát sao kê ngân hàng rồi xác nhận thủ công. Đây là bước duy nhất
    /// được phép chuyển một đặt chỗ chuyển khoản sang trạng thái đã thanh toán.
    /// </summary>
    [HttpPost("/quan-ly/dat-ve/xac-nhan-thanh-toan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmTransferPayment(IFormCollection form, CancellationToken cancellationToken)
    {
        var pnr = form["pnr"].ToString().Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(pnr))
        {
            return BookingMessage("Mã đặt chỗ không hợp lệ.", "danger");
        }

        var booking = await dbContext.DatVes
            .Include(x => x.Ves)
            .Include(x => x.ThanhToans)
            .Include(x => x.LichSus)
            .FirstOrDefaultAsync(x => x.MaDatVe == pnr, cancellationToken);

        if (booking is null)
        {
            return BookingMessage($"Không tìm thấy đặt chỗ {pnr}.", "danger");
        }

        if (booking.TrangThai == TrangThaiDatVe.DaXacNhan)
        {
            return BookingMessage($"Đặt chỗ {pnr} đã được xác nhận trước đó.", "info");
        }

        if (booking.TrangThai != TrangThaiDatVe.ChoThanhToan)
        {
            return BookingMessage($"Đặt chỗ {pnr} không còn chờ thanh toán (trạng thái {booking.TrangThai}).", "danger");
        }

        var now = DateTimeOffset.UtcNow;
        if (booking.HetHanGiuCho <= now)
        {
            var previous = booking.TrangThai;
            booking.TrangThai = TrangThaiDatVe.HetHan;
            foreach (var ticket in booking.Ves.Where(x => x.TrangThai == TrangThaiVe.TamGiu))
            {
                ticket.TrangThai = TrangThaiVe.DaHuy;
                ticket.NgayHuy = now;
                ticket.LyDoHuy = "Hết thời hạn giữ chỗ";
            }
            booking.LichSus.Add(new LichSuDatVe
            {
                DatVeId = booking.Id,
                HanhDong = "HetHanGiuCho",
                TrangThaiTruoc = previous.ToString(),
                TrangThaiSau = booking.TrangThai.ToString(),
                LyDo = "Hết thời hạn giữ chỗ khi nhân viên đối soát",
                NguoiThucHien = User.DisplayName(),
                ThoiDiem = now
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return BookingMessage($"Đặt chỗ {pnr} đã hết hạn giữ chỗ, không thể xác nhận thanh toán.", "danger");
        }

        var payment = booking.ThanhToans.FirstOrDefault(x =>
            x.PhuongThuc == PhuongThucThanhToan.ChuyenKhoan
            && x.TrangThai == TrangThaiThanhToan.ChoXuLy);

        if (payment is null)
        {
            return BookingMessage($"Đặt chỗ {pnr} không có giao dịch chuyển khoản đang chờ đối soát.", "danger");
        }

        payment.TrangThai = TrangThaiThanhToan.ThanhCong;
        payment.NgayHoanTat = now;

        foreach (var ticket in booking.Ves.Where(x => x.TrangThai == TrangThaiVe.TamGiu))
        {
            ticket.TrangThai = TrangThaiVe.DaPhatHanh;
            ticket.MaVe = NewTicketNumber();
            ticket.NgayPhatHanh = now;
        }

        var oldStatus = booking.TrangThai;
        booking.TrangThai = TrangThaiDatVe.DaXacNhan;
        booking.NgayXacNhan = now;
        booking.LichSus.Add(new LichSuDatVe
        {
            DatVeId = booking.Id,
            HanhDong = "XacNhanChuyenKhoan",
            TrangThaiTruoc = oldStatus.ToString(),
            TrangThaiSau = booking.TrangThai.ToString(),
            LyDo = "Nhân viên đối soát xác nhận đã nhận chuyển khoản",
            NguoiThucHien = User.DisplayName(),
            ThoiDiem = now,
            DuLieuBoSung = JsonSerializer.Serialize(new { booking.MaDatVe, payment.MaGiaoDich })
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return BookingMessage($"Đã xác nhận thanh toán và phát hành vé cho đặt chỗ {pnr}.", "success");
    }

    private IActionResult BookingMessage(string message, string tone)
    {
        TempData["BookingMessage"] = message;
        TempData["BookingTone"] = tone;
        return RedirectToAction(nameof(Bookings));
    }

    private static string NewTicketNumber() =>
        "VE" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    [HttpGet("/quan-ly/doi-hoan")]
    public async Task<IActionResult> ChangeRefunds(
        string? tab,
        int? id,
        string? q,
        string? loai,
        string? ngay,
        [FromQuery(Name = "hop-thoai")] string? hopThoai,
        CancellationToken cancellationToken)
    {
        var activeTab = NormalizeRefundTab(tab);
        var statusCounts = await dbContext.YeuCauDoiHoans.AsNoTracking()
            .GroupBy(x => x.TrangThai)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

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
            .Include(x => x.DatVe).ThenInclude(x => x.ThanhToans)
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
                $"{item.MaYeuCau} · {item.DatVe.MaDatVe}",
                item.DatVe.KhachHang.HoTen,
                item.LoaiYeuCau == LoaiYeuCau.HoanVe ? "Hoàn vé" : "Đổi vé",
                item.NgayTao.ToOffset(Vn).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                statusText,
                statusTone,
                selected != null && item.Id == selected.Id,
                item.Id,
                $"/quan-ly/doi-hoan?tab={activeTab}&id={item.Id}");
        }).ToArray();

        var model = new ManagerChangeRefundsViewModel
        {
            Tabs = new[]
            {
                new ManagerQueueTab($"Tất cả ({total})", "/quan-ly/doi-hoan", activeTab == "all"),
                new ManagerQueueTab($"Chờ duyệt ({pending})", "/quan-ly/doi-hoan?tab=cho-duyet", activeTab == "cho-duyet"),
                new ManagerQueueTab($"Đã duyệt ({approved})", "/quan-ly/doi-hoan?tab=da-duyet", activeTab == "da-duyet"),
                new ManagerQueueTab($"Hoàn tất ({completed})", "/quan-ly/doi-hoan?tab=hoan-tat", activeTab == "hoan-tat")
            },
            QueueSubtitle = $"Chờ duyệt: {pendingChanges} đổi vé · {pendingRefunds} hoàn vé",
            Rows = rows,
            ResultText = rows.Length == 0
                ? "Không có yêu cầu phù hợp bộ lọc"
                : $"Hiển thị {rows.Length} yêu cầu",
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

        if (request.LoaiYeuCau == LoaiYeuCau.HoanVe)
        {
            var payment = booking.ThanhToans
                .Where(x => x.TrangThai == TrangThaiThanhToan.ThanhCong)
                .OrderByDescending(x => x.NgayTao)
                .FirstOrDefault();

            if (payment == null)
            {
                return RefundMessage(
                    $"Đặt chỗ {booking.MaDatVe} chưa có giao dịch thanh toán thành công để hoàn tiền.",
                    "danger",
                    "cho-duyet",
                    request.Id);
            }

            var amount = request.SoTienHoan;
            if (amount <= 0)
            {
                amount = Math.Max(0m,
                    booking.TongTien
                    - request.PhiHoan
                    - request.PhiDichVuKhongHoan
                    - request.PhiHanhLyKhongHoan);
            }

            request.SoTienHoan = amount;
            dbContext.HoanTiens.Add(new HoanTien
            {
                ThanhToanId = payment.Id,
                SoTien = amount,
                DonViTienTe = payment.DonViTienTe,
                NhaCungCap = payment.NhaCungCap,
                MaGiaoDich = payment.MaGiaoDich,
                LyDo = Truncate(request.LyDo, 300),
                TrangThai = TrangThaiHoanTien.ChoXuLy,
                NgayTao = now
            });
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

        return RefundMessage(
            request.LoaiYeuCau == LoaiYeuCau.HoanVe
                ? $"Đã duyệt hoàn {request.MaYeuCau}. Lệnh hoàn {Money.Vnd(request.SoTienHoan)} đang chờ chi trả."
                : $"Đã duyệt yêu cầu đổi vé {request.MaYeuCau}.",
            "success",
            "da-duyet",
            request.Id);
    }

    [HttpPost("/quan-ly/doi-hoan/tu-choi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRefund(IFormCollection form, CancellationToken cancellationToken)
    {
        var id = ParseInt(form["yeu-cau-id"]);
        var reason = NormalizeReason(form["ly-do"].ToString(), "Không đáp ứng điều kiện đổi / hoàn vé.");

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
            .Include(x => x.DatVe).ThenInclude(x => x.KhachHang)
            .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.HanhKhach)
            .Include(x => x.DatVe).ThenInclude(x => x.ThanhToans)
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
        TrangThaiYeuCau.ChoDuyet => ("Chờ duyệt", "warning"),
        TrangThaiYeuCau.DaDuyet => type == LoaiYeuCau.HoanVe
            ? ("Đã duyệt · Chờ hoàn tiền", "info")
            : ("Đã duyệt · Chờ đổi vé", "info"),
        TrangThaiYeuCau.HoanTat => ("Hoàn tất", "success"),
        TrangThaiYeuCau.TuChoi => ("Từ chối", "danger"),
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
        var paid = booking.ThanhToans
            .Where(x => x.TrangThai == TrangThaiThanhToan.ThanhCong)
            .Sum(x => x.SoTien);
        if (paid <= 0)
        {
            paid = booking.TongTien;
        }

        model.DetailTitle = $"Chi tiết yêu cầu · {request.MaYeuCau}";
        model.DetailSubtitle =
            $"{booking.KhachHang.HoTen} · {booking.MaDatVe} · Vé {(string.IsNullOrWhiteSpace(ve?.MaVe) ? "chưa phát hành" : ve.MaVe)}";
        model.DetailFields = new[]
        {
            new AdminDetailField("Chuyến bay gốc", flight == null ? "—" : $"{flight.SoHieu} · {route}"),
            new AdminDetailField("Khởi hành", flight == null
                ? "—"
                : $"{FlightMapping.DisplayDate(flight.GioKhoiHanh)} · {FlightMapping.DisplayTime(flight.GioKhoiHanh)} (UTC+7)"),
            new AdminDetailField("Loại vé", string.IsNullOrWhiteSpace(ve?.HangVe) ? "Phổ thông" : ve.HangVe),
            new AdminDetailField("Lý do của khách", request.LyDo),
            new AdminDetailField("Thông tin bổ sung", string.IsNullOrWhiteSpace(request.ThongTinBoSung) ? "—" : request.ThongTinBoSung)
        };

        model.IsRefundRequest = request.LoaiYeuCau == LoaiYeuCau.HoanVe;
        model.CanDecide = request.TrangThai == TrangThaiYeuCau.ChoDuyet;
        if (request.LoaiYeuCau == LoaiYeuCau.HoanVe)
        {
            model.ProcessTitle = $"Quy trình hoàn vé · {pendingRefunds} yêu cầu đang chờ";
            model.ProcessText =
                "Kiểm tra điều kiện vé → xác nhận phí hoàn và phần không hoàn → duyệt để tạo lệnh hoàn đang chờ chi trả → " +
                "chỉ ghi nhận hoàn tất khi cổng thanh toán xác nhận. " +
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
                : Math.Max(0m, paid - request.PhiHoan - request.PhiDichVuKhongHoan - request.PhiHanhLyKhongHoan);

            model.RefundTitle = "Tính hoàn tiền dự kiến";
            model.RefundRows = new[]
            {
                new ManagerRefundRow("Tổng đã thanh toán", Money.Vnd(paid)),
                new ManagerRefundRow("Phí hoàn vé", "−" + Money.Vnd(request.PhiHoan)),
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
            var newFare = newFlight?.GiaCoBan ?? oldFare;
            var difference = Math.Max(0m, newFare - oldFare) + request.PhiDoi;

            model.RefundTitle = "Chi phí đổi vé dự kiến";
            model.RefundRows = new[]
            {
                new ManagerRefundRow("Tổng đã thanh toán", Money.Vnd(paid)),
                new ManagerRefundRow("Phí đổi vé", Money.Vnd(request.PhiDoi)),
                new ManagerRefundRow("Giá vé chuyến mới", Money.Vnd(newFare)),
                new ManagerRefundRow("Chênh lệch dự kiến", Money.Vnd(difference), true)
            };
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
            TrangThaiYeuCau.DaDuyet => $"{statusText} — đã ghi nhận xử lý",
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
                : Math.Max(0m, paid - request.PhiHoan - request.PhiDichVuKhongHoan - request.PhiHanhLyKhongHoan);
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
            MaNhatKy = $"LOG-{at:yyyyMMddHHmmssfff}-{Random.Shared.Next(100, 999)}",
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
        CancellationToken cancellationToken) =>
        View(await BuildReportsAsync(ky, hang, tuyen, cancellationToken));

    [HttpPost("/quan-ly/bao-cao")]
    [ValidateAntiForgeryToken]
    public IActionResult Reports(IFormCollection form) =>
        RedirectToAction(nameof(Reports), new
        {
            ky = form["ky"].ToString(),
            hang = form["hang"].ToString(),
            tuyen = form["tuyen"].ToString()
        });

    [HttpPost("/quan-ly/bao-cao/xuat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportReport(IFormCollection form, CancellationToken cancellationToken)
    {
        var model = await BuildReportsAsync(
            form["ky"].ToString(),
            form["hang"].ToString(),
            form["tuyen"].ToString(),
            cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Bao cao kinh doanh - AeroViet");
        sb.AppendLine($"Ky,{Csv(model.PeriodValue)}");
        sb.AppendLine($"Hang,{Csv(model.AirlineValue)}");
        sb.AppendLine($"Tuyen,{Csv(model.RouteValue)}");
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

    private static string Csv(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

    private async Task<ManagerReportsViewModel> BuildReportsAsync(
        string? periodText,
        string? airlineText,
        string? routeText,
        CancellationToken cancellationToken)
    {
        var nowVn = DateTimeOffset.UtcNow.ToOffset(Vn);
        var fallbackStart = new DateTimeOffset(nowVn.Year, nowVn.Month, 1, 0, 0, 0, Vn);
        var from = fallbackStart;
        var toExclusive = fallbackStart.AddMonths(1);
        if (TryParsePeriod(periodText, out var parsedFrom, out var parsedTo) && parsedTo > parsedFrom)
        {
            from = parsedFrom;
            toExclusive = parsedTo;
        }

        var periodValue = $"{from.ToOffset(Vn):dd/MM/yyyy} – {toExclusive.AddDays(-1).ToOffset(Vn):dd/MM/yyyy}";
        var airlineFilter = IsAllValue(airlineText, AllAirlines) ? null : airlineText!.Trim();
        var routeFilter = IsAllValue(routeText, AllRoutes) ? null : routeText!.Trim();

        var airlineNames = await dbContext.HangHangKhongS.AsNoTracking()
            .Where(x => x.DangHoatDong)
            .OrderBy(x => x.TenHang)
            .Select(x => x.TenHang)
            .ToListAsync(cancellationToken);
        var routes = await dbContext.TuyenBays.AsNoTracking()
            .Include(x => x.SanBayDi)
            .Include(x => x.SanBayDen)
            .Where(x => x.DangKhaiThac)
            .OrderBy(x => x.SanBayDi.MaSanBay)
            .ThenBy(x => x.SanBayDen.MaSanBay)
            .ToListAsync(cancellationToken);
        var routeLabels = routes.Select(RouteLabel).Distinct(StringComparer.Ordinal).ToArray();

        var periodPayments = await LoadPaymentFactsAsync(from, toExclusive, cancellationToken);
        var periodRefunds = await LoadRefundFactsAsync(from, toExclusive, cancellationToken);
        var periodTickets = await LoadIssuedTicketFactsAsync(from, toExclusive, cancellationToken);

        var filteredPayments = periodPayments
            .Where(x => airlineFilter == null || x.Airline == airlineFilter)
            .Where(x => routeFilter == null || x.Route == routeFilter)
            .ToList();
        var filteredRefunds = periodRefunds
            .Where(x => airlineFilter == null || x.Airline == airlineFilter)
            .Where(x => routeFilter == null || x.Route == routeFilter)
            .ToList();
        var filteredTickets = periodTickets
            .Where(x => airlineFilter == null || x.Airline == airlineFilter)
            .Where(x => routeFilter == null || x.Route == routeFilter)
            .ToList();

        var gross = filteredPayments.Sum(x => x.Amount);
        var duration = toExclusive - from;
        var previousFrom = from - duration;
        var previousPayments = await LoadPaymentFactsAsync(previousFrom, from, cancellationToken);
        var previousGross = previousPayments
            .Where(x => airlineFilter == null || x.Airline == airlineFilter)
            .Where(x => routeFilter == null || x.Route == routeFilter)
            .Sum(x => x.Amount);
        var previousTickets = await LoadIssuedTicketFactsAsync(previousFrom, from, cancellationToken);
        var previousIssued = previousTickets
            .Where(x => airlineFilter == null || x.Airline == airlineFilter)
            .Where(x => routeFilter == null || x.Route == routeFilter)
            .Count();

        var periodBookings = await dbContext.DatVes.AsNoTracking()
            .Where(x => x.NgayDat >= from.ToUniversalTime() && x.NgayDat < toExclusive.ToUniversalTime())
            .Select(x => x.TrangThai)
            .ToListAsync(cancellationToken);
        var canceledBookings = periodBookings.Count(x =>
            x is TrangThaiDatVe.DaHuy or TrangThaiDatVe.HetHan);
        var cancelRate = periodBookings.Count == 0
            ? 0m
            : canceledBookings * 100m / periodBookings.Count;

        var refundRate = filteredTickets.Count == 0
            ? 0m
            : filteredRefunds.Count * 100m / filteredTickets.Count;

        var monthStart = new DateTimeOffset(from.Year, from.Month, 1, 0, 0, 0, Vn);
        var trendMonths = Enumerable.Range(0, 6)
            .Select(offset => monthStart.AddMonths(offset - 5))
            .ToArray();
        var trendTo = monthStart.AddMonths(1);
        var trendPayments = await LoadPaymentFactsAsync(trendMonths[0], trendTo, cancellationToken);
        var filteredTrend = trendPayments
            .Where(x => airlineFilter == null || x.Airline == airlineFilter)
            .Where(x => routeFilter == null || x.Route == routeFilter)
            .ToList();
        var bars = trendMonths.Select(month =>
        {
            var amount = filteredTrend
                .Where(x => x.OccurredAt.ToOffset(Vn).Year == month.Year
                    && x.OccurredAt.ToOffset(Vn).Month == month.Month)
                .Sum(x => x.Amount);
            var billions = amount / 1_000_000_000m;
            return new ManagerRevenueBar(
                $"T{month:MM}",
                billions.ToString("0.0#", ViCulture),
                billions,
                month.Year == monthStart.Year && month.Month == monthStart.Month);
        }).ToArray();

        var airlineGroups = filteredPayments
            .GroupBy(x => x.Airline)
            .Select(group => new { Name = group.Key, Amount = group.Sum(x => x.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToArray();
        var airlineTotal = airlineGroups.Sum(x => x.Amount);
        var tones = new[] { "primary", "success", "muted", "info" };
        var airlines = airlineGroups.Select((group, index) =>
        {
            var share = airlineTotal <= 0 ? 0m : group.Amount * 100m / airlineTotal;
            return new ManagerAirlineRevenue(
                group.Name,
                Money.Vnd(group.Amount),
                airlineTotal <= 0 ? "0% tổng doanh thu" : $"{PercentText(share)} tổng doanh thu",
                (int)Math.Round(share, MidpointRounding.AwayFromZero),
                tones[index % tones.Length]);
        }).ToArray();

        var routeNames = filteredPayments.Select(x => x.Route)
            .Concat(filteredRefunds.Select(x => x.Route))
            .Concat(filteredTickets.Select(x => x.Route))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        var routeRows = routeNames.Select(route =>
        {
            var tickets = filteredTickets.Count(x => x.Route == route);
            var routeGross = filteredPayments.Where(x => x.Route == route).Sum(x => x.Amount);
            var routeRefunded = filteredRefunds.Where(x => x.Route == route).Sum(x => x.Amount);
            return new ManagerRouteRevenue(
                route,
                tickets.ToString("N0", ViCulture),
                Money.Vnd(routeGross),
                Money.Vnd(routeRefunded),
                Money.Vnd(routeGross - routeRefunded));
        }).ToList();

        if (routeRows.Count > 0)
        {
            routeRows.Add(new ManagerRouteRevenue(
                "Tổng cộng",
                filteredTickets.Count.ToString("N0", ViCulture),
                Money.Vnd(gross),
                Money.Vnd(filteredRefunds.Sum(x => x.Amount)),
                Money.Vnd(gross - filteredRefunds.Sum(x => x.Amount)),
                true));
        }

        var issuedCount = filteredTickets.Count;

        return new ManagerReportsViewModel
        {
            PeriodValue = periodValue,
            AirlineValue = airlineFilter ?? AllAirlines,
            RouteValue = routeFilter ?? AllRoutes,
            AirlineOptions = new[] { new ManagerSelectOption(string.Empty, AllAirlines, airlineFilter == null) }
                .Concat(airlineNames.Select(name => new ManagerSelectOption(name, name, name == airlineFilter)))
                .ToArray(),
            RouteOptions = new[] { new ManagerSelectOption(string.Empty, AllRoutes, routeFilter == null) }
                .Concat(routeLabels.Select(label => new ManagerSelectOption(label, label, label == routeFilter)))
                .ToArray(),
            Kpis = new[]
            {
                new ManagerKpi(
                    "Doanh thu gộp",
                    Money.Vnd(gross),
                    GrowthText(gross, previousGross),
                    gross >= previousGross ? "success" : "warning",
                    "chart"),
                new ManagerKpi(
                    "Vé đã xuất",
                    issuedCount.ToString("N0", ViCulture),
                    previousIssued == 0
                        ? "Tính theo Ngày phát hành trong kỳ"
                        : $"Kỳ trước: {previousIssued} vé",
                    "success",
                    "ticket"),
                new ManagerKpi(
                    "Tỷ lệ hủy đặt chỗ",
                    PercentText(cancelRate),
                    $"{canceledBookings} / {periodBookings.Count} đặt chỗ tạo trong kỳ",
                    cancelRate > 5m ? "warning" : "success",
                    "x"),
                new ManagerKpi(
                    "Tỷ lệ vé hoàn",
                    PercentText(refundRate),
                    $"{filteredRefunds.Count} giao dịch / {issuedCount} vé đã xuất",
                    filteredRefunds.Count > 0 ? "warning" : "success",
                    "undo")
            },
            TrendSubtitle = $"6 tháng gần nhất · Đơn vị: tỷ VND",
            Bars = bars,
            AirlineSubtitle = $"Kỳ báo cáo · Tổng {Money.Vnd(airlineTotal)}",
            Airlines = airlines,
            RoutesSubtitle = "Doanh thu vé và dịch vụ trước hoàn tiền · Đơn vị tiền tệ: VND",
            Routes = routeRows,
            Note =
                $"Cơ sở: {periodBookings.Count} đặt chỗ tạo trong kỳ, {canceledBookings} đơn hủy / hết hạn; " +
                $"{issuedCount} vé đã xuất, {filteredRefunds.Count} giao dịch hoàn thành công. " +
                "Tiền hoàn chỉ tính giao dịch đã xác nhận thành công; doanh thu theo tuyến lấy theo vé đầu tiên của đặt chỗ."
        };
    }

    private async Task<List<PaymentFact>> LoadPaymentFactsAsync(
        DateTimeOffset from,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken)
    {
        var fromUtc = from.ToUniversalTime();
        var toUtc = toExclusive.ToUniversalTime();
        var rows = await dbContext.ThanhToans.AsNoTracking()
            .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.HangHangKhong)
            .Where(x => x.TrangThai == TrangThaiThanhToan.ThanhCong
                && x.NgayTao >= fromUtc
                && x.NgayTao < toUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(payment =>
        {
            var ve = payment.DatVe.Ves.OrderBy(x => x.Id).FirstOrDefault();
            return new PaymentFact(
                payment.NgayTao,
                payment.SoTien,
                RouteOf(ve),
                AirlineOf(ve));
        }).ToList();
    }

    private async Task<List<RefundFact>> LoadRefundFactsAsync(
        DateTimeOffset from,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken)
    {
        var fromUtc = from.ToUniversalTime();
        var toUtc = toExclusive.ToUniversalTime();
        var rows = await dbContext.HoanTiens.AsNoTracking()
            .Include(x => x.ThanhToan).ThenInclude(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.ThanhToan).ThenInclude(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .Include(x => x.ThanhToan).ThenInclude(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.HangHangKhong)
            .Where(x => x.TrangThai == TrangThaiHoanTien.ThanhCong
                && x.NgayHoanTat != null
                && x.NgayHoanTat >= fromUtc
                && x.NgayHoanTat < toUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(refund =>
        {
            var ve = refund.ThanhToan.DatVe.Ves.OrderBy(x => x.Id).FirstOrDefault();
            return new RefundFact(
                refund.NgayHoanTat ?? refund.NgayTao,
                refund.SoTien,
                RouteOf(ve),
                AirlineOf(ve));
        }).ToList();
    }

    private async Task<List<TicketFact>> LoadIssuedTicketFactsAsync(
        DateTimeOffset from,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken)
    {
        var fromUtc = from.ToUniversalTime();
        var toUtc = toExclusive.ToUniversalTime();
        var rows = await dbContext.Ves.AsNoTracking()
            .Include(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .Include(x => x.ChuyenBay).ThenInclude(x => x.HangHangKhong)
            .Where(x => x.NgayPhatHanh != null
                && x.NgayPhatHanh >= fromUtc
                && x.NgayPhatHanh < toUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(ve => new TicketFact(RouteOf(ve), AirlineOf(ve))).ToList();
    }

    private static string RouteOf(Ve? ve) =>
        ve?.ChuyenBay?.TuyenBay is { } route
            ? RouteLabel(route)
            : "Khác";

    private static string AirlineOf(Ve? ve) =>
        ve?.ChuyenBay?.HangHangKhong?.TenHang
        ?? (ve?.ChuyenBay != null ? FlightMapping.AirlineNameFor(ve.ChuyenBay.SoHieu) : "Khác");

    private static string RouteLabel(TuyenBay route) =>
        $"{route.SanBayDi.MaSanBay} ↔ {route.SanBayDen.MaSanBay}";

    private static bool IsAllValue(string? value, string allText) =>
        string.IsNullOrWhiteSpace(value)
        || string.Equals(value.Trim(), allText, StringComparison.OrdinalIgnoreCase)
        || value.Contains(AllText, StringComparison.OrdinalIgnoreCase);

    private static string GrowthText(decimal current, decimal previous)
    {
        if (previous <= 0)
        {
            return current > 0 ? "Kỳ trước không có doanh thu" : "Chưa có doanh thu trong kỳ";
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
        if (text.Length == 0)
        {
            return false;
        }

        var dateMatches = Regex.Matches(text, @"\d{1,2}/\d{1,2}/\d{4}");
        if (dateMatches.Count >= 1)
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

            from = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), Vn);
            toExclusive = new DateTimeOffset(endDate.ToDateTime(TimeOnly.MinValue), Vn).AddDays(1);
            return true;
        }

        var yearMonth = Regex.Match(text, @"^(?<year>\d{4})[-/](?<month>\d{1,2})$");
        var monthYear = Regex.Match(text, @"^(?<month>\d{1,2})/(?<year>\d{4})$");
        var match = yearMonth.Success ? yearMonth : monthYear;
        if (match.Success
            && int.TryParse(match.Groups["year"].Value, out var year)
            && int.TryParse(match.Groups["month"].Value, out var month)
            && month is >= 1 and <= 12)
        {
            from = new DateTimeOffset(year, month, 1, 0, 0, 0, Vn);
            toExclusive = from.AddMonths(1);
            return true;
        }

        return false;
    }

    private sealed record PaymentFact(DateTimeOffset OccurredAt, decimal Amount, string Route, string Airline);

    private sealed record RefundFact(DateTimeOffset OccurredAt, decimal Amount, string Route, string Airline);

    private sealed record TicketFact(string Route, string Airline);

    private static ManagerCancelFlightDialogViewModel BuildCancelDialog(ChuyenBayDTO flight, string dateText) => new()
    {
        Title = $"Xác nhận hủy chuyến {flight.SoHieu}?",
        Subtitle = $"Lịch bay & tồn chỗ / {flight.SoHieu} · {ChuyenBayBUS.HienThiTrangThai(ParseStatus(flight.TrangThai))}",
        FlightValue = $"{flight.SoHieu} · {FlightMapping.AirlineNameFor(flight.SoHieu)} · " +
                      $"{flight.MaSanBayDi} → {flight.MaSanBayDen}",
        DepartureValue = $"{dateText} · {FlightMapping.DisplayTime(flight.GioKhoiHanh)} (UTC+7)",
        SeatsValue = $"{flight.SoGheDaBan} / {flight.SoGheDangGiu}",
        ImpactTitle = $"Ảnh hưởng đến hành khách trên {flight.SoGheDaBan} ghế đã bán",
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
        if (parts.Length < 2)
        {
            return false;
        }

        return TimeSpan.TryParse(parts[0], CultureInfo.InvariantCulture, out start)
            && TimeSpan.TryParse(parts[1], CultureInfo.InvariantCulture, out end);
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
}
