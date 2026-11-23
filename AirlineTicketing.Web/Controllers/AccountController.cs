<<<<<<< Updated upstream
using System.Security.Claims;
using AirlineTicketing.BUS;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
=======
using AirlineTicketing.BUS;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.Infrastructure.Email;
using AirlineTicketing.Web.ViewModels;
>>>>>>> Stashed changes
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
<<<<<<< Updated upstream

namespace AirlineTicketing.Web.Controllers;

public class AccountController(ITaiKhoanBUS taiKhoanBUS, ILogger<AccountController> logger) : Controller
=======
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using static AirlineTicketing.Web.Infrastructure.UiLanguage;

namespace AirlineTicketing.Web.Controllers;

[AllowAnonymous]
public class AccountController(TaiKhoanBUS accounts, AccountLinks links, EmailDeliveryQueue emailQueue,
    ILogger<AccountController> logger) : Controller
>>>>>>> Stashed changes
{
    private const string ResetPurpose = "reset";

    [HttpGet("/dang-ky")]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction(nameof(Login))
        : View(new RegisterViewModel());

    [HttpPost("/dang-ky")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AppRateLimits.AccountLinks)]
    public async Task<IActionResult> Register(RegisterViewModel form, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction(nameof(Login));
        form.Username = form.Username?.Trim() ?? "";
        if (!Regex.IsMatch(form.Username, @"\A[A-Za-z0-9][A-Za-z0-9._-]{2,49}\z"))
            form.Error = T("Tên đăng nhập cần 3–50 ký tự gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.",
                "Use 3–50 letters, digits, periods, underscores or hyphens, starting with a letter or digit.");
        else if (!ValidPassword(form.Password))
            form.Error = T("Mật khẩu cần từ 12 đến 128 ký tự.", "Use a password between 12 and 128 characters.");
        else if (!await accounts.TaoKhachHangAsync(form.Username, form.Password!, ct))
            form.Error = T("Tên đăng nhập đã được sử dụng.", "That username is already taken.");
        else
        {
            TempData["AccountMessage"] = T("Đã tạo tài khoản. Đăng nhập để hoàn thiện hồ sơ.",
                "Account created. Sign in to complete your profile.");
            return RedirectToAction(nameof(Login));
        }
        return View(form);
    }

    [HttpGet("/quen-mat-khau")]
    public IActionResult ForgotPassword() => View("RequestLink", new AccountFormViewModel());

    [HttpPost("/quen-mat-khau")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AppRateLimits.AccountLinks)]
    public Task<IActionResult> ForgotPassword(AccountFormViewModel form, CancellationToken ct) => RequestLinkAsync(form, ct);

    [HttpGet("/dang-ky/xac-nhan")]
    public IActionResult OldRegistrationLink() => RedirectToAction(nameof(Register));

    [HttpGet("/dat-lai-mat-khau")]
    public Task<IActionResult> ResetPassword(string? token, CancellationToken ct) =>
        PasswordFormAsync(new AccountFormViewModel { Token = token }, false, ct);

    [HttpPost("/dat-lai-mat-khau")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AppRateLimits.AccountLinks)]
    public Task<IActionResult> ResetPassword(AccountFormViewModel form, CancellationToken ct) =>
        PasswordFormAsync(form, true, ct);

    private async Task<IActionResult> RequestLinkAsync(AccountFormViewModel form, CancellationToken ct)
    {
        form.Email = form.Email?.Trim().ToLowerInvariant() ?? "";
        if (form.Email.Length is < 3 or > 254 || form.Email.Any(char.IsWhiteSpace)
            || !new EmailAddressAttribute().IsValid(form.Email))
        {
            form.Error = T("Vui lòng nhập địa chỉ email hợp lệ.", "Enter a valid email address.");
            return View("RequestLink", form);
        }

        var account = await accounts.TimTheoEmailAsync(form.Email, ct);
        var proof = account is { VaiTro: VaiTro.KhachHang, TrangThai: TrangThaiTaiKhoan.HoatDong }
            && !account.UsesUsernameRegistration()
            ? new AccountLinks.Proof(account.Email, account.Id, AccountLinks.Credential(account.MatKhauHash))
            : null;
        if (proof is not null)
        {
            var link = links.Create(ResetPurpose, "/dat-lai-mat-khau", proof);
            var title = T("Đặt lại mật khẩu AeroViet", "Reset your AeroViet password");
            var expiry = T(
                $"Liên kết có hiệu lực trong {AccountLinks.LifetimeMinutes} phút.",
                $"The link is valid for {AccountLinks.LifetimeMinutes} minutes.");
            var continueText = T("Tiếp tục", "Continue");
            var ignoreText = T("Bỏ qua thư này nếu bạn không yêu cầu.", "Ignore this email if you did not request it.");
            try
            {
                await emailQueue.QueueAccountAsync(new EmailMessage
                {
                    To = proof.Email,
                    Subject = title,
                    HtmlBody = $"<p>{title}</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">{continueText}</a></p><p>{expiry} {ignoreText}</p>",
                    TextBody = $"{title}\n{link}\n{expiry}"
                }, DateTimeOffset.UtcNow.AddMinutes(AccountLinks.LifetimeMinutes).AddSeconds(-30), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Không gửi được email tài khoản ({Purpose}, {ErrorType}).", ResetPurpose, ex.GetType().Name);
            }
        }
        return View("RequestLink", new AccountFormViewModel { Sent = true });
    }

    private async Task<IActionResult> PasswordFormAsync(AccountFormViewModel form, bool submit, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var proof = links.Read(ResetPurpose, form.Token);
        if (proof is null) return InvalidPasswordLink(form);
        form.Email = proof.Email;
        var account = await accounts.TimTheoEmailAsync(proof.Email, ct);
        if (!MatchesResetAccount(account, proof))
            return InvalidPasswordLink(form);
        if (!submit) return View("SetPassword", form);

        if (!ValidPassword(form.Password))
            form.Error = T("Mật khẩu cần từ 12 đến 128 ký tự.", "Use a password between 12 and 128 characters.");
        else if (form.Password != form.ConfirmPassword)
            form.Error = T("Hai mật khẩu chưa khớp.", "Your passwords do not match.");
        if (form.Error is not null) return View("SetPassword", form);

        if (!await accounts.DatLaiMatKhauAsync(account!, form.Password!, ct))
            return InvalidPasswordLink(form);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["AccountMessage"] = T("Đã đặt lại mật khẩu. Hãy đăng nhập bằng mật khẩu mới.", "Your password was reset. Sign in using your new password.");
        return RedirectToAction(nameof(Login));
    }

    private static bool MatchesResetAccount(TaiKhoan? account, AccountLinks.Proof proof) =>
        account is { VaiTro: VaiTro.KhachHang, TrangThai: TrangThaiTaiKhoan.HoatDong }
        && !account.UsesUsernameRegistration()
        && account.Id == proof.AccountId && AccountLinks.Credential(account.MatKhauHash) == proof.Credential;

    private static bool ValidPassword(string? password) =>
        password is { Length: >= 12 and <= 128 } && !string.IsNullOrWhiteSpace(password);

    private IActionResult InvalidPasswordLink(AccountFormViewModel form)
    {
        form.InvalidLink = true;
        form.Token = null;
        return View("SetPassword", form);
    }
    [HttpGet("/dang-nhap")]
<<<<<<< Updated upstream
    [HttpGet("/Account/Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDefault();
=======
    public IActionResult Login(string? returnUrl = null, bool tuChoi = false)
    {
        if (User.Identity?.IsAuthenticated == true && !tuChoi)
        {
            return LocalRedirect(AppRoles.HomePathFor(User.FindFirstValue(ClaimTypes.Role)));
        }

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            ErrorMessage = tuChoi ? T("Bạn không có quyền truy cập khu vực này.", "You do not have permission to access this area.") : null,
            Email = string.Empty,
            Password = string.Empty
        });
    }

    [HttpPost("/dang-nhap")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(AppRateLimits.Login)]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        model.Email = model.Email?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            model.ErrorMessage = T("Vui lòng nhập tên đăng nhập/email và mật khẩu.", "Enter your username or email and your password.");
            model.Password = string.Empty;
            return View(model);
        }

        var login = await accounts.DangNhapAsync(model.Email, model.Password, RemoteIp, ct);
        if (login.TaiKhoan is not { } account)
        {
            model.ErrorMessage = login.BiKhoa
                ? T("Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.", "Your account is locked. Contact an administrator.")
                : T("Tên đăng nhập/email hoặc mật khẩu không đúng.", "The username, email, or password is incorrect.");
            model.Password = string.Empty;
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.TenDangNhap),
            new("display_name", account.HoTen),
            new(ClaimTypes.Role, account.VaiTro.ToString()),
            new(AccountLinks.CredentialClaim, AccountLinks.Credential(account.MatKhauHash))
        };
        if (account.UsesUsernameRegistration()) claims.Add(new(AccountLinks.UsernameAccountClaim, "true"));
        else claims.Add(new(ClaimTypes.Email, account.Email));

        if (account.KhachHangId is int khachHangId)
        {
            claims.Add(new Claim("khach_hang_id", khachHangId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProps = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(8),
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProps);

        var role = account.VaiTro.ToString();
        if (account.NeedsProfile())
            return RedirectToAction(nameof(ProfileController.Index), "Profile",
                new { returnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null });
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
            await accounts.GhiNhatKyAsync(User.TaiKhoanId(), User.DisplayName(), User.FindFirstValue(ClaimTypes.Role) ?? "Khách",
                "Đăng xuất", User.Identity.Name ?? "", KetQuaNhatKy.ThanhCong, RemoteIp, null, ct);
>>>>>>> Stashed changes
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new DangNhapDTO { ReturnUrl = returnUrl });
    }

<<<<<<< Updated upstream
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
=======
    private string? RemoteIp => HttpContext.Connection.RemoteIpAddress?.ToString();
>>>>>>> Stashed changes
}
