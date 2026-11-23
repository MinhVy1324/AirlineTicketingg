using System.Security.Claims;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.DAL.Repositories;
<<<<<<< Updated upstream
using AirlineTicketing.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
=======
using AirlineTicketing.BUS;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.Infrastructure.Email;
using AirlineTicketing.Web.Infrastructure.Tickets;
using AirlineTicketing.Web.Infrastructure.Payments;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.RateLimiting;
>>>>>>> Stashed changes
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

<<<<<<< Updated upstream
builder.Services.AddControllersWithViews();
=======
// Khóa Data Protection dùng để mã hóa cookie đăng nhập, antiforgery và session.
// Lưu xuống đĩa thay vì key ring tạm trong bộ nhớ: rebuild container không làm
// mọi người bị đăng xuất, và compose gắn cùng volume cho bốn container web.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
}

Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("AirlineTicketing")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

// Cookie chỉ gửi qua HTTPS khi cấu hình "Always". Mặc định SameAsRequest để
// stack Docker chạy HTTP nội bộ vẫn đăng nhập được; bật Always khi có TLS.
var cookieSecurePolicy = Enum.TryParse<CookieSecurePolicy>(
    builder.Configuration["Auth:CookieSecurePolicy"], ignoreCase: true, out var parsedSecurePolicy)
    ? parsedSecurePolicy
    : CookieSecurePolicy.SameAsRequest;

// 1. Đăng ký Controllers, Views và Font tiếng Việt (UTF-8)
builder.Services.AddControllersWithViews(options => options.Filters.Add<ContactInfoFilter>());
var appFormattingCulture = CultureInfo.CurrentCulture;
var vietnamese = CultureInfo.GetCultureInfo(UiLanguage.Vietnamese);
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    // Only the UI language changes. Form binding, prices and date parsing keep the app's existing culture.
    options.DefaultRequestCulture = new RequestCulture(appFormattingCulture, vietnamese);
    options.SupportedCultures = [appFormattingCulture];
    options.SupportedUICultures = [vietnamese, CultureInfo.GetCultureInfo(UiLanguage.English)];
    options.RequestCultureProviders = [new CookieRequestCultureProvider { CookieName = UiLanguage.CookieName }];
});
builder.Services.AddScoped<AccountLinks>();
builder.Services.AddSingleton<BookingPromotion>();
>>>>>>> Stashed changes
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/dang-nhap";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "AirlineTicketing.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
<<<<<<< Updated upstream
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
=======
        options.Events.OnValidatePrincipal = async context =>
        {
            var principal = context.Principal;
            var accountId = principal?.TaiKhoanId();
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var account = accountId.HasValue
                ? await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId.Value,
                    context.HttpContext.RequestAborted)
                : null;
            if (account is { TrangThai: TrangThaiTaiKhoan.HoatDong }
                && principal?.FindFirstValue(AccountLinks.CredentialClaim) == AccountLinks.Credential(account.MatKhauHash)
                && principal.FindFirstValue(ClaimTypes.Role) == account.VaiTro.ToString()
                && principal.FindFirstValue(ClaimTypes.Email) == (account.UsesUsernameRegistration() ? null : account.Email))
            {
                if (principal.Identity is ClaimsIdentity identity)
                {
                    RefreshClaim("display_name", account.HoTen);
                    RefreshClaim("khach_hang_id", account.KhachHangId?.ToString(CultureInfo.InvariantCulture));
                    RefreshClaim(AccountLinks.UsernameAccountClaim, account.UsesUsernameRegistration() ? "true" : null);

                    void RefreshClaim(string type, string? value)
                    {
                        var existing = identity.FindFirst(type);
                        if (existing?.Value == value) return;
                        if (existing is not null) identity.RemoveClaim(existing);
                        if (value is not null) identity.AddClaim(new Claim(type, value));
                        context.ShouldRenew = true;
                    }
                }
                return;
            }
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        };
        options.Events.OnRedirectToAccessDenied = context =>
>>>>>>> Stashed changes
        {
            var idValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idValue, out var accountId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var account = await db.TaiKhoans.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == accountId, context.HttpContext.RequestAborted);
            if (account is null || account.TrangThai != TrangThaiTaiKhoan.HoatDong)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var identity = (ClaimsIdentity)context.Principal!.Identity!;
            var role = identity.FindFirst(ClaimTypes.Role);
            if (role?.Value != account.VaiTro.ToString())
            {
                if (role is not null)
                {
                    identity.RemoveClaim(role);
                }
                identity.AddClaim(new Claim(ClaimTypes.Role, account.VaiTro.ToString()));
                context.ShouldRenew = true;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
<<<<<<< Updated upstream
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireStaff", policy => policy.RequireRole("Admin", "NhanVien"));
=======
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(AppRoles.Admin));
    options.AddPolicy("StaffOnly", policy => policy.RequireRole(AppRoles.Admin, AppRoles.Manager));

    // Mặc định mọi endpoint đều yêu cầu đăng nhập; các trang công khai
    // (trang chủ, tìm chuyến, đặt vé, tra cứu) được đánh dấu [AllowAnonymous].
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddSingleton<HtmlEncoder>(HtmlEncoder.Create(UnicodeRanges.All));

// Giới hạn số lần đăng nhập sai theo IP để chặn dò mật khẩu. Cấu hình được
// qua Auth:LoginPermitLimit và Auth:LoginWindowSeconds trong appsettings.json.
var loginPermitLimit = Math.Max(1, builder.Configuration.GetValue<int?>("Auth:LoginPermitLimit") ?? 5);
var loginWindow = TimeSpan.FromSeconds(
    Math.Max(1, builder.Configuration.GetValue<int?>("Auth:LoginWindowSeconds") ?? 60));

// Gửi lại vé qua email là thao tác tốn tài nguyên: giới hạn theo IP để tránh lạm dụng.
var resendPermitLimit = Math.Max(1, builder.Configuration.GetValue<int?>("Auth:ResendPermitLimit") ?? 3);
var resendWindow = TimeSpan.FromSeconds(
    Math.Max(1, builder.Configuration.GetValue<int?>("Auth:ResendWindowSeconds") ?? 300));

builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy(AppRateLimits.Login, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPermitLimit,
                Window = loginWindow,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    limiter.AddPolicy(AppRateLimits.ResendTicket, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = resendPermitLimit,
                Window = resendWindow,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    limiter.AddPolicy(AppRateLimits.AccountLinks, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = loginWindow, QueueLimit = 0 }));

    limiter.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "text/html; charset=utf-8";

        // Chính sách đăng nhập và gửi lại vé có cửa sổ khác nhau, nên chọn mốc chờ theo đường dẫn.
        var retryAfter = context.HttpContext.Request.Path.StartsWithSegments("/ve")
            ? resendWindow
            : loginWindow;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var metadataRetry))
        {
            retryAfter = metadataRetry;
        }

        response.Headers.RetryAfter =
            Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

        var title = UiLanguage.T("Vui lòng thử lại sau", "Please try again later");
        var heading = UiLanguage.T("Bạn thao tác quá nhanh", "Too many requests");
        var wait = UiLanguage.T(
            $"Vì lý do an toàn, vui lòng chờ khoảng {Math.Ceiling(retryAfter.TotalSeconds)} giây rồi thử lại.",
            $"For your security, wait about {Math.Ceiling(retryAfter.TotalSeconds)} seconds before trying again.");
        var home = UiLanguage.T("Về trang chủ", "Back to home");
        await response.WriteAsync($"""
            <!DOCTYPE html><html lang="{UiLanguage.Code}"><head><meta charset="utf-8" />
            <title>{title}</title></head><body>
            <h1>{heading}</h1><p>{wait}</p><p><a href="/">{home}</a></p></body></html>
            """, cancellationToken);
    };
>>>>>>> Stashed changes
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var host = builder.Configuration["POSTGRES_HOST"] ?? "localhost";
    var port = builder.Configuration["POSTGRES_PORT"] ?? "5432";
    var database = builder.Configuration["POSTGRES_DB"] ?? "airline_ticketing";
    var username = builder.Configuration["POSTGRES_USER"] ?? "airline_app";
    var password = builder.Configuration["POSTGRES_PASSWORD"] ?? "";
    connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
<<<<<<< Updated upstream
builder.Services.AddScoped<ITaiKhoanBUS, TaiKhoanBUS>();
builder.Services.AddScoped<IThanhToanBUS, ThanhToanBUS>();
builder.Services.AddScoped<IThongKeBUS, ThongKeBUS>();

// 3. Đăng ký các dịch vụ Business (BUS)
builder.Services.AddScoped<ITaiKhoanBUS, TaiKhoanBUS>();
builder.Services.AddScoped<IThanhToanBUS, ThanhToanBUS>();
builder.Services.AddScoped<IThongKeBUS, ThongKeBUS>();

var app = builder.Build();

=======
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new DatVeOptions
{
    ThoiHanGiuCho = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int?>("Booking:HoldMinutes") ?? 15)
});
builder.Services.AddScoped<CauHinhHeThongBUS>();
builder.Services.AddScoped<DatVeBUS>();
builder.Services.AddScoped<TaiKhoanBUS>();
builder.Services.AddScoped<ThongKeBUS>();
builder.Services.AddScoped<BookingCheckoutService>();
builder.Services.AddScoped<TicketAfterSalesService>();
builder.Services.AddScoped<FlightDisruptionService>();
builder.Services.Configure<VnpayOptions>(builder.Configuration.GetSection("Vnpay"));
builder.Services.AddScoped<VnpayBookingService>();
builder.Services.AddScoped<VnpayOperations>();
builder.Services.AddScoped<PaymentReconciliationService>();
builder.Services.AddHttpClient<VnpayClient>(client => client.Timeout = TimeSpan.FromSeconds(12))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<JourneyBookingBuilder>();
builder.Services.AddScoped<VeBUS>();
builder.Services.AddHostedService<HoldExpiryService>();
var demoEnabled = builder.Configuration.GetValue("Demo:Enabled", builder.Environment.IsDevelopment());
if (demoEnabled && builder.Configuration["App:DefaultArea"] is not ("admin" or "manager"))
    builder.Services.AddHostedService<DemoFlightScheduleWorker>();

// Email vé điện tử: dùng SMTP thật khi có cấu hình, ngược lại ghi ra hộp thư demo
// (Email:OutboxPath) để vẫn kiểm thử được toàn bộ luồng gửi vé.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
var emailOptions = builder.Configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
if (emailOptions.CoCauHinhSmtp)
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, FileEmailSender>();
}
builder.Services.AddScoped<GuiVeDienTuService>();
builder.Services.AddScoped<EmailDeliveryQueue>();
builder.Services.AddScoped<EmailDeliveryProcessor>();
if (builder.Configuration.GetValue("Email:RunWorker", true))
    builder.Services.AddHostedService<EmailDeliveryWorker>();

var app = builder.Build();

// Nạp font tiếng Việt và giấy phép QuestPDF trước khi phục vụ yêu cầu đầu tiên.
VeDienTuPdf.KhoiTao(app.Environment);

// 4. Chế độ một lần cho service "migrate" của Compose: áp dụng migration + seed
// rồi thoát, để các container web không tranh nhau sửa schema.
if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await migrationDb.Database.MigrateAsync();
    if (demoEnabled)
        await DbSeeder.SeedAsync(migrationDb, DateTimeOffset.UtcNow);
    app.Logger.LogInformation("Database migrations applied. Demo data enabled: {DemoEnabled}.", demoEnabled);
    return;
}

>>>>>>> Stashed changes
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
<<<<<<< Updated upstream

    if (app.Environment.IsDevelopment() && app.Configuration.GetValue("Database:SeedSampleData", false))
    {
        await DbSeeder.SeedAsync(dbContext, app.Configuration["Seed:AdminPassword"]);
    }
=======
    if (demoEnabled)
        await DbSeeder.SeedAsync(dbContext, DateTimeOffset.UtcNow);
>>>>>>> Stashed changes
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
<<<<<<< Updated upstream
app.UseAuthentication();
app.UseAuthorization();
=======
app.UseRequestLocalization();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.User.IsInRole(AppRoles.KhachHang)
        && context.User.HasClaim(AccountLinks.UsernameAccountClaim, "true")
        && context.User.KhachHangId() is null
        && context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action
        && action.ControllerName is not ("Profile" or "Language" or "Vnpay")
        && !(action.ControllerName == "Account" && action.ActionName is "Logout" or "LogoutGet"))
    {
        var target = context.Request.PathBase.Add("/ho-so").Value!;
        if (HttpMethods.IsGet(context.Request.Method) && action.ControllerName != "Account")
            target += QueryString.Create("returnUrl", context.Request.GetEncodedPathAndQuery());
        context.Response.Redirect(target);
        return;
    }
    await next();
});

>>>>>>> Stashed changes
app.MapStaticAssets();

app.MapGet("/health/database", async Task<IResult> (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    try
    {
        return await dbContext.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "connected", database = "PostgreSQL" })
            : Results.Problem(title: "Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Database health check failed.");
        return Results.Problem(title: "Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();
