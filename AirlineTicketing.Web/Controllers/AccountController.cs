using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AirlineTicketing.Web.Controllers;

public class AccountController(AppDbContext db) : Controller
{
    [HttpGet("/dang-nhap")]
    public IActionResult Login(string? returnUrl = null, bool tuChoi = false)
    {
        if (User.Identity?.IsAuthenticated == true && !tuChoi)
        {
            return LocalRedirect(AppRoles.HomePathFor(User.FindFirstValue(ClaimTypes.Role)));
        }

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            ErrorMessage = tuChoi ? "Bạn không có quyền truy cập khu vực này." : null,
            Email = string.Empty,
            Password = string.Empty
        });
    }

    [HttpPost("/dang-nhap")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        model.Email = model.Email?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            model.ErrorMessage = "Vui lòng nhập tên đăng nhập/email và mật khẩu.";
            model.Password = string.Empty;
            return View(model);
        }

        var identifier = model.Email.ToLowerInvariant();
        var account = await db.TaiKhoans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenDangNhap.ToLower() == identifier || x.Email.ToLower() == identifier, ct);

        if (account is null || !PasswordHelper.Verify(model.Password, account.MatKhauHash))
        {
            await WriteAuditAsync(null, model.Email, "Khách", "Đăng nhập thất bại", model.Email, KetQuaNhatKy.ThatBai, "Sai tên đăng nhập hoặc mật khẩu.", ct);
            model.ErrorMessage = "Tên đăng nhập/email hoặc mật khẩu không đúng.";
            model.Password = string.Empty;
            return View(model);
        }

        if (account.TrangThai == TrangThaiTaiKhoan.BiKhoa)
        {
            await WriteAuditAsync(account.Id, account.HoTen, account.VaiTro.ToString(), "Đăng nhập bị chặn", account.TenDangNhap, KetQuaNhatKy.ThatBai, "Tài khoản đã bị khóa.", ct);
            model.ErrorMessage = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.";
            model.Password = string.Empty;
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.TenDangNhap),
            new("display_name", account.HoTen),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Role, account.VaiTro.ToString())
        };
        if (account.KhachHangId.HasValue)
        {
            claims.Add(new Claim("khach_hang_id", account.KhachHangId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProps = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(8),
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProps);

        // Update last login (tracked in a separate lightweight operation)
        var tracked = await db.TaiKhoans.FirstAsync(x => x.Id == account.Id, ct);
        tracked.LanDangNhapCuoi = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(account.Id, account.HoTen, account.VaiTro.ToString(), "Đăng nhập", account.TenDangNhap, KetQuaNhatKy.ThanhCong, null, ct);

        var role = account.VaiTro.ToString();
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            // Prevent cross-area open redirects in practice: only allow safe local URLs.
            return LocalRedirect(model.ReturnUrl);
        }

        return LocalRedirect(AppRoles.HomePathFor(role));
    }

    [HttpGet("/dang-xuat")]
    public Task<IActionResult> LogoutGet(CancellationToken ct) => SignOutAndRedirect(ct);

    [HttpPost("/dang-xuat")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Logout(CancellationToken ct) => SignOutAndRedirect(ct);

    private async Task<IActionResult> SignOutAndRedirect(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            await WriteAuditAsync(User.TaiKhoanId(), User.DisplayName(), User.FindFirstValue(ClaimTypes.Role) ?? "Khách", "Đăng xuất", User.Identity.Name ?? "", KetQuaNhatKy.ThanhCong, null, ct);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task WriteAuditAsync(int? taiKhoanId, string ten, string vaiTro, string hanhDong, string doiTuong, KetQuaNhatKy ketQua, string? chiTiet, CancellationToken ct)
    {
        db.NhatKyHoatDongs.Add(new NhatKyHoatDong
        {
            MaNhatKy = $"LOG-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}",
            TaiKhoanId = taiKhoanId,
            TenNguoiDung = ten,
            VaiTro = vaiTro,
            HanhDong = hanhDong,
            DoiTuong = doiTuong,
            KetQua = ketQua,
            ChiTiet = chiTiet,
            DiaChiIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ThoiGian = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
