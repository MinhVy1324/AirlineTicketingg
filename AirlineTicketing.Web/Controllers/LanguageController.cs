using System.Globalization;
using AirlineTicketing.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

[AllowAnonymous]
public sealed class LanguageController : Controller
{
    [HttpPost("/ngon-ngu")]
    [ValidateAntiForgeryToken]
    public IActionResult Set(string? language, string? returnUrl)
    {
        if (language is not (UiLanguage.Vietnamese or UiLanguage.English))
            return BadRequest();

        var preference = new RequestCulture(CultureInfo.CurrentCulture, CultureInfo.GetCultureInfo(language));
        Response.Cookies.Append(UiLanguage.CookieName, CookieRequestCultureProvider.MakeCookieValue(preference),
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Path = Request.PathBase.HasValue ? Request.PathBase.Value : "/",
                MaxAge = TimeSpan.FromDays(365)
            });

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/"));
    }
}
