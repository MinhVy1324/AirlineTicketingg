<<<<<<< Updated upstream
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.DAL.Repositories;
=======
using System.Security.Claims;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.DAL.Repositories;
using AirlineTicketing.Domain;
using Microsoft.AspNetCore.Authentication;
>>>>>>> Stashed changes
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
<<<<<<< Updated upstream

// 1. Cấu hình xác thực người dùng bằng Cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "AirlineTicketing.Auth";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireStaff", policy => policy.RequireRole("Admin", "NhanVien"));
});

// 2. Cấu hình DbContext & Connection String
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=airline_ticketing;Username=airline_app;Password=secret";
=======
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
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
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
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireStaff", policy => policy.RequireRole("Admin", "NhanVien"));
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
>>>>>>> Stashed changes

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<ITaiKhoanBUS, TaiKhoanBUS>();
builder.Services.AddScoped<IThanhToanBUS, ThanhToanBUS>();
builder.Services.AddScoped<IThongKeBUS, ThongKeBUS>();

// 3. Đăng ký các dịch vụ Business (BUS)
builder.Services.AddScoped<ITaiKhoanBUS, TaiKhoanBUS>();
builder.Services.AddScoped<IThanhToanBUS, ThanhToanBUS>();
builder.Services.AddScoped<IThongKeBUS, ThongKeBUS>();

var app = builder.Build();

<<<<<<< Updated upstream
// 4. Tự động áp dụng Migration & Seed dữ liệu mẫu khi cấu hình cho phép
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();

        if (app.Configuration.GetValue<bool>("Database:SeedSampleData", true))
        {
            await DbSeeder.SeedAsync(dbContext);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Chưa thể kết nối PostgreSQL để chạy migration/seeder lúc khởi động.");
=======
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    if (app.Environment.IsDevelopment() && app.Configuration.GetValue("Database:SeedSampleData", false))
    {
        await DbSeeder.SeedAsync(dbContext, app.Configuration["Seed:AdminPassword"]);
>>>>>>> Stashed changes
    }
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

// Authentication phải đứng trước Authorization
=======
>>>>>>> Stashed changes
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

<<<<<<< Updated upstream
app.MapGet("/health/database", async Task<IResult> (
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
=======
app.MapGet("/health/database", async Task<IResult> (AppDbContext dbContext, CancellationToken cancellationToken) =>
>>>>>>> Stashed changes
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

<<<<<<< Updated upstream
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
=======
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
>>>>>>> Stashed changes

app.Run();
