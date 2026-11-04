using System.Diagnostics;
using AirlineTicketing.DAL;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.Demo;
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
        var demo = DemoCatalog.Home();

        try
        {
            var destinations = await LoadDestinationsAsync(cancellationToken);
            if (destinations.Count == 0)
            {
                return View(demo);
            }

            return View(new HomeViewModel
            {
                CustomerName = demo.CustomerName,
                CustomerInitials = demo.CustomerInitials,
                Search = demo.Search,
                Destinations = destinations
            });
        }
        catch (Exception)
        {
            // Trang chủ vẫn phải mở được khi CSDL chưa sẵn sàng; dùng dữ liệu minh hoạ.
            return View(demo);
        }
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

        var date = FlightMapping.TryParseDate("20/11/2026", out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

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

            var flights = await chuyenBay.TimChuyenBayAsync(new TimChuyenBayDTO
            {
                SanBayDiId = route.SanBayDiId,
                SanBayDenId = route.SanBayDenId,
                NgayDi = date,
                SoHanhKhach = 1
            }, cancellationToken);

            if (flights.Count == 0)
            {
                continue;
            }

            cards.Add(new DestinationCard(
                destination.ThanhPho,
                destination.MaSanBay,
                flights.Min(item => item.GiaCoBan),
                image));
        }

        return cards;
    }
}
