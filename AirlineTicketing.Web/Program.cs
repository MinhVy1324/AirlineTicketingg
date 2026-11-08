using AirlineTicketing.DAL;
using AirlineTicketing.DAL.Repositories;
using AirlineTicketing.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);

// Controllers/Views, session (booking flow), auth (GĐ2), UTF-8
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddControllersWithViews().AddSessionStateTempDataProvider();

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

// ConnectionString: DefaultConnection -> fallback from POSTGRES_* env vars (.env)
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

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

var app = builder.Build();

// One-shot migrate mode (Compose migrate service / parallel startup safety)
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Tag responses with instance area (parallel Docker test instances)
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
