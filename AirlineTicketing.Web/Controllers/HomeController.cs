using System.Diagnostics;
using AirlineTicketing.DAL;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public class HomeController(AppDbContext dbContext) : Controller
{
    private static readonly (string Code, string Image)[] FeaturedDestinations =
    [
        ("HAN", "dest-ha-noi.png"),
        ("DAD", "dest-da-nang.png"),
        ("PQC", "dest-phu-quoc.png")
    ];

    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        IReadOnlyList<DestinationCard> destinations = Array.Empty<DestinationCard>();

        try
        {
            destinations = await LoadDestinationsAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Trang chủ vẫn mở được khi CSDL chưa sẵn sàng; hiển thị trạng thái trống.
        }

        return View(new HomeViewModel
        {
            Search = new SearchFormModel(),
            Destinations = destinations
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    private async Task<IReadOnlyList<DestinationCard>> LoadDestinationsAsync(CancellationToken cancellationToken)
    {
        var danhMuc = FlightServices.DanhMuc(dbContext);
        var chuyenBay = FlightServices.ChuyenBay(dbContext);

        var airports = await danhMuc.LaySanBayAsync(trangThai: "Hoạt động", cancellationToken: cancellationToken);
        var routes = await danhMuc.LayTuyenBayAsync(trangThai: "Đang khai thác", cancellationToken: cancellationToken);
        var origin = airports.FirstOrDefault(airport =>
            string.Equals(airport.MaSanBay, "SGN", StringComparison.OrdinalIgnoreCase));

        if (origin is null)
        {
            return Array.Empty<DestinationCard>();
        }

        var dates = new[]
        {
            DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)),
            new DateOnly(2026, 11, 20)
        }.Distinct().ToArray();

        var cards = new List<DestinationCard>();

        foreach (var (code, image) in FeaturedDestinations)
        {
            var destination = airports.FirstOrDefault(airport =>
                string.Equals(airport.MaSanBay, code, StringComparison.OrdinalIgnoreCase));
            var route = routes.FirstOrDefault(item =>
                item.SanBayDiId == origin.Id && item.SanBayDenId == destination?.Id);

            if (destination is null || route is null)
            {
                continue;
            }

            decimal? minPrice = null;
            foreach (var date in dates)
            {
                var flights = await chuyenBay.TimChuyenBayAsync(new TimChuyenBayDTO
                {
                    SanBayDiId = route.SanBayDiId,
                    SanBayDenId = route.SanBayDenId,
                    NgayDi = date,
                    SoHanhKhach = 1
                }, cancellationToken);

                if (flights.Count > 0)
                {
                    var candidate = flights.Min(item => item.GiaCoBan);
                    minPrice = minPrice is null ? candidate : Math.Min(minPrice.Value, candidate);
                }
            }

            if (minPrice is null)
            {
                continue;
            }

            cards.Add(new DestinationCard(destination.ThanhPho, destination.MaSanBay, minPrice.Value, image));
        }

        return cards;
    }
}
