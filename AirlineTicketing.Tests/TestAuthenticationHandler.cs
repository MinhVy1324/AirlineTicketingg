using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AirlineTicketing.Tests;

/// <summary>
/// Scheme xác thực chỉ dùng trong test. Thay vì đăng nhập bằng mật khẩu và xử lý
/// antiforgery, test gửi header để mô phỏng chính xác các claim mà
/// <c>AccountController</c> tạo khi đăng nhập thật.
/// </summary>
internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RoleHeader = "X-Test-Role";
    public const string EmailHeader = "X-Test-Email";
    public const string KhachHangIdHeader = "X-Test-KhachHangId";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers[RoleHeader].ToString();
        if (string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var email = Request.Headers[EmailHeader].ToString();
        var khachHangId = Request.Headers[KhachHangIdHeader].ToString();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "9999"),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(email) ? $"test-{role}" : email),
            new("display_name", $"Test {role}"),
            new(ClaimTypes.Role, role)
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        if (int.TryParse(khachHangId, out var id))
        {
            claims.Add(new Claim("khach_hang_id", id.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    // Bắt chước cookie handler của ứng dụng để test được đúng luồng chuyển hướng.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var returnUrl = $"{Request.Path}{Request.QueryString}";
        Response.Redirect($"/dang-nhap?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.Redirect("/dang-nhap?tu-choi=1");
        return Task.CompletedTask;
    }
}
