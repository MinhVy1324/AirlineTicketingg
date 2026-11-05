using AirlineTicketing.Web.Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

[Authorize(Roles = "Admin,NhanVien")]
public class ManagerController : Controller
{
    [HttpGet("/quan-ly")]
    public IActionResult Index() => RedirectToAction("Index", "Dashboard");

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
    public IActionResult Bookings(string? q) => View(DemoCatalog.ManagerBookings());

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
    public IActionResult Reports() => RedirectToAction("Index", "BaoCao");

    [HttpPost("/quan-ly/bao-cao")]
    [ValidateAntiForgeryToken]
    public IActionResult Reports(IFormCollection form) => RedirectToAction("Index", "BaoCao");

    [HttpPost("/quan-ly/bao-cao/xuat")]
    [ValidateAntiForgeryToken]
    public IActionResult ExportReport(IFormCollection form) => RedirectToAction(nameof(Reports));
}
