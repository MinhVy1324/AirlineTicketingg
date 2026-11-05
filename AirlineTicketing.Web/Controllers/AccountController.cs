using System.Security.Claims;
using AirlineTicketing.BUS;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public class AccountController(ITaiKhoanBUS taiKhoanBUS, ILogger<AccountController> logger) : Controller
{
    [HttpGet("/dang-nhap")]
    [HttpGet("/Account/Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDefault();
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new DangNhapDTO { ReturnUrl = returnUrl });
    }

    [HttpPost("/dang-nhap")]
    [HttpPost("/Account/Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(DangNhapDTO model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var user = await taiKhoanBUS.DangNhapAsync(model.TenDangNhap, model.MatKhau, cancellationToken);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.TenDangNhap),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.GivenName, user.HoTen),
                new(ClaimTypes.Role, user.VaiTro)
            };

            if (user.KhachHangId.HasValue)
            {
                claims.Add(new Claim("KhachHangId", user.KhachHangId.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProps = new AuthenticationProperties
            {
                IsPersistent = model.GhiNho,
                ExpiresUtc = model.GhiNho ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProps);
            logger.LogInformation("Người dùng '{TenDangNhap}' (Vai trò: {VaiTro}) đã đăng nhập thành công.", user.TenDangNhap, user.VaiTro);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToDefault();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet("/dang-ky")]
    [HttpGet("/Account/Register")]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDefault();
        }

        return View(new DangKyDTO());
    }

    [HttpPost("/dang-ky")]
    [HttpPost("/Account/Register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(DangKyDTO model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var user = await taiKhoanBUS.DangKyAsync(model, cancellationToken);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.TenDangNhap),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.GivenName, user.HoTen),
                new(ClaimTypes.Role, user.VaiTro)
            };

            if (user.KhachHangId.HasValue)
            {
                claims.Add(new Claim("KhachHangId", user.KhachHangId.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Chào mừng bạn đến với hệ thống.";

            return RedirectToAction("Index", "Home");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost("/dang-xuat")]
    [HttpPost("/Account/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken = default)
    {
        int userId = GetCurrentUserId();
        var user = await taiKhoanBUS.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        var model = new CapNhatHoSoDTO
        {
            HoTen = user.HoTen,
            Email = user.Email,
            SoDienThoai = user.SoDienThoai
        };

        ViewBag.TaiKhoan = user;
        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(CapNhatHoSoDTO model, CancellationToken cancellationToken = default)
    {
        int userId = GetCurrentUserId();
        var user = await taiKhoanBUS.GetByIdAsync(userId, cancellationToken);
        ViewBag.TaiKhoan = user;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await taiKhoanBUS.CapNhatHoSoAsync(userId, model, cancellationToken);
            TempData["SuccessMessage"] = "Cập nhật thông tin hồ sơ thành công!";
            return RedirectToAction(nameof(Profile));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View(new DoiMatKhauDTO());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(DoiMatKhauDTO model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            int userId = GetCurrentUserId();
            await taiKhoanBUS.DoiMatKhauAsync(userId, model, cancellationToken);
            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction(nameof(Profile));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToDefault()
    {
        if (User.IsInRole("Admin") || User.IsInRole("NhanVien"))
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return RedirectToAction("Index", "Home");
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
    }
}
