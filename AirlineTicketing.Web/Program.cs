using AirlineTicketing.DAL;
using AirlineTicketing.DAL.Repositories;
using AirlineTicketing.BUS;
using AirlineTicketing.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký Controllers, Views và Font tiếng Việt (UTF-8)
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "aeroviet.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
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
});
builder.Services.AddSingleton<HtmlEncoder>(HtmlEncoder.Create(UnicodeRanges.All));

// Session dùng cho luồng đặt vé (giữ chuyến bay, hành khách và hành lý giữa các bước).
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "aeroviet.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

// 2. Lấy ConnectionString: Ưu tiên DefaultConnection -> Nếu thiếu thì tự ghép từ biến môi trường (.env)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    var host = builder.Configuration["POSTGRES_HOST"] ?? "localhost";
    var port = builder.Configuration["POSTGRES_PORT"] ?? "5432";
    var db = builder.Configuration["POSTGRES_DB"] ?? "airline_ticketing";
    var user = builder.Configuration["POSTGRES_USER"] ?? "airline_app";
    var pass = builder.Configuration["POSTGRES_PASSWORD"] ?? "123";

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
builder.Services.AddScoped<DatVeBUS>();
builder.Services.AddScoped<VeBUS>();

var app = builder.Build();

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
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// 6. Endpoint kiểm tra sức khỏe CSDL
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

// 7. Route mặc định MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
