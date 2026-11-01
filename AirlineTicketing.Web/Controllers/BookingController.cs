using AirlineTicketing.Web.Demo;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public class BookingController : Controller
{
    [HttpGet("/dat-ve/hanh-khach")]
    public IActionResult Passengers() => View(DemoCatalog.Passengers());

    [HttpPost("/dat-ve/hanh-khach")]
    [ValidateAntiForgeryToken]
    public IActionResult Passengers(IFormCollection form) =>
        RedirectToAction(nameof(Checkout));

    [HttpGet("/dat-ve/hanh-khach/kiem-tra")]
    public IActionResult ReviewPassengers() => View("Passengers", DemoCatalog.ReviewPassengers());

    [HttpGet("/dat-ve/thanh-toan")]
    public IActionResult Checkout() => View(DemoCatalog.Checkout());

    [HttpPost("/dat-ve/thanh-toan")]
    [ValidateAntiForgeryToken]
    public IActionResult Checkout(IFormCollection form) =>
        RedirectToAction(nameof(Success));

    [HttpGet("/dat-ve/thanh-cong")]
    public IActionResult Success() => View(DemoCatalog.Success());

    [HttpGet("/dat-ve/het-cho")]
    public IActionResult SoldOut() => View("Unavailable", DemoCatalog.SoldOut());

    [HttpGet("/dat-ve/that-bai")]
    public IActionResult PaymentFailed() => View(DemoCatalog.PaymentFailed());

    [HttpGet("/dat-ve/het-han")]
    public IActionResult Expired() => View(DemoCatalog.Expired());

    [HttpGet("/dat-ve/dang-xuat-ve")]
    public IActionResult Processing() => View(DemoCatalog.Processing());
}
