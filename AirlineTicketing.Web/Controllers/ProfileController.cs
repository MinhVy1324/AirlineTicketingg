using AirlineTicketing.BUS;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using static AirlineTicketing.Web.Infrastructure.UiLanguage;

namespace AirlineTicketing.Web.Controllers;

[Authorize(Roles = AppRoles.KhachHang)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ProfileController(TaiKhoanBUS accounts) : Controller
{
    [HttpGet("/ho-so")]
    public async Task<IActionResult> Index(CancellationToken ct, string? returnUrl = null)
    {
        if (User.TaiKhoanId() is not int id || await accounts.LayHoSoAsync(id, ct) is not { } profile)
            return Forbid();

        return View(new AccountProfileViewModel
        {
            FullName = profile.HoTen,
            Email = profile.UsernameAccount ? profile.GmailLienHe : profile.Email,
            Phone = profile.SoDienThoai,
            UsernameAccount = profile.UsernameAccount,
            NeedsProfile = profile.NeedsProfile,
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
        });
    }

    [HttpPost("/ho-so")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update([Bind("FullName,Phone,Email,ReturnUrl")] AccountProfileViewModel form, CancellationToken ct)
    {
        if (User.TaiKhoanId() is not int id || await accounts.LayHoSoAsync(id, ct) is not { } profile)
            return Forbid();

        form.UsernameAccount = profile.UsernameAccount;
        form.NeedsProfile = profile.NeedsProfile;
        form.ReturnUrl = Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl : null;
        form.Email = profile.UsernameAccount ? form.Email?.Trim().ToLowerInvariant() : profile.Email;
        form.Phone = form.Phone?.Trim();
        if (profile.UsernameAccount)
        {
            if (string.IsNullOrWhiteSpace(form.Phone))
                ModelState.AddModelError(nameof(form.Phone), T("Nhập số điện thoại.", "Enter your phone number."));
            if (form.Email is null || form.Email.Length > 254 || form.Email.Any(char.IsWhiteSpace)
                || !form.Email.EndsWith("@gmail.com", StringComparison.Ordinal) || !new EmailAddressAttribute().IsValid(form.Email))
                ModelState.AddModelError(nameof(form.Email), T("Nhập địa chỉ Gmail hợp lệ.", "Enter a valid Gmail address."));
        }
        var name = form.FullName?.Trim();
        if (name?.Any(char.IsControl) == true)
            ModelState.AddModelError(nameof(form.FullName), T("Họ và tên chứa ký tự không hợp lệ.", "The full name contains invalid characters."));
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(name))
            return View("Index", form);

        if (!await accounts.CapNhatHoSoAsync(id, name, form.Phone, form.Email, HttpContext.Connection.RemoteIpAddress?.ToString(), ct))
            return Forbid();

        TempData["ProfileMessage"] = T("Đã lưu hồ sơ.", "Your profile was saved.");
        if (profile.NeedsProfile)
            return LocalRedirect(form.ReturnUrl ?? "/");
        return RedirectToAction(nameof(Index));
    }
}
