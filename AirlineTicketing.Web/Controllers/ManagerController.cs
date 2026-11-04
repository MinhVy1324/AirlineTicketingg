using AirlineTicketing.Web.Demo;
using AirlineTicketing.BUS;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public class ManagerController(DatVeBUS datVeBUS) : Controller
{
    [HttpGet("/quan-ly")]
    public IActionResult Index() => View(DemoCatalog.ManagerOverview());

    [HttpGet("/quan-ly/lich-bay")]
    public IActionResult Schedules([FromQuery(Name = "hop-thoai")] string? hopThoai) =>
        View(DemoCatalog.ManagerSchedules(hopThoai));

    [HttpPost("/quan-ly/lich-bay")]
    [ValidateAntiForgeryToken]
    public IActionResult Schedules(IFormCollection form) => RedirectToAction(nameof(Schedules));

    [HttpPost("/quan-ly/lich-bay/huy-chuyen")]
    [ValidateAntiForgeryToken]
    public IActionResult CancelFlight(IFormCollection form) => RedirectToAction(nameof(Schedules));

    [HttpPost("/quan-ly/lich-bay/doi-gia")]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePrice(IFormCollection form) => RedirectToAction(nameof(Schedules));

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
}
