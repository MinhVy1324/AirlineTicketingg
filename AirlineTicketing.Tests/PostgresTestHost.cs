using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketing.Tests;

/// <summary>
/// Dựng một host ASP.NET Core thật (qua <see cref="WebApplicationFactory{TEntryPoint}"/>)
/// trỏ vào database PostgreSQL test, đồng thời tạo dữ liệu mẫu riêng cho từng test.
/// </summary>
internal sealed class PostgresTestHost : IAsyncDisposable
{
    private readonly TestApplicationFactory _factory;
    private readonly string _temporaryRoot;

    private PostgresTestHost(TestApplicationFactory factory, string temporaryRoot, TestData data)
    {
        _factory = factory;
        _temporaryRoot = temporaryRoot;
        Data = data;
    }

    public TestData Data { get; }

    public static async Task<PostgresTestHost> CreateAsync()
    {
        var connection = PostgresTestConnection.Require();
        var data = await TestDataSeeder.SeedAsync(connection);
        var root = Path.Combine(Path.GetTempPath(), "airline-ticketing-tests", Guid.NewGuid().ToString("N"));
        var factory = new TestApplicationFactory(connection, root);
        return new PostgresTestHost(factory, root, data);
    }

    public HttpClient CreateClient(string? role = null, string? email = null, int? khachHangId = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeader, role);
        }

        if (email is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.EmailHeader, email);
        }

        if (khachHangId.HasValue)
        {
            client.DefaultRequestHeaders.Add(
                TestAuthenticationHandler.KhachHangIdHeader, khachHangId.Value.ToString());
        }

        return client;
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();

        try
        {
            if (Directory.Exists(_temporaryRoot))
            {
                Directory.Delete(_temporaryRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Thư mục tạm bị khóa trên Windows không ảnh hưởng kết quả test.
        }

        return ValueTask.CompletedTask;
    }

    private sealed class TestApplicationFactory(string connection, string temporaryRoot)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var keysPath = Path.Combine(temporaryRoot, "keys");
            var outboxPath = Path.Combine(temporaryRoot, "outbox");

            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connection);
            builder.UseSetting("DataProtection:KeysPath", keysPath);
            builder.UseSetting("Email:OutboxPath", outboxPath);
            builder.UseSetting("App:DefaultArea", "test");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connection,
                    ["DataProtection:KeysPath"] = keysPath,
                    ["Email:OutboxPath"] = outboxPath,
                    ["Database:ApplyMigrationsOnStartup"] = "false"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName, _ => { });
            });
        }
    }
}

internal sealed record TestData(
    int CustomerAId,
    string CustomerAEmail,
    string PnrA,
    int CustomerBId,
    string CustomerBEmail,
    string PnrB);

internal static class TestDataSeeder
{
    public static async Task<TestData> SeedAsync(string connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var marker = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var emailA = $"khach-a-{marker.ToLowerInvariant()}@aeroviet.test";
        var emailB = $"khach-b-{marker.ToLowerInvariant()}@aeroviet.test";

        var customerA = new KhachHang
        {
            HoTen = "Khách hàng A",
            Email = emailA,
            SoDienThoai = "0900000001",
            NgayTao = now
        };
        var customerB = new KhachHang
        {
            HoTen = "Khách hàng B",
            Email = emailB,
            SoDienThoai = "0900000002",
            NgayTao = now
        };

        var departure = now.AddDays(30);
        var flight = new ChuyenBay
        {
            SoHieu = $"AV{marker[..5]}",
            TuyenBay = new TuyenBay
            {
                SanBayDi = new SanBay
                {
                    MaSanBay = MaSanBayNgauNhien(),
                    TenSanBay = "Sân bay Nội Bài",
                    ThanhPho = "Hà Nội",
                    QuocGia = "Việt Nam"
                },
                SanBayDen = new SanBay
                {
                    MaSanBay = MaSanBayNgauNhien(),
                    TenSanBay = "Sân bay Tân Sơn Nhất",
                    ThanhPho = "TP. Hồ Chí Minh",
                    QuocGia = "Việt Nam"
                },
                ThoiLuongDuKienPhut = 130
            },
            HangHangKhong = new HangHangKhong
            {
                MaHang = $"H{marker[..4]}",
                TenHang = "AeroViet Test",
                QuocGia = "Việt Nam"
            },
            MayBay = new MayBay
            {
                MaMayBay = $"MB-{marker}",
                TenMayBay = "Airbus A321",
                HangSanXuat = "Airbus",
                SoGhe = 200
            },
            GioKhoiHanh = departure,
            GioHaCanh = departure.AddHours(2).AddMinutes(10),
            GiaCoBan = 1_500_000m,
            TrangThai = TrangThaiChuyenBay.DuKien
        };

        var pnrA = $"PNR{marker}A";
        var pnrB = $"PNR{marker}B";

        db.AddRange(
            TaoDatVe(customerA, flight, pnrA, $"VE{marker}A", "1A", emailA, now),
            TaoDatVe(customerB, flight, pnrB, $"VE{marker}B", "2A", emailB, now));
        await db.SaveChangesAsync();

        return new TestData(customerA.Id, emailA, pnrA, customerB.Id, emailB, pnrB);
    }

    private static DatVe TaoDatVe(
        KhachHang khachHang,
        ChuyenBay flight,
        string pnr,
        string maVe,
        string soGhe,
        string email,
        DateTimeOffset now) => new()
    {
        MaDatVe = pnr,
        KhachHang = khachHang,
        NgayDat = now,
        HetHanGiuCho = now.AddMinutes(30),
        NgayXacNhan = now,
        TrangThai = TrangThaiDatVe.DaXacNhan,
        TongTien = 1_500_000m,
        DonViTienTe = "VND",
        ThuePhi = 100_000m,
        PhiDichVu = 50_000m,
        PhiHanhLy = 0m,
        EmailLienHe = email,
        SoDienThoaiLienHe = "0900000001",
        Ves =
        [
            new Ve
            {
                MaVe = maVe,
                ChuyenBay = flight,
                HanhKhach = new HanhKhach
                {
                    HoTen = khachHang.HoTen,
                    Email = email,
                    SoDienThoai = khachHang.SoDienThoai,
                    QuocTich = "Việt Nam"
                },
                SoGhe = soGhe,
                HangVe = "PhoThong",
                GiaVe = 1_350_000m,
                TrangThai = TrangThaiVe.DaPhatHanh,
                HanhLyXachTayKg = 7,
                HanhLyKyGuiKg = 20,
                NgayPhatHanh = now
            }
        ]
    };

    private static string MaSanBayNgauNhien()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var value = Random.Shared.Next(0, 36 * 36 * 36);
        Span<char> code = stackalloc char[3];
        for (var i = 2; i >= 0; i--)
        {
            code[i] = alphabet[value % 36];
            value /= 36;
        }

        return new string(code);
    }
}

internal static class PostgresTestConnection
{
    public static string Require() =>
        Environment.GetEnvironmentVariable("AIRLINE_TEST_CONNECTION")
        ?? throw new InvalidOperationException(
            "Đặt AIRLINE_TEST_CONNECTION trỏ tới database PostgreSQL có chữ 'test' để chạy test tích hợp.");
}
