using AirlineTicketing.Web.Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

[Authorize]
public class TicketsController : Controller
{
    [HttpGet("/ve-cua-toi")]
    public IActionResult Index(string? tab) => View("Index", DemoCatalog.Tickets(tab));

    [HttpGet("/ve-cua-toi/chuyen-doi-gio")]
    public IActionResult ScheduleChanged() => View(DemoCatalog.ScheduleChanged());

    [HttpGet("/ve-cua-toi/chuyen-huy")]
    public IActionResult Cancelled() => View(DemoCatalog.Cancelled());

    [HttpGet("/ve-cua-toi/yeu-cau-doi")]
    public IActionResult ChangeRequest() => View(DemoCatalog.ChangeRequest());

    [HttpPost("/ve-cua-toi/yeu-cau-doi")]
    [ValidateAntiForgeryToken]
    public IActionResult ChangeRequest(IFormCollection form) => RedirectToAction(nameof(Requests));

    [HttpGet("/ve-cua-toi/yeu-cau-hoan")]
    public IActionResult RefundRequest() => View(DemoCatalog.RefundRequest());

    [HttpPost("/ve-cua-toi/yeu-cau-hoan")]
    [ValidateAntiForgeryToken]
    public IActionResult RefundRequest(IFormCollection form) => RedirectToAction(nameof(Requests));

    [HttpGet("/ve-cua-toi/yeu-cau")]
    public IActionResult Requests() => View(DemoCatalog.Requests());
}
