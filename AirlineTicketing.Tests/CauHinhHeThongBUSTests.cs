using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AirlineTicketing.Tests;

public sealed class CauHinhHeThongBUSTests
{
    [Fact]
    public async Task LayChinhSach_TraVeMacDinhKhiBangTrong()
    {
        await using var db = TaoDb();
        var bus = new CauHinhHeThongBUS(db);

        var chinhSach = await bus.LayChinhSachAsync();

        Assert.Equal(ChinhSachDatVe.DefaultHoldMinutes, chinhSach.HoldMinutes);
        Assert.Equal(ChinhSachDatVe.DefaultServiceFee, chinhSach.ServiceFee);
        Assert.Equal(ChinhSachDatVe.DefaultTax, chinhSach.Tax);
        Assert.Equal(ChinhSachDatVe.DefaultChangeFee, chinhSach.ChangeFee);
        Assert.Equal(ChinhSachDatVe.DefaultRefundFee, chinhSach.RefundFee);
        Assert.Equal(ChinhSachDatVe.DefaultHotline, chinhSach.Hotline);
        Assert.Equal(ChinhSachDatVe.DefaultSupportEmail, chinhSach.SupportEmail);
    }

    [Fact]
    public async Task LayChinhSach_DocGiaTriDaCauHinh()
    {
        await using var db = TaoDb();
        db.CauHinhHeThongs.AddRange(
            TaoCauHinh("booking.hold_minutes", "5"),
            TaoCauHinh("booking.service_fee", "80000"),
            TaoCauHinh("booking.tax_default", "400000"),
            TaoCauHinh("policy.change_fee", "250000"),
            TaoCauHinh("policy.refund_fee", "450000"),
            TaoCauHinh("contact.hotline", "1900 999 888"),
            TaoCauHinh("contact.email", "hotro@aeroviet.vn"));
        await db.SaveChangesAsync();

        var chinhSach = await new CauHinhHeThongBUS(db).LayChinhSachAsync();

        Assert.Equal(5, chinhSach.HoldMinutes);
        Assert.Equal(80_000m, chinhSach.ServiceFee);
        Assert.Equal(400_000m, chinhSach.Tax);
        Assert.Equal(250_000m, chinhSach.ChangeFee);
        Assert.Equal(450_000m, chinhSach.RefundFee);
        Assert.Equal("1900 999 888", chinhSach.Hotline);
        Assert.Equal("hotro@aeroviet.vn", chinhSach.SupportEmail);
    }

    [Fact]
    public async Task TaoDatVe_DungThoiHanGiuTuCauHinh()
    {
        await using var db = TaoDb();
        var flight = await SeedFlightAsync(db);
        db.CauHinhHeThongs.Add(TaoCauHinh("booking.hold_minutes", "5"));
        await db.SaveChangesAsync();

        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero));
        var bus = new DatVeBUS(db, clock, new DatVeOptions(), new CauHinhHeThongBUS(db));

        var booking = await bus.TaoDatVeAsync(new TaoDatVeDTO
        {
            NguoiLienHe = new KhachHangDTO
            {
                HoTen = "Nguyen Van A", Email = "customer@example.com", SoDienThoai = "0901234567"
            },
            Ves = [new TaoVeDTO
            {
                ChuyenBayId = flight.Id, SoGhe = "12A", HangVe = "PhoThong",
                HanhKhach = new HanhKhachDTO { HoTen = "Nguyen Van A", NgaySinh = new DateOnly(1990, 1, 1) }
            }]
        });

        Assert.Equal(clock.GetUtcNow().AddMinutes(5), booking.HetHanGiuCho);
    }

    private static AppDbContext TaoDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static CauHinhHeThong TaoCauHinh(string khoa, string giaTri) => new()
    {
        Khoa = khoa, GiaTri = giaTri, Nhom = "Test", NgayCapNhat = DateTimeOffset.UtcNow
    };

    private static async Task<ChuyenBay> SeedFlightAsync(AppDbContext db)
    {
        var from = new SanBay { MaSanBay = "AAA", TenSanBay = "Đi", ThanhPho = "HCM", QuocGia = "VN" };
        var to = new SanBay { MaSanBay = "BBB", TenSanBay = "Đến", ThanhPho = "HN", QuocGia = "VN" };
        var flight = new ChuyenBay
        {
            SoHieu = "VN123", TuyenBay = new TuyenBay { SanBayDi = from, SanBayDen = to },
            MayBay = new MayBay
            {
                MaMayBay = "MB123456", TenMayBay = "A321", HangSanXuat = "Airbus", SoGhe = 180
            },
            GioKhoiHanh = new DateTimeOffset(2026, 11, 20, 1, 0, 0, TimeSpan.Zero),
            GioHaCanh = new DateTimeOffset(2026, 11, 20, 3, 0, 0, TimeSpan.Zero),
            GiaCoBan = 2_000_000m
        };
        db.ChuyenBays.Add(flight);
        await db.SaveChangesAsync();
        return flight;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
