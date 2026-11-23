using AirlineTicketing.Web.Demo;
using Microsoft.AspNetCore.Mvc;
<<<<<<< Updated upstream

namespace AirlineTicketing.Web.Controllers;

public class FlightsController : Controller
=======
using Microsoft.EntityFrameworkCore;
using static AirlineTicketing.Web.Infrastructure.UiLanguage;

namespace AirlineTicketing.Web.Controllers;

[AllowAnonymous]
public class FlightsController(AppDbContext dbContext, CauHinhHeThongBUS policies) : Controller
>>>>>>> Stashed changes
{
    [HttpGet("/chuyen-bay")]
<<<<<<< Updated upstream
    public IActionResult Index() => View(DemoCatalog.Flights());
=======
    public async Task<IActionResult> Index(string? from, string? to, string? depart, string? passengers,
        string? cabin, string? trip, string? sort, [FromQuery] string[]? airlines, [FromQuery] string[]? times,
        string? minPrice, string? maxPrice, string? adults, string? children, string? infants,
        string? returnDate, int? outboundId, CancellationToken cancellationToken)
    {
        var fromText = string.IsNullOrWhiteSpace(from) ? DefaultFrom : from.Trim();
        var toText = string.IsNullOrWhiteSpace(to) ? DefaultTo : to.Trim();
        var filters = FlightFilters.Parse(airlines, times, minPrice, maxPrice);
        var sortKey = FlightSort.Normalize(sort);
        var date = FlightMapping.TryParseDate(depart, out var parsedDate) ? parsedDate : await ResolveDefaultDateAsync(cancellationToken);
        var catalog = FlightServices.DanhMuc(dbContext);
        var origin = await catalog.TimSanBayTheoTuKhoaAsync(fromText, cancellationToken);
        var destination = await catalog.TimSanBayTheoTuKhoaAsync(toText, cancellationToken);
        var criteria = new FlightSearchCriteria
        {
            OriginCity = origin?.ThanhPho ?? fromText, OriginCode = origin?.MaSanBay ?? "---",
            DestinationCity = destination?.ThanhPho ?? toText, DestinationCode = destination?.MaSanBay ?? "---",
            DepartDateText = FlightMapping.DisplayDate(date), PriceRangeText = filters.PriceRangeText
        };
        try
        {
            var party = new PassengerParty(ParsePartyCount(adults, FlightMapping.ParsePassengers(passengers)),
                ParsePartyCount(children, 0), ParsePartyCount(infants, 0));
            party.Validate();
            var fareClass = JourneyRules.Cabin(cabin);
            var roundTrip = string.Equals(trip, "roundtrip", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(trip) && !roundTrip && !string.Equals(trip, "oneway", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(T("Loại hành trình không hợp lệ.", "Invalid trip type."));
            DateOnly? returnDay = null;
            if (roundTrip)
            {
                if (!FlightMapping.TryParseDate(returnDate ?? Request.Query["return"].ToString(), out var value) || value < date)
                    throw new ArgumentException(T("Vui lòng chọn ngày về hợp lệ, không trước ngày đi.",
                        "Select a valid return date on or after your departure date."));
                returnDay = value;
            }
            if (outboundId.HasValue && !roundTrip)
                throw new ArgumentException(T("Vui lòng chọn lại loại hành trình.", "Select your trip type again."));
            criteria = criteria with
            {
                Party = party, Passengers = party.Total, Cabin = fareClass.Name,
                TripTypeText = roundTrip ? "Khứ hồi" : "Một chiều",
                ReturnDateText = returnDay.HasValue ? FlightMapping.DisplayDate(returnDay.Value) : null,
                OutboundDateText = FlightMapping.DisplayDate(date),
                JourneyOriginCode = origin?.MaSanBay, JourneyDestinationCode = destination?.MaSanBay
            };
            if (origin is null || destination is null)
                return View(EmptyModel(criteria, T("Không tìm thấy sân bay", "Airport not found"),
                    T("Hãy nhập mã IATA hoặc tên thành phố hợp lệ.", "Enter a valid IATA code or city name."), filters, sortKey));
            var flightService = FlightServices.ChuyenBay(dbContext);
            ChuyenBayDTO? outbound = null;
            if (outboundId.HasValue)
            {
                outbound = await flightService.LayChuyenBayAsync(outboundId.Value, cancellationToken);
                if (outbound is null || outbound.MaSanBayDi != origin.MaSanBay || outbound.MaSanBayDen != destination.MaSanBay
                    || DateOnly.FromDateTime(FlightMapping.ToVietnamTime(outbound.GioKhoiHanh).DateTime) != date
                    || outbound.GioKhoiHanh <= DateTimeOffset.UtcNow || outbound.TrangThai != nameof(TrangThaiChuyenBay.DuKien)
                    || outbound.SoGheTrong < party.Seats)
                    throw new ArgumentException(T("Chiều đi không còn phù hợp hoặc không đủ chỗ. Vui lòng chọn lại.",
                        "The outbound flight is no longer available or has too few seats. Select another flight."));
                criteria = criteria with
                {
                    SelectedOutboundId = outbound.Id, OriginCity = destination.ThanhPho, OriginCode = destination.MaSanBay,
                    DestinationCity = origin.ThanhPho, DestinationCode = origin.MaSanBay,
                    DepartDateText = FlightMapping.DisplayDate(returnDay!.Value)
                };
            }
            var legOrigin = outbound is null ? origin : destination;
            var legDestination = outbound is null ? destination : origin;
            var legDate = outbound is null ? date : returnDay!.Value;
            var results = await flightService.TimChuyenBayAsync(new TimChuyenBayDTO
            {
                SanBayDiId = legOrigin.Id, SanBayDenId = legDestination.Id, NgayDi = legDate, SoHanhKhach = party.Seats
            }, cancellationToken);
            if (outbound is not null) results = results.Where(flight => flight.GioKhoiHanh > outbound.GioHaCanh).ToArray();
            var policy = await policies.LayChinhSachAsync(cancellationToken);
            var filtered = filters.Apply(results, policy, cabin: fareClass);
            var linkModel = new FlightResultsViewModel { Criteria = criteria, Filters = filters, SortKey = sortKey };
            var cards = FlightSort.Apply(filtered, sortKey).Select((dto, index) =>
                FlightMapping.ToCard(dto, highlighted: index == 0, policy: policy, cabin: fareClass) with
                {
                    SelectUrl = FlightSearchUrl.Select(linkModel, dto.Id),
                    SelectLabel = roundTrip && outbound is null ? "Chọn chiều đi" : outbound is not null ? "Chọn chiều về" : "Chọn chuyến",
                    PartySummary = BookingUiText.Party(party),
                    PartyTotal = party.Total > 1 ? JourneyRules.PartyTotal(dto.GiaCoBan, policy, fareClass, party) : null
                }).ToArray();
            var days = await BuildDayPricesAsync(flightService, legOrigin.Id, legDestination.Id, legDate,
                party.Seats, filters, policy, fareClass, outbound?.GioHaCanh, cancellationToken);
            var airlineMatches = filters.Apply(results, policy, ignoreAirlines: true, cabin: fareClass);
            var timeMatches = filters.Apply(results, policy, ignoreTimes: true, cabin: fareClass);
            var names = results.Where(x => x.MaHangHangKhong.Length > 0).GroupBy(x => x.MaHangHangKhong, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().TenHangHangKhong, StringComparer.OrdinalIgnoreCase);
            var selectedCodes = filters.Airlines.Take(30).ToArray();
            foreach (var airline in await dbContext.Set<HangHangKhong>().AsNoTracking().Where(x => selectedCodes.Contains(x.MaHang)).ToListAsync(cancellationToken))
                names.TryAdd(airline.MaHang, airline.TenHang);
            foreach (var value in selectedCodes) names.TryAdd(value, value);
            return View(new FlightResultsViewModel
            {
                Criteria = criteria, Filters = filters, SortKey = sortKey, Flights = cards, DayPrices = days,
                TotalCount = cards.Length, AvailableCount = results.Count, IsEmpty = cards.Length == 0,
                SelectedOutbound = outbound is null ? null : FlightMapping.ToOffer(outbound, policy: policy, cabin: fareClass),
                AirlineOptions = names.OrderBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase).Select(pair => new FlightFilterOption(
                    pair.Key, string.IsNullOrWhiteSpace(pair.Value) ? pair.Key : pair.Value,
                    airlineMatches.Count(flight => string.Equals(flight.MaHangHangKhong, pair.Key, StringComparison.OrdinalIgnoreCase)),
                    filters.Airlines.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))).ToArray(),
                DepartureOptions = FlightFilters.Windows.Select(window => new FlightFilterOption(window.Key, window.Label,
                    timeMatches.Count(flight => window.Contains(flight.GioKhoiHanh)), filters.Times.Contains(window.Key))).ToArray(),
                EmptyTitle = !filters.IsValid ? T("Bộ lọc chưa hợp lệ", "Invalid filters")
                    : results.Count > 0 && filters.IsActive ? T("Không có chuyến khớp bộ lọc", "No flights match your filters")
                    : T("Chưa có chuyến phù hợp", "No suitable flights"),
                EmptyText = !filters.IsValid
                    ? T("Vui lòng sửa các mục được thông báo trong bộ lọc rồi áp dụng lại.", "Correct the indicated filters and apply them again.")
                    : results.Count > 0 && filters.IsActive
                        ? T($"Có {results.Count} chuyến còn đủ chỗ. Hãy mở rộng bộ lọc.",
                            $"{results.Count} flights have enough seats. Try expanding the filters.")
                        : T($"Không có chuyến phù hợp ngày {criteria.DepartDateText} còn đủ {party.Seats} ghế. Bạn có thể chọn ngày khác.",
                            $"No flights on {criteria.DepartDateText} have enough seats for your group. Try another date.")
            });
        }
        catch (ArgumentException error)
        {
            return View(EmptyModel(criteria, T("Thông tin tìm chuyến chưa hợp lệ", "Review your flight search"),
                BookingUiText.Text(error.Message), filters, sortKey));
        }
    }
>>>>>>> Stashed changes

    private static int ParsePartyCount(string? value, int fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : int.TryParse(value, out var count) && count >= 0 && count <= 9
            ? count : throw new ArgumentException(T("Số hành khách phải là số nguyên từ 0 đến 9.",
                "Enter a whole number between 0 and 9 for the passenger count."));
    [HttpGet("/chuyen-bay/khong-co-chuyen")]
<<<<<<< Updated upstream
    public IActionResult NoResults() => View("Index", DemoCatalog.EmptyFlights());
=======
    public IActionResult NoResults() => View("Index", EmptyModel(
        new FlightSearchCriteria { PriceRangeText = "1.500.000 ₫ – 1.800.000 ₫" },
        T("Không có chuyến phù hợp bộ lọc", "No flights match your filters"),
        T("Chưa tìm thấy chuyến bay trong khoảng giá đã chọn. Bạn có thể mở rộng khoảng giá, bỏ bộ lọc hoặc đổi ngày bay.",
            "No flights were found in the selected price range. Expand the range, clear filters or change the travel date.")));

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
        string text,
        FlightFilters? filters = null,
        string sortKey = FlightSort.Recommended) => new()
    {
        Criteria = criteria,
        Flights = Array.Empty<FlightCardModel>(),
        DayPrices = Array.Empty<DayPriceChip>(),
        IsEmpty = true,
        EmptyTitle = title,
        EmptyText = text,
        Filters = filters ?? FlightFilters.Parse(),
        SortKey = sortKey,
        AirlineOptions = (filters?.Airlines ?? []).Select(code => new FlightFilterOption(code, code, 0, true)).ToArray(),
        DepartureOptions = FlightFilters.Windows.Select(window => new FlightFilterOption(window.Key, window.Label,
            0, filters?.Times.Contains(window.Key) == true)).ToArray()
    };

    private static async Task<IReadOnlyList<DayPriceChip>> BuildDayPricesAsync(
        AirlineTicketing.BUS.ChuyenBayBUS chuyenBay,
        int sanBayDiId,
        int sanBayDenId,
        DateOnly date,
        int passengerCount,
        FlightFilters filters,
        ChinhSachDatVe policy,
        FareClass cabin,
        DateTimeOffset? after,
        CancellationToken cancellationToken)
    {
        // Tuần cố định từ thứ Hai đến Chủ nhật. Các cột giữ nguyên khi khách chọn
        // ngày khác trong cùng tuần, chỉ cột của ngày được chọn đổi màu tô sáng.
        var daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        var weekStart = date.AddDays(-daysFromMonday);
        var chips = new List<DayPriceChip>(7);

        for (var i = 0; i < 7; i++)
        {
            var day = weekStart.AddDays(i);
            IReadOnlyList<ChuyenBayDTO> items;
            try
            {
                items = await chuyenBay.TimChuyenBayAsync(new TimChuyenBayDTO
                {
                    SanBayDiId = sanBayDiId,
                    SanBayDenId = sanBayDenId,
                    NgayDi = day,
                    SoHanhKhach = passengerCount
                }, cancellationToken);
            }
            catch (ArgumentException)
            {
                items = Array.Empty<ChuyenBayDTO>();
            }

            var matching = filters.Apply(after.HasValue ? items.Where(item => item.GioKhoiHanh > after.Value) : items, policy, cabin: cabin);
            var cheapest = matching.Count == 0 ? 0m : matching.Min(item => JourneyRules.AdultTotal(item.GiaCoBan, policy, cabin));
            chips.Add(new DayPriceChip(
                day,
                day.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture),
                WeekdayText(day),
                cheapest,
                day == date));
        }

        return chips;
    }

    private static string WeekdayText(DateOnly day) => day.DayOfWeek switch
    {
        DayOfWeek.Monday => "T2",
        DayOfWeek.Tuesday => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6",
        DayOfWeek.Saturday => "T7",
        _ => "CN"
    };
>>>>>>> Stashed changes
}
