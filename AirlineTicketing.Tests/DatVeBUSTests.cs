using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AirlineTicketing.Tests;

public sealed class DatVeBUSTests
{
    [Fact]
    public async Task TaoDatVe_TinhGiaTrenServer_VaGiuGhe15Phut()
    {
        await using var db = TaoDb();
        var data = await SeedAsync(db);
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero));
        var bus = TaoDatVeBus(db, clock);

        var result = await bus.TaoDatVeAsync(TaoRequest(data.Flight1.Id, "12A"));

        Assert.Equal(data.Flight1.GiaCoBan, result.TongTien);
        Assert.Equal(clock.GetUtcNow().AddMinutes(15), result.HetHanGiuCho);
        Assert.Equal("ChoThanhToan", result.TrangThai);
        Assert.Equal("TamGiu", Assert.Single(result.Ves).TrangThai);
        Assert.Single(await db.LichSuDatVes.ToListAsync());
    }

    [Fact]
    public async Task HetHan_GiaiPhongGhe_VaChoPhepDatLai()
    {
        await using var db = TaoDb();
        var data = await SeedAsync(db);
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero));
        var bus = TaoDatVeBus(db, clock);
        var first = await bus.TaoDatVeAsync(TaoRequest(data.Flight1.Id, "12A"));

        clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(1, await bus.HetHanCacDatVeAsync());
        var second = await bus.TaoDatVeAsync(TaoRequest(data.Flight1.Id, "12A", "second@example.com"));

        Assert.Equal("HetHan", (await db.DatVes.SingleAsync(x => x.Id == first.Id)).TrangThai.ToString());
        Assert.Equal("ChoThanhToan", second.TrangThai);
    }

    [Fact]
    public async Task ThanhToanThatBai_KhongPhatHanh_ThanhCongMoiPhatHanh_VaIdempotent()
    {
        await using var db = TaoDb();
        var data = await SeedAsync(db);
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero));
        var bus = TaoDatVeBus(db, clock);
        var booking = await bus.TaoDatVeAsync(TaoRequest(data.Flight1.Id, "12A"));

        var failed = await bus.XuLyKetQuaThanhToanAsync(new XuLyThanhToanDTO
        {
            MaDatVe = booking.MaDatVe, NhaCungCap = "FakePay", MaGiaoDich = "TX-FAIL", ThanhCong = false
        });
        Assert.False(failed.DaXacNhan);
        Assert.Equal("TamGiu", Assert.Single(failed.DatVe.Ves).TrangThai);

        var successRequest = new XuLyThanhToanDTO
        {
            MaDatVe = booking.MaDatVe, NhaCungCap = "FakePay", MaGiaoDich = "TX-OK", ThanhCong = true
        };
        var success = await bus.XuLyKetQuaThanhToanAsync(successRequest);
        var repeated = await bus.XuLyKetQuaThanhToanAsync(successRequest);

        Assert.True(success.DaXacNhan);
        Assert.Equal("DaPhatHanh", Assert.Single(success.DatVe.Ves).TrangThai);
        Assert.True(repeated.DaXuLyTruocDo);
        Assert.Equal(2, await db.ThanhToans.CountAsync());
    }

    [Fact]
    public async Task GheDaDuocGiu_RequestSauBiTuChoi()
    {
        await using var db = TaoDb();
        var data = await SeedAsync(db, capacity: 1);
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero));
        var bus = TaoDatVeBus(db, clock);
        await bus.TaoDatVeAsync(TaoRequest(data.Flight1.Id, "1A"));

        var error = await Assert.ThrowsAsync<NgoaiLeNghiepVu>(() =>
            bus.TaoDatVeAsync(TaoRequest(data.Flight1.Id, "1B", "second@example.com")));
        Assert.Equal("CHUYEN_BAY_HET_CHO", error.MaLoi);
    }

    [Fact]
    public async Task DoiVaHuyVe_CapNhatGheGiaTongTienVaLichSu()
    {
        await using var db = TaoDb();
        var data = await SeedAsync(db);
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero));
        var bookingBus = TaoDatVeBus(db, clock);
        var booking = await bookingBus.TaoDatVeAsync(TaoRequest(data.Flight1.Id, "12A"));
        var paid = await bookingBus.XuLyKetQuaThanhToanAsync(new XuLyThanhToanDTO
        {
            MaDatVe = booking.MaDatVe, NhaCungCap = "FakePay", MaGiaoDich = "TX-1", ThanhCong = true
        });
        var ticket = Assert.Single(paid.DatVe.Ves);
        var ticketBus = new VeBUS(db, clock, new DatVeOptions());

        var changed = await ticketBus.DoiVeAsync(new DoiVeDTO
        {
            VeId = ticket.Id, ChuyenBayMoiId = data.Flight2.Id, SoGheMoi = "14C",
            HangVeMoi = "ThuongGia", LyDo = "Đổi kế hoạch"
        }, "customer@example.com");
        Assert.Equal("14C", changed.SoGhe);
        Assert.Equal(data.Flight2.GiaCoBan * 1.8m, changed.GiaVe);

        var cancelled = await ticketBus.HuyVeAsync(new HuyVeDTO
        {
            MaVe = changed.MaVe, LyDo = "Không thể bay"
        }, "customer@example.com");
        Assert.Equal("DaHuy", cancelled.TrangThai);
        Assert.Contains(await db.LichSuDatVes.ToListAsync(), x => x.HanhDong == "DoiVe");
        Assert.Contains(await db.LichSuDatVes.ToListAsync(), x => x.HanhDong == "HuyVe" && x.LyDo == "Không thể bay");
    }

    private static AppDbContext TaoDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static DatVeBUS TaoDatVeBus(AppDbContext db, TimeProvider clock) =>
        new(db, clock, new DatVeOptions { ThoiHanGiuCho = TimeSpan.FromMinutes(15) });

    private static TaoDatVeDTO TaoRequest(int flightId, string seat, string email = "customer@example.com") => new()
    {
        NguoiLienHe = new KhachHangDTO { HoTen = "Nguyen Van A", Email = email, SoDienThoai = "0901234567" },
        Ves = [new TaoVeDTO
        {
            ChuyenBayId = flightId, SoGhe = seat, HangVe = "PhoThong",
            HanhKhach = new HanhKhachDTO { HoTen = "Nguyen Van A", NgaySinh = new DateOnly(1990, 1, 1) }
        }]
    };

    private static async Task<TestData> SeedAsync(AppDbContext db, int capacity = 180)
    {
        var from = new SanBay { MaSanBay = $"A{Guid.NewGuid():N}"[..3].ToUpperInvariant(), TenSanBay = "Đi", ThanhPho = "HCM", QuocGia = "VN" };
        var to = new SanBay { MaSanBay = $"B{Guid.NewGuid():N}"[..3].ToUpperInvariant(), TenSanBay = "Đến", ThanhPho = "HN", QuocGia = "VN" };
        var route = new TuyenBay { SanBayDi = from, SanBayDen = to };
        var plane = new MayBay { MaMayBay = $"MB{Guid.NewGuid():N}"[..8], TenMayBay = "A321", HangSanXuat = "Airbus", SoGhe = capacity };
        var flight1 = new ChuyenBay
        {
            SoHieu = $"VN{Random.Shared.Next(100, 999)}", TuyenBay = route, MayBay = plane,
            GioKhoiHanh = new DateTimeOffset(2026, 11, 20, 1, 0, 0, TimeSpan.Zero),
            GioHaCanh = new DateTimeOffset(2026, 11, 20, 3, 0, 0, TimeSpan.Zero), GiaCoBan = 2_000_000m
        };
        var flight2 = new ChuyenBay
        {
            SoHieu = $"VN{Random.Shared.Next(100, 999)}", TuyenBay = route, MayBay = plane,
            GioKhoiHanh = new DateTimeOffset(2026, 11, 21, 1, 0, 0, TimeSpan.Zero),
            GioHaCanh = new DateTimeOffset(2026, 11, 21, 3, 0, 0, TimeSpan.Zero), GiaCoBan = 2_500_000m
        };
        db.AddRange(flight1, flight2);
        await db.SaveChangesAsync();
        return new TestData(flight1, flight2);
    }

    private sealed record TestData(ChuyenBay Flight1, ChuyenBay Flight2);

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
