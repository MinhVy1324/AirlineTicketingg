using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AirlineTicketing.Tests;

/// <summary>
/// Chạy bằng: AIRLINE_TEST_CONNECTION="...Database=airline_ticketing_test..." dotnet test
/// Database phải dành riêng cho test. Test này xác minh unique index thật của PostgreSQL.
/// </summary>
public sealed class PostgresSeatConcurrencyTests
{
    [PostgresFact]
    public async Task HaiRequestDongThoi_CungMotGhe_ChiMotRequestThanhCong()
    {
        var connection = Environment.GetEnvironmentVariable("AIRLINE_TEST_CONNECTION")!;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        await using (var setup = new AppDbContext(dbOptions))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        var flightId = await SeedFlightAsync(dbOptions);
        var now = new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);
        var attempts = await Task.WhenAll(
            AttemptAsync(dbOptions, flightId, "race-a@example.com", now),
            AttemptAsync(dbOptions, flightId, "race-b@example.com", now));

        Assert.Single(attempts, x => x is null);
        Assert.Single(attempts, x => x is GheKhongConException);
    }

    private static async Task<Exception?> AttemptAsync(DbContextOptions<AppDbContext> options, int flightId,
        string email, DateTimeOffset now)
    {
        await using var db = new AppDbContext(options);
        var bus = new DatVeBUS(db, new FixedTimeProvider(now), new DatVeOptions());
        try
        {
            await bus.TaoDatVeAsync(new TaoDatVeDTO
            {
                NguoiLienHe = new KhachHangDTO { HoTen = "Race Test", Email = email, SoDienThoai = "0900000000" },
                Ves = [new TaoVeDTO
                {
                    ChuyenBayId = flightId, SoGhe = "9A", HangVe = "PhoThong",
                    HanhKhach = new HanhKhachDTO { HoTen = email }
                }]
            });
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task<int> SeedFlightAsync(DbContextOptions<AppDbContext> options)
    {
        await using var db = new AppDbContext(options);
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var from = new SanBay { MaSanBay = $"A{suffix}"[..3], TenSanBay = "Race A", ThanhPho = "A", QuocGia = "VN" };
        var to = new SanBay { MaSanBay = $"B{suffix}"[..3], TenSanBay = "Race B", ThanhPho = "B", QuocGia = "VN" };
        var flight = new ChuyenBay
        {
            SoHieu = $"RC{suffix}", TuyenBay = new TuyenBay { SanBayDi = from, SanBayDen = to },
            MayBay = new MayBay { MaMayBay = $"MB{suffix}", TenMayBay = "Test", HangSanXuat = "Test", SoGhe = 10 },
            GioKhoiHanh = new DateTimeOffset(2026, 12, 1, 1, 0, 0, TimeSpan.Zero),
            GioHaCanh = new DateTimeOffset(2026, 12, 1, 2, 0, 0, TimeSpan.Zero), GiaCoBan = 1_000_000m
        };
        db.Add(flight);
        await db.SaveChangesAsync();
        return flight.Id;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        var value = Environment.GetEnvironmentVariable("AIRLINE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(value))
        {
            Skip = "Đặt AIRLINE_TEST_CONNECTION để chạy test cạnh tranh trên PostgreSQL.";
            return;
        }
        try
        {
            var database = new NpgsqlConnectionStringBuilder(value).Database;
            if (string.IsNullOrWhiteSpace(database) || !database.Contains("test", StringComparison.OrdinalIgnoreCase))
                Skip = "AIRLINE_TEST_CONNECTION phải trỏ tới database có chữ 'test'.";
        }
        catch
        {
            Skip = "AIRLINE_TEST_CONNECTION không hợp lệ.";
        }
    }
}
