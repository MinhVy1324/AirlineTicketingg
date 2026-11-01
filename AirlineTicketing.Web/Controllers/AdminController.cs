using AirlineTicketing.Web.Demo;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public class AdminController : Controller
{
    [HttpGet("/admin")]
    public IActionResult Index() => View(DemoCatalog.AdminOverview());

    [HttpGet("/admin/tai-khoan")]
    public IActionResult Accounts() => View(DemoCatalog.AdminAccounts());

    [HttpGet("/admin/tai-khoan/khoa")]
    public IActionResult LockAccount() => View("Accounts", DemoCatalog.AdminAccounts(showLockDialog: true));

    [HttpPost("/admin/tai-khoan/khoa")]
    [ValidateAntiForgeryToken]
    public IActionResult LockAccount(IFormCollection form) => RedirectToAction(nameof(Accounts));

    [HttpGet("/admin/vai-tro")]
    public IActionResult Roles() => View(DemoCatalog.AdminRoles());

    [HttpPost("/admin/vai-tro")]
    [ValidateAntiForgeryToken]
    public IActionResult Roles(IFormCollection form) => RedirectToAction(nameof(Roles));

    [HttpGet("/admin/danh-muc")]
    public IActionResult Catalog(string? tab) => View(DemoCatalog.AdminCatalog(tab));

    [HttpPost("/admin/danh-muc")]
    [ValidateAntiForgeryToken]
    public IActionResult Catalog(IFormCollection form) => RedirectToAction(nameof(Catalog));

    [HttpGet("/admin/cau-hinh")]
    public IActionResult Settings() => View(DemoCatalog.AdminSettings());

    [HttpPost("/admin/cau-hinh")]
    [ValidateAntiForgeryToken]
    public IActionResult Settings(IFormCollection form) => RedirectToAction(nameof(Settings));

    [HttpGet("/admin/nhat-ky")]
    public IActionResult Audit() => View(DemoCatalog.AdminAudit());

    [HttpPost("/admin/nhat-ky")]
    [ValidateAntiForgeryToken]
    public IActionResult Audit(IFormCollection form) => RedirectToAction(nameof(Audit));
}
