// using AirlineTicketing.DAL;
// using AirlineTicketing.DAL.Repositories;
// using Microsoft.EntityFrameworkCore;
// using System.Text.Encodings.Web;
// using System.Text.Unicode;

// var builder = WebApplication.CreateBuilder(args);
// builder.Services.AddDbContext<AppDbContext>(options =>
//     options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// builder.Services.AddControllersWithViews();
// builder.Services.AddAuthorization();
// builder.Services.AddSingleton<HtmlEncoder>(HtmlEncoder.Create(UnicodeRanges.All));

// var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
//     ?? throw new InvalidOperationException(
//         "Connection string 'DefaultConnection' was not found. Set ConnectionStrings__DefaultConnection.");

// builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
// builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

// var app = builder.Build();

// if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
// {
//     await using var scope = app.Services.CreateAsyncScope();
//     var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//     await dbContext.Database.MigrateAsync();
// }

// if (!app.Environment.IsDevelopment())
// {
//     app.UseExceptionHandler("/Home/Error");
//     app.UseHsts();
// }
// app.UseHttpsRedirection();
// app.UseStaticFiles();
// app.UseRouting();
// app.UseAuthorization();

// app.MapStaticAssets();

// app.MapGet("/health/database", async Task<IResult> (
//     AppDbContext dbContext,
//     CancellationToken cancellationToken) =>
// {
//     try
//     {
//         if (await dbContext.Database.CanConnectAsync(cancellationToken))
//         {
//             return Results.Ok(new { status = "connected", database = "PostgreSQL" });
//         }
//     }
//     catch (Exception exception)
//     {
//         app.Logger.LogError(exception, "Database health check failed.");
//     }

//     return Results.Problem(
//         title: "Database unavailable",
//         statusCode: StatusCodes.Status503ServiceUnavailable);
// });

// app.MapControllerRoute(
//     name: "default",
//     pattern: "{controller=Home}/{action=Index}/{id?}");

// app.Run();
using AirlineTicketing.DAL;
using AirlineTicketing.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký Controllers, Views, session và Font tiếng Việt (UTF-8)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddControllersWithViews().AddSessionStateTempDataProvider();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<HtmlEncoder>(HtmlEncoder.Create(UnicodeRanges.All));

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

var app = builder.Build();

// 4. Hỗ trợ --migrate (one-shot cho Docker) và tự áp dụng migration khi khởi động.
if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await migrationDb.Database.MigrateAsync();
    app.Logger.LogInformation("Database migrations applied.");
    return;
}

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

// 5. Cấu hình Middleware Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
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
