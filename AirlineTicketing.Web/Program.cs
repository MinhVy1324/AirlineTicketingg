using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using AirlineTicketing.DAL;
using AirlineTicketing.DAL.Repositories;
using AirlineTicketing.BUS;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.Infrastructure.Email;
using AirlineTicketing.Web.Infrastructure.Tickets;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "aeroviet.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
        options.LoginPath = "/dang-nhap";
        options.AccessDeniedPath = "/dang-nhap";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.Redirect("/dang-nhap?tu-choi=1");
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
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

        await response.WriteAsync(
            "<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\" />" +
            "<title>Vui lòng thử lại sau</title></head><body>" +
            "<h1>Bạn thao tác quá nhanh</h1>" +
            $"<p>Vì lý do an toàn, vui lòng chờ khoảng {Math.Ceiling(retryAfter.TotalSeconds)} giây rồi thử lại.</p>" +
            "<p><a href=\"/\">Về trang chủ</a></p>" +
            "</body></html>",
            cancellationToken);
    };
});

// Session dùng cho luồng đặt vé (giữ chuyến bay, hành khách và hành lý giữa các bước).
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "aeroviet.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

// 2. Lấy ConnectionString: ưu tiên ConnectionStrings__DefaultConnection. Nếu thiếu
// thì ghép từ biến môi trường POSTGRES_* (Docker/`.env`); không lưu mật khẩu trong repo.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    var host = builder.Configuration["POSTGRES_HOST"] ?? "localhost";
    var port = builder.Configuration["POSTGRES_PORT"] ?? "5432";
    var db = builder.Configuration["POSTGRES_DB"] ?? "airline_ticketing";
    var user = builder.Configuration["POSTGRES_USER"] ?? "airline_app";
    var pass = builder.Configuration["POSTGRES_PASSWORD"];

    if (string.IsNullOrWhiteSpace(pass))
    {
        throw new InvalidOperationException(
            "Chưa cấu hình kết nối CSDL. Đặt biến môi trường ConnectionStrings__DefaultConnection " +
            "(hoặc POSTGRES_PASSWORD) trong .env trước khi chạy; xem docs/INSTALL.md.");
    }

    connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={pass}";
}

// 3. Đăng ký AppDbContext & Repositories
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new DatVeOptions
{
    ThoiHanGiuCho = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int?>("Booking:HoldMinutes") ?? 15)
});
builder.Services.AddScoped<CauHinhHeThongBUS>();
builder.Services.AddScoped<DatVeBUS>();
builder.Services.AddScoped<VeBUS>();
builder.Services.AddHostedService<HoldExpiryService>();

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
    await DbSeeder.SeedAsync(migrationDb);
    app.Logger.LogInformation("Database migrations and seed applied.");
    return;
}

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
    await DbSeeder.SeedAsync(dbContext);
}

// 5. Cấu hình Middleware Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Tags every response with the area this container was started for, so the
// parallel Docker instances are easy to tell apart while testing.
var instanceArea = app.Configuration["App:DefaultArea"] ?? "customer";
app.Use(async (context, next) =>
{
    context.Response.Headers["X-App-Area"] = instanceArea;
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// 6. Endpoint kiểm tra sức khỏe CSDL (healthcheck của Docker gọi ẩn danh)
app.MapGet("/health/database", async Task<IResult> (
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return Results.Ok(new { status = "connected", database = "PostgreSQL" });
        }
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Database health check failed.");
    }

    return Results.Problem(
        title: "Database unavailable",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

// 7. Route mặc định MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
