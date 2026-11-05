using AirlineTicketing.BUS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

[Authorize(Roles = "Admin,NhanVien")]
public class DashboardController(
    IThongKeBUS thongKeBUS,
    IThanhToanBUS thanhToanBUS) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var stats = await thongKeBUS.GetDashboardStatsAsync(cancellationToken);
        int currentYear = DateTime.UtcNow.Year;
        var monthlyRevenue = await thongKeBUS.GetDoanhThuTheoThangAsync(currentYear, "VND", cancellationToken);
        var topFlights = await thongKeBUS.GetDoanhThuTheoChuyenBayAsync(cancellationToken: cancellationToken);
        var recentPayments = (await thanhToanBUS.GetAllGiaoDichAsync(cancellationToken: cancellationToken)).Take(5).ToList();

        ViewBag.Stats = stats;
        ViewBag.MonthlyRevenue = monthlyRevenue;
        ViewBag.TopFlights = topFlights.Take(5).ToList();
        ViewBag.RecentPayments = recentPayments;

        return View();
    }
}
