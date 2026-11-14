using System.Globalization;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.Demo;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public class ManagerController(AppDbContext dbContext, DatVeBUS datVeBUS) : Controller
{
    private const string DefaultDateText = "20/11/2026";
    private const string AllText = "Tất cả";

    private static readonly string[] StatusChoices = ["Mở bán", "Đang bay", "Hoàn tất", "Đã hủy"];

    [HttpGet("/quan-ly")]
    public IActionResult Index() => View(DemoCatalog.ManagerOverview());

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
            DatVes = await datVeBUS.DanhSachQuanLyAsync(q, cancellationToken: cancellationToken)
        });

    [HttpPost("/quan-ly/dat-ve")]
    [ValidateAntiForgeryToken]
    public IActionResult Bookings(IFormCollection form) => RedirectToAction(nameof(Bookings));

    [HttpGet("/quan-ly/doi-hoan")]
    public IActionResult ChangeRefunds(
        string? tab,
        [FromQuery(Name = "hop-thoai")] string? hopThoai) =>
        View(DemoCatalog.ManagerChangeRefunds(tab, hopThoai));

    [HttpPost("/quan-ly/doi-hoan/duyet")]
    [ValidateAntiForgeryToken]
    public IActionResult ApproveRefund(IFormCollection form) => RedirectToAction(nameof(ChangeRefunds));

    [HttpPost("/quan-ly/doi-hoan/tu-choi")]
    [ValidateAntiForgeryToken]
    public IActionResult RejectRefund(IFormCollection form) => RedirectToAction(nameof(ChangeRefunds));

    [HttpGet("/quan-ly/bao-cao")]
    public IActionResult Reports() => View(DemoCatalog.ManagerReports());

    [HttpPost("/quan-ly/bao-cao")]
    [ValidateAntiForgeryToken]
    public IActionResult Reports(IFormCollection form) => RedirectToAction(nameof(Reports));

    [HttpPost("/quan-ly/bao-cao/xuat")]
    [ValidateAntiForgeryToken]
    public IActionResult ExportReport(IFormCollection form) => RedirectToAction(nameof(Reports));

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
