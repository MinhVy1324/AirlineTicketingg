using System.Diagnostics;
using AirlineTicketing.Web.Demo;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public class HomeController(IConfiguration configuration) : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
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

        return View(DemoCatalog.Home());
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
