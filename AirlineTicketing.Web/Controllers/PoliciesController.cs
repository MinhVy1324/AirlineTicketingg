using AirlineTicketing.BUS;
using AirlineTicketing.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

[AllowAnonymous]
public sealed class PoliciesController(CauHinhHeThongBUS policies, BookingPromotion promotion) : Controller
{
    [HttpGet("/dieu-kien-van-chuyen")]
    public IActionResult Carriage() => View();

    [HttpGet("/bao-mat")]
    public IActionResult Privacy() => View();

    [HttpGet("/doi-hoan-ve")]
    public async Task<IActionResult> ChangeRefund(CancellationToken ct) =>
        View(await policies.LayChinhSachAsync(ct));

    [HttpGet("/khuyen-mai")]
    public IActionResult Promotions() => View(promotion);
}
