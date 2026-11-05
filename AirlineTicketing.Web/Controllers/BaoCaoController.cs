using AirlineTicketing.BUS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

[Authorize(Roles = "Admin,NhanVien")]
public class BaoCaoController(
    IThongKeBUS thongKeBUS,
    IThanhToanBUS thanhToanBUS) : Controller
{
    public async Task<IActionResult> Index(
        string? preset = null,
        DateOnly? tuNgay = null,
        DateOnly? denNgay = null,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (!tuNgay.HasValue || !denNgay.HasValue)
        {
            switch (preset)
            {
                case "today":
                    tuNgay = today;
                    denNgay = today;
                    break;
                case "week":
                    tuNgay = today.AddDays(-7);
                    denNgay = today;
                    break;
                case "year":
                    tuNgay = new DateOnly(today.Year, 1, 1);
                    denNgay = new DateOnly(today.Year, 12, 31);
                    break;
                case "month":
                default:
                    tuNgay = new DateOnly(today.Year, today.Month, 1);
                    denNgay = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                    preset = "month";
                    break;
            }
        }

<<<<<<< Updated upstream
=======
        if (tuNgay.Value > denNgay.Value)
        {
            return BadRequest("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");
        }

        if (denNgay!.Value.DayNumber - tuNgay!.Value.DayNumber > 366)
        {
            return BadRequest("Khoảng báo cáo tối đa là 367 ngày.");
        }

>>>>>>> Stashed changes
        var overview = await thongKeBUS.GetDoanhThuTongQuanAsync(tuNgay, denNgay, "VND", cancellationToken);
        var dailyRevenue = await thongKeBUS.GetDoanhThuTheoNgayAsync(tuNgay.Value, denNgay.Value, "VND", cancellationToken);
        var flightRevenue = await thongKeBUS.GetDoanhThuTheoChuyenBayAsync(tuNgay, denNgay, cancellationToken);
        var transactions = await thanhToanBUS.GetAllGiaoDichAsync(cancellationToken: cancellationToken);
        var refunds = await thanhToanBUS.GetAllHoanTienAsync(cancellationToken: cancellationToken);

        ViewBag.Overview = overview;
        ViewBag.DailyRevenue = dailyRevenue;
        ViewBag.FlightRevenue = flightRevenue;
        ViewBag.Transactions = transactions.Take(10).ToList();
        ViewBag.Refunds = refunds.Take(10).ToList();
        ViewBag.TuNgay = tuNgay.Value.ToString("yyyy-MM-dd");
        ViewBag.DenNgay = denNgay.Value.ToString("yyyy-MM-dd");
        ViewBag.Preset = preset;

        return View();
    }
}
