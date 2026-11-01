using AirlineTicketing.Web.Demo;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public class LookupController : Controller
{
    [HttpGet("/tra-cuu")]
    public IActionResult Index()
    {
        ViewData["CompactNav"] = true;
        return View(DemoCatalog.LookupFound());
    }

    [HttpPost("/tra-cuu")]
    [ValidateAntiForgeryToken]
    public IActionResult Index(LookupFormModel form)
    {
        ViewData["CompactNav"] = true;
        return View(DemoCatalog.Lookup(form));
    }
}
