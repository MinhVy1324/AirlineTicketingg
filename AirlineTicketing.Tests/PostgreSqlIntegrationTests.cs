using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.DAL.Security;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit.Sdk;

namespace AirlineTicketing.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(TestDatabase.ConnectionString))
        {
            Skip = "Set AIRLINETICKETING_TEST_CONNECTION to run isolated PostgreSQL integration tests.";
        }
    }
}

public sealed class PostgreSqlIntegrationTests
{
    [PostgreSqlFact]
    public async Task Payment_flow_is_idempotent_and_refunds_reduce_net_revenue()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (bookingId, ticketId) = await database.AddBookingAsync("payment");
        await using var context = database.CreateContext();
        var payments = new ThanhToanBUS(context);

        var first = await payments.KhoiTaoThanhToanAsync(bookingId, PhuongThucThanhToan.ChuyenKhoan, "VNPAY");
        var duplicate = await payments.KhoiTaoThanhToanAsync(bookingId, PhuongThucThanhToan.ViDienTu, "MOMO");
        Assert.Equal(first.Id, duplicate.Id);
        Assert.Equal(1, await context.ThanhToans.CountAsync(x => x.DatVeId == bookingId));

        var success = await payments.XuLyThanhToanGiaLapAsync(first.Id, true, "TEST-TXN-001");
        var replay = await payments.XuLyThanhToanGiaLapAsync(first.Id, true, "TEST-TXN-REPLAY");
        Assert.True(success.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal("TEST-TXN-001", replay.MaGiaoDich);

        var partialRefund = await payments.TaoHoanTienAsync(
            new TaoHoanTienDTO { ThanhToanId = first.Id, SoTien = 100_000m, LyDo = "Integration partial refund" }, "test-admin");
        Assert.Equal(100_000m, partialRefund.SoTien);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var statistics = new ThongKeBUS(context);
        var partialSummary = await statistics.GetDoanhThuTongQuanAsync(today, today);
        var daily = await statistics.GetDoanhThuTheoNgayAsync(today, today);
        var monthly = await statistics.GetDoanhThuTheoThangAsync(today.Year);
        var byFlight = await statistics.GetDoanhThuTheoChuyenBayAsync();
        Assert.Equal(200_000m, partialSummary.DoanhThuThuan);
        Assert.Equal(200_000m, Assert.Single(daily).DoanhThuThuan);
        Assert.Equal(200_000m, monthly[today.Month - 1].DoanhThuThuan);
        Assert.Equal(200_000m, Assert.Single(byFlight).DoanhThu);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await payments.TaoHoanTienAsync(
                new TaoHoanTienDTO { ThanhToanId = first.Id, SoTien = 200_001m, LyDo = "Over refund" });
        });

        var fullRefund = await payments.TaoHoanTienAsync(
            new TaoHoanTienDTO { ThanhToanId = first.Id, SoTien = 200_000m, LyDo = "Complete refund" }, "test-admin");
        Assert.Equal(200_000m, fullRefund.SoTien);

        await using var verification = database.CreateContext();
        var booking = await verification.DatVes.SingleAsync(x => x.Id == bookingId);
        var ticket = await verification.Ves.SingleAsync(x => x.Id == ticketId);
        Assert.Equal(TrangThaiDatVe.HoanTien, booking.TrangThai);
        Assert.Equal(TrangThaiVe.DaHoanTien, ticket.TrangThai);

        var revenue = await new ThongKeBUS(verification).GetDoanhThuTongQuanAsync(today, today);
        Assert.Equal(300_000m, revenue.TongTienDaThu);
        Assert.Equal(300_000m, revenue.TongTienDaHoan);
        Assert.Equal(0m, revenue.DoanhThuThuan);
    }

    [PostgreSqlFact]
    public async Task Failed_payment_does_not_confirm_booking_or_issue_ticket()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (bookingId, ticketId) = await database.AddBookingAsync("failed");
        await using var context = database.CreateContext();
        var payments = new ThanhToanBUS(context);
        var payment = await payments.KhoiTaoThanhToanAsync(bookingId, PhuongThucThanhToan.TheNganHang, "MoMo");

        var result = await payments.XuLyThanhToanGiaLapAsync(payment.Id, false);
        var replay = await payments.XuLyThanhToanGiaLapAsync(payment.Id, true, "LATE-SUCCESS");

        Assert.False(result.IsSuccess);
        Assert.False(replay.IsSuccess);
        Assert.Equal(TrangThaiThanhToan.ThatBai, Enum.Parse<TrangThaiThanhToan>(replay.TrangThai));

        await using var verification = database.CreateContext();
        Assert.Equal(TrangThaiDatVe.ChoThanhToan, (await verification.DatVes.SingleAsync(x => x.Id == bookingId)).TrangThai);
        Assert.Equal(TrangThaiVe.TamGiu, (await verification.Ves.SingleAsync(x => x.Id == ticketId)).TrangThai);
    }

    [PostgreSqlFact]
    public async Task Account_registration_hashes_password_and_authenticates_case_insensitively()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var accounts = new TaiKhoanBUS(context);
        var username = $"Customer_{Guid.NewGuid():N}"[..20];
        const string password = "Safe-Integration-Pass-2026!";

        var account = await accounts.DangKyAsync(new DangKyDTO
        {
            TenDangNhap = username.ToUpperInvariant(),
            MatKhau = password,
            XacNhanMatKhau = password,
            HoTen = "Integration Customer",
            Email = $"{Guid.NewGuid():N}@example.test",
            SoDienThoai = "0901234567"
        });

        var persisted = await context.TaiKhoans.SingleAsync(x => x.Id == account.Id);
        Assert.Equal(username.ToLowerInvariant(), persisted.TenDangNhap);
        Assert.NotEqual(password, persisted.MatKhauHash);
        Assert.True(PasswordSecurity.VerifyPassword(password, persisted.MatKhauHash));
        Assert.NotNull(await accounts.DangNhapAsync(username.ToUpperInvariant(), password));
        Assert.Null(await accounts.DangNhapAsync(username, "incorrect-password"));
    }

    [PostgreSqlFact]
    public async Task Only_active_admin_cannot_be_demoted_or_locked()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var admin = new TaiKhoan
        {
            TenDangNhap = "only-admin",
            MatKhauHash = PasswordSecurity.HashPassword("Safe-Integration-Admin-2026!"),
            HoTen = "Only Admin",
            Email = "only-admin@example.test",
            SoDienThoai = "",
            VaiTro = VaiTroTaiKhoan.Admin,
            TrangThai = TrangThaiTaiKhoan.HoatDong,
            NgayTao = DateTimeOffset.UtcNow
        };
        context.TaiKhoans.Add(admin);
        await context.SaveChangesAsync();
        var accounts = new TaiKhoanBUS(context);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await accounts.CapNhatVaiTroAsync(admin.Id, VaiTroTaiKhoan.NhanVien);
        });
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await accounts.CapNhatTrangThaiAsync(admin.Id, TrangThaiTaiKhoan.BiKhoa);
        });
        Assert.Equal(VaiTroTaiKhoan.Admin, (await context.TaiKhoans.SingleAsync()).VaiTro);
        Assert.Equal(TrangThaiTaiKhoan.HoatDong, admin.TrangThai);
    }
}

internal sealed class TestDatabase : IAsyncDisposable
{
    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("AIRLINETICKETING_TEST_CONNECTION")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

    private readonly string _schema;
    private readonly string _connectionString;
    private bool _disposed;

    private TestDatabase(string schema, string connectionString)
    {
        _schema = schema;
        _connectionString = connectionString;
    }

    public static async Task<TestDatabase> CreateAsync()
    {
        var baseConnectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            throw new InvalidOperationException("AIRLINETICKETING_TEST_CONNECTION is required.");
        }

        string schema = $"airline_test_{Guid.NewGuid():N}";
        await using var adminConnection = new NpgsqlConnection(baseConnectionString);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await create.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schema,
            Pooling = false
        };
        var database = new TestDatabase(schema, builder.ConnectionString);
        try
        {
            await using var context = database.CreateContext();
            await context.Database.MigrateAsync();
            return database;
        }
        catch
        {
            await database.DropSchemaAsync();
            throw;
        }
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new AppDbContext(options);
    }

    public async Task<(int BookingId, int TicketId)> AddBookingAsync(string prefix)
    {
        await using var context = CreateContext();
        var origin = new SanBay
        {
            MaSanBay = "SGN",
            TenSanBay = "Tan Son Nhat",
            ThanhPho = "Ho Chi Minh City",
            QuocGia = "Vietnam",
            DangHoatDong = true
        };
        var destination = new SanBay
        {
            MaSanBay = "HAN",
            TenSanBay = "Noi Bai",
            ThanhPho = "Hanoi",
            QuocGia = "Vietnam",
            DangHoatDong = true
        };
        var route = new TuyenBay
        {
            SanBayDi = origin,
            SanBayDen = destination,
            ThoiLuongDuKienPhut = 120,
            DangKhaiThac = true
        };
        var aircraft = new MayBay
        {
            MaMayBay = "TEST-A321",
            TenMayBay = "Test Airbus",
            HangSanXuat = "Airbus",
            SoGhe = 180,
            DangHoatDong = true
        };
        var flight = new ChuyenBay
        {
            SoHieu = $"T-{Guid.NewGuid():N}"[..12],
            TuyenBay = route,
            MayBay = aircraft,
            GioKhoiHanh = DateTimeOffset.UtcNow.AddDays(2),
            GioHaCanh = DateTimeOffset.UtcNow.AddDays(2).AddHours(2),
            GiaCoBan = 300_000m,
            TrangThai = TrangThaiChuyenBay.DuKien
        };
        var customer = new KhachHang
        {
            HoTen = "Integration Customer",
            Email = $"{Guid.NewGuid():N}@example.test",
            SoDienThoai = "0901234567",
            NgayTao = DateTimeOffset.UtcNow
        };
        var passenger = new HanhKhach { HoTen = "Integration Passenger", QuocTich = "Vietnam" };
        var booking = new DatVe
        {
            MaDatVe = $"TEST-{prefix}-{Guid.NewGuid():N}"[..30],
            KhachHang = customer,
            NgayDat = DateTimeOffset.UtcNow,
            TrangThai = TrangThaiDatVe.ChoThanhToan,
            TongTien = 300_000m,
            DonViTienTe = "VND"
        };
        var ticket = new Ve
        {
            MaVe = $"HOLD-{Guid.NewGuid():N}"[..30],
            ChuyenBay = flight,
            DatVe = booking,
            HanhKhach = passenger,
            SoGhe = "1A",
            HangVe = "PhoThong",
            GiaVe = 300_000m,
            TrangThai = TrangThaiVe.TamGiu
        };

        context.Ves.Add(ticket);
        await context.SaveChangesAsync();
        return (booking.Id, ticket.Id);
    }

    private async Task DropSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE";
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DropSchemaAsync();
    }
}
