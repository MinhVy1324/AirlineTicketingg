using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.DAL.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

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

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

// 3. Đăng ký các dịch vụ Business (BUS)
builder.Services.AddScoped<ITaiKhoanBUS, TaiKhoanBUS>();
builder.Services.AddScoped<IThanhToanBUS, ThanhToanBUS>();
builder.Services.AddScoped<IThongKeBUS, ThongKeBUS>();

var app = builder.Build();

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

// Authentication phải đứng trước Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

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
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
