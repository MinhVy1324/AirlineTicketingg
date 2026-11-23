using AirlineTicketing.Web.Infrastructure;
using System.Diagnostics;
using AirlineTicketing.Web.Demo;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

<<<<<<< Updated upstream
public class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View(DemoCatalog.Home());
=======
[AllowAnonymous]
public class HomeController(IConfiguration configuration, AppDbContext db, CauHinhHeThongBUS policies) : Controller
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
            .Where(t => t.DangKhaiThac && t.SanBayDi.DangHoatDong && t.SanBayDen.DangHoatDong
                && t.SanBayDi.MaSanBay == "SGN"
                && (t.SanBayDen.MaSanBay == "HAN" || t.SanBayDen.MaSanBay == "DAD" || t.SanBayDen.MaSanBay == "PQC"))
            .Select(t => new
            {
                t.SanBayDen.ThanhPho,
                t.SanBayDen.MaSanBay,
                MinPrice = db.ChuyenBays
                    .Where(c => c.TuyenBayId == t.Id && c.GioKhoiHanh > now
                        && c.TrangThai == TrangThaiChuyenBay.DuKien && c.MayBay.DangHoatDong)
                    .Select(c => (decimal?)c.GiaCoBan)
                    .Min()
            })
            .Where(x => x.MinPrice != null)
            .GroupBy(x => new { x.ThanhPho, x.MaSanBay })
            .Select(g => new { g.Key.ThanhPho, g.Key.MaSanBay, Price = g.Min(x => x.MinPrice)!.Value })
            .OrderBy(x => x.Price)
            .Take(3)
            .ToListAsync(ct);

        var policy = await policies.LayChinhSachAsync(ct);
        var cards = destinations.Select(d => new DestinationCard(
            d.ThanhPho, d.MaSanBay, d.Price + policy.Tax + policy.ServiceFee,
            d.MaSanBay switch { "DAD" => "dest-da-nang.png", "PQC" => "dest-phu-quoc.png", _ => "dest-ha-noi.png" }
        )).ToList();

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

        if (!string.IsNullOrWhiteSpace(Request.Query["from"])) search.From = Request.Query["from"].ToString();
        if (!string.IsNullOrWhiteSpace(Request.Query["to"])) search.To = Request.Query["to"].ToString();
        if (FlightMapping.TryParseDate(Request.Query["depart"], out var editDate)) search.DepartDate = editDate.ToString("dd/MM/yyyy");
        if (FlightMapping.TryParseDate(Request.Query["returnDate"], out var editReturn)) search.ReturnDate = editReturn.ToString("dd/MM/yyyy");
        search.Trip = Request.Query["trip"] == "roundtrip" ? "roundtrip" : "oneway";
        if (int.TryParse(Request.Query["adults"], out var adults) && adults is >= 1 and <= 9) search.Adults = adults;
        if (int.TryParse(Request.Query["children"], out var children) && children is >= 0 and <= 8) search.Children = children;
        if (int.TryParse(Request.Query["infants"], out var infants) && infants is >= 0 and <= 9) search.Infants = infants;
        search.Cabin = JourneyBookingBuilder.CabinOf(Request.Query["cabin"].ToString()).Name;
        return View(new HomeViewModel
        {
            CustomerName = "Khách hàng",
            CustomerInitials = "KH",
            Search = search,
            Destinations = cards
        });
    }
>>>>>>> Stashed changes

    [HttpGet("/gioi-thieu")]
    public IActionResult About() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
