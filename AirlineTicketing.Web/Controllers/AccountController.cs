using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public class AccountController : Controller
{
    [HttpGet("/dang-nhap")]
    public IActionResult Login(string? returnUrl = null) =>
        View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost("/dang-nhap")]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model)
    {
        // Demo only: any credentials sign in and land on the customer home page.
        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            model.ErrorMessage = "Vui lòng nhập email và mật khẩu.";
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet("/dang-xuat")]
    public IActionResult Logout() => RedirectToAction("Login");
}
