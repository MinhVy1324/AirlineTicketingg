using AirlineTicketing.Web.Demo;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public class FlightsController : Controller
{
    [HttpGet("/chuyen-bay")]
    public IActionResult Index() => View(DemoCatalog.Flights());

    [HttpGet("/chuyen-bay/khong-co-chuyen")]
    public IActionResult NoResults() => View("Index", DemoCatalog.EmptyFlights());
}
