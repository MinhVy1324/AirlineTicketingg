using System.Diagnostics;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Controllers;

public class HomeController(IConfiguration configuration, AppDbContext db) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Each Docker test container can be pointed at one area. Requests to "/"
        // then land on the matching home page so every instance opens where the
        // tester expects it, while direct links to the other areas keep working.
        var area = configuration["App:DefaultArea"];

        if (string.Equals(area, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect("/admin");
        }

        if (string.Equals(area, "manager", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect("/quan-ly");
        }

        var now = DateTimeOffset.UtcNow;
        var destinations = await db.TuyenBays
            .AsNoTracking()
            .Include(t => t.SanBayDen)
            .Select(t => new
            {
                t.SanBayDen.ThanhPho,
                t.SanBayDen.MaSanBay,
                MinPrice = db.ChuyenBays
                    .Where(c => c.TuyenBayId == t.Id && c.GioKhoiHanh >= now)
                    .Select(c => (decimal?)c.GiaCoBan)
                    .Min()
            })
            .Where(x => x.MinPrice != null)
            .GroupBy(x => new { x.ThanhPho, x.MaSanBay })
            .Select(g => new { g.Key.ThanhPho, g.Key.MaSanBay, Price = g.Min(x => x.MinPrice)!.Value })
            .OrderBy(x => x.Price)
            .Take(3)
            .ToListAsync(ct);

        var cards = destinations.Select(d => new DestinationCard(
            d.ThanhPho, d.MaSanBay, d.Price,
            d.MaSanBay switch { "HAN" => "dest-ha-noi.png", "DAD" => "dest-da-nang.png", "PQC" => "dest-phu-quoc.png", _ => "dest-ha-noi.png" }
        )).ToList();
        if (cards.Count == 0)
        {
            cards = new List<DestinationCard>
            {
                new("Hà Nội", "HAN", 1_950_000m, "dest-ha-noi.png"),
                new("Đà Nẵng", "DAD", 1_290_000m, "dest-da-nang.png"),
                new("Phú Quốc", "PQC", 1_150_000m, "dest-phu-quoc.png")
            };
        }

        // Ngày mặc định lấy theo chuyến gần nhất còn mở bán để danh sách kết quả
        // luôn có dữ liệu thật thay vì một ngày cố định đã trôi qua.
        var nextDeparture = await db.ChuyenBays
            .AsNoTracking()
            .Where(c => c.TrangThai == TrangThaiChuyenBay.DuKien && c.GioKhoiHanh > now)
            .OrderBy(c => c.GioKhoiHanh)
            .Select(c => (DateTimeOffset?)c.GioKhoiHanh)
            .FirstOrDefaultAsync(ct);
        var search = new SearchFormModel();
        if (nextDeparture is { } departure)
        {
            search.DepartDate = departure.ToOffset(ChuyenBayBUS.VietnamOffset)
                .ToString("dd/MM/yyyy");
        }

        return View(new HomeViewModel
        {
            CustomerName = "Khách hàng",
            CustomerInitials = "KH",
            Search = search,
            Destinations = cards
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
