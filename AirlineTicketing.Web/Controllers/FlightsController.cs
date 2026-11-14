using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Controllers;

public class FlightsController(AppDbContext dbContext) : Controller
{
    private const string DefaultFrom = "TP. Hồ Chí Minh (SGN)";
    private const string DefaultTo = "Hà Nội (HAN)";

    [HttpGet("/chuyen-bay")]
    public async Task<IActionResult> Index(
        string? from,
        string? to,
        string? depart,
        string? passengers,
        string? cabin,
        string? trip,
        CancellationToken cancellationToken)
    {
        var fromText = string.IsNullOrWhiteSpace(from) ? DefaultFrom : from.Trim();
        var toText = string.IsNullOrWhiteSpace(to) ? DefaultTo : to.Trim();
        var departText = string.IsNullOrWhiteSpace(depart) ? string.Empty : depart.Trim();
        var passengerCount = FlightMapping.ParsePassengers(passengers);
        var cabinText = FlightMapping.ParseCabin(cabin);
        var tripText = FlightMapping.ParseTrip(trip);

        var danhMuc = FlightServices.DanhMuc(dbContext);
        var sanBayDi = await danhMuc.TimSanBayTheoTuKhoaAsync(fromText, cancellationToken);
        var sanBayDen = await danhMuc.TimSanBayTheoTuKhoaAsync(toText, cancellationToken);

        var date = FlightMapping.TryParseDate(departText, out var parsedDate)
            ? parsedDate
            : await ResolveDefaultDateAsync(cancellationToken);

        var criteria = new FlightSearchCriteria
        {
            OriginCity = sanBayDi?.ThanhPho ?? fromText,
            OriginCode = sanBayDi?.MaSanBay ?? "---",
            DestinationCity = sanBayDen?.ThanhPho ?? toText,
            DestinationCode = sanBayDen?.MaSanBay ?? "---",
            DepartDateText = FlightMapping.DisplayDate(date),
            TripTypeText = tripText,
            Passengers = passengerCount,
            Cabin = cabinText,
            PriceRangeText = "Giá thực tế theo từng chuyến"
        };

        if (sanBayDi is null || sanBayDen is null)
        {
            return View(EmptyModel(
                criteria,
                "Không tìm thấy sân bay",
                $"Không xác định được sân bay cho \"{fromText}\" hoặc \"{toText}\". " +
                "Hãy nhập mã IATA 3 ký tự (ví dụ SGN) hoặc tên thành phố."));
        }

        var chuyenBay = FlightServices.ChuyenBay(dbContext);

        try
        {
            var results = await chuyenBay.TimChuyenBayAsync(new TimChuyenBayDTO
            {
                SanBayDiId = sanBayDi.Id,
                SanBayDenId = sanBayDen.Id,
                NgayDi = date,
                SoHanhKhach = passengerCount
            }, cancellationToken);

            var cards = results
                .Select((dto, index) => FlightMapping.ToCard(dto, highlighted: index == 0))
                .ToArray();

            var dayPrices = await BuildDayPricesAsync(chuyenBay, sanBayDi.Id, sanBayDen.Id, date, cancellationToken);

            return View(new FlightResultsViewModel
            {
                Criteria = criteria,
                Flights = cards,
                DayPrices = dayPrices,
                TotalCount = cards.Length,
                IsEmpty = cards.Length == 0,
                EmptyTitle = "Chưa có chuyến phù hợp",
                EmptyText = $"Không có chuyến {criteria.OriginCode} → {criteria.DestinationCode} " +
                            $"ngày {criteria.DepartDateText} còn đủ {passengerCount} ghế. " +
                            "Bạn có thể đổi ngày bay hoặc giảm số hành khách."
            });
        }
        catch (ArgumentException ex)
        {
            return View(EmptyModel(criteria, "Bộ lọc chưa hợp lệ", ex.Message));
        }
    }

    [HttpGet("/chuyen-bay/khong-co-chuyen")]
    public IActionResult NoResults() => View("Index", EmptyModel(
        new FlightSearchCriteria { PriceRangeText = "1.500.000 ₫ – 1.800.000 ₫" },
        "Không có chuyến phù hợp bộ lọc",
        "Chưa tìm thấy chuyến bay trong khoảng giá đã chọn. Bạn có thể mở rộng khoảng giá, " +
        "bỏ bộ lọc hoặc đổi ngày bay."));

    /// <summary>Sơ đồ ghế dùng cho bước chọn chỗ ở luồng đặt vé.</summary>
    [HttpGet("/chuyen-bay/{id:int}/so-do-ghe")]
    public async Task<IActionResult> SeatMap(int id, CancellationToken cancellationToken)
    {
        var chuyenBay = FlightServices.ChuyenBay(dbContext);
        try
        {
            var seatMap = await chuyenBay.LaySoDoGheAsync(id, cancellationToken);
            return Json(new
            {
                seatMap.ChuyenBayId,
                seatMap.SoHieu,
                seatMap.TongSoGhe,
                ghes = seatMap.Ghes.Select(seat => new
                {
                    seat.SoGhe,
                    seat.TrangThai,
                    TrangThaiText = FlightMapping.SeatStateText(seat.TrangThai),
                    seat.LoaiGhe,
                    seat.TenLoaiGhe,
                    seat.PhuThu
                })
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Ngày mặc định khi người dùng chưa chọn: ngày gần nhất còn chuyến mở bán.
    /// </summary>
    private async Task<DateOnly> ResolveDefaultDateAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var next = await dbContext.ChuyenBays
            .AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiChuyenBay.DuKien && x.GioKhoiHanh > now)
            .OrderBy(x => x.GioKhoiHanh)
            .Select(x => (DateTimeOffset?)x.GioKhoiHanh)
            .FirstOrDefaultAsync(cancellationToken);

        return next is { } value
            ? DateOnly.FromDateTime(value.ToOffset(ChuyenBayBUS.VietnamOffset).DateTime)
            : new DateOnly(2026, 11, 20);
    }

    private static FlightResultsViewModel EmptyModel(
        FlightSearchCriteria criteria,
        string title,
        string text) => new()
    {
        Criteria = criteria,
        Flights = Array.Empty<FlightCardModel>(),
        DayPrices = Array.Empty<DayPriceChip>(),
        IsEmpty = true,
        EmptyTitle = title,
        EmptyText = text
    };

    private static async Task<IReadOnlyList<DayPriceChip>> BuildDayPricesAsync(
        AirlineTicketing.BUS.ChuyenBayBUS chuyenBay,
        int sanBayDiId,
        int sanBayDenId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var chips = new List<DayPriceChip>(3);

        for (var offset = -1; offset <= 1; offset++)
        {
            var day = date.AddDays(offset);
            IReadOnlyList<ChuyenBayDTO> items;
            try
            {
                items = await chuyenBay.TimChuyenBayAsync(new TimChuyenBayDTO
                {
                    SanBayDiId = sanBayDiId,
                    SanBayDenId = sanBayDenId,
                    NgayDi = day,
                    SoHanhKhach = 1
                }, cancellationToken);
            }
            catch (ArgumentException)
            {
                items = Array.Empty<ChuyenBayDTO>();
            }

            var cheapest = items.Count == 0 ? 0m : items.Min(item => item.GiaCoBan);
            chips.Add(new DayPriceChip(
                day.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture),
                cheapest,
                offset == 0));
        }

        return chips;
    }
}
