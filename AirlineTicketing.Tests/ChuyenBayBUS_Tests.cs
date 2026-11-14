using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.DAL.Repositories;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Tests;

internal static class TestFixture
{
    public static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    public static AppDbContext CreateDb() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"airline-tests-{Guid.NewGuid():N}")
            .Options);

    public static ChuyenBayBUS CreateFlightBus(AppDbContext db) => new(
        new ChuyenBayDAL(db),
        new SanBayDAL(db),
        new MayBayDAL(db),
        new TuyenBayDAL(db),
        new HangHangKhongDAL(db));

    public static DanhMucBUS CreateCatalogBus(AppDbContext db) => new(
        new SanBayDAL(db),
        new MayBayDAL(db),
        new TuyenBayDAL(db),
        new ChuyenBayDAL(db),
        new HangHangKhongDAL(db));

    public static (SanBay Sgn, SanBay Han, MayBay Aircraft, TuyenBay Route, ChuyenBay Flight) Seed(
        AppDbContext db,
        int seats = 180,
        DateTimeOffset? departure = null,
        bool routeActive = true,
        bool aircraftActive = true,
        bool airportsActive = true)
    {
        var sgn = new SanBay
        {
            MaSanBay = "SGN",
            TenSanBay = "Tân Sơn Nhất",
            ThanhPho = "TP. Hồ Chí Minh",
            QuocGia = "Việt Nam",
            DangHoatDong = airportsActive
        };
        var han = new SanBay
        {
            MaSanBay = "HAN",
            TenSanBay = "Nội Bài",
            ThanhPho = "Hà Nội",
            QuocGia = "Việt Nam",
            DangHoatDong = airportsActive
        };
        var aircraft = new MayBay
        {
            MaMayBay = "A321",
            TenMayBay = "Airbus A321",
            HangSanXuat = "Airbus",
            SoGhe = seats,
            DangHoatDong = aircraftActive
        };
        var route = new TuyenBay
        {
            SanBayDi = sgn,
            SanBayDen = han,
            ThoiLuongDuKienPhut = 130,
            DangKhaiThac = routeActive
        };
        var airline = new HangHangKhong
        {
            MaHang = "VN",
            TenHang = "Vietnam Airlines",
            MaIcao = "HVN",
            QuocGia = "Việt Nam",
            DangHoatDong = true
        };

        var start = (departure ?? new DateTimeOffset(2026, 11, 20, 8, 0, 0, Vietnam)).ToUniversalTime();
        var flight = new ChuyenBay
        {
            SoHieu = "VN210",
            TuyenBay = route,
            MayBay = aircraft,
            HangHangKhong = airline,
            GioKhoiHanh = start,
            GioHaCanh = start.AddHours(2),
            GiaCoBan = 1_500_000m,
            TrangThai = TrangThaiChuyenBay.DuKien
        };

        db.AddRange(sgn, han, aircraft, route, flight);
        db.SaveChanges();
        return (sgn, han, aircraft, route, flight);
    }

    public static void AddTickets(AppDbContext db, ChuyenBay flight, int sold, int held)
    {
        var customer = new KhachHang
        {
            HoTen = "Khách kiểm thử",
            Email = $"test-{Guid.NewGuid():N}@example.com",
            SoDienThoai = "0900000000",
            NgayTao = DateTimeOffset.UtcNow
        };
        var booking = new DatVe
        {
            MaDatVe = $"DV-{Guid.NewGuid():N}"[..20],
            KhachHang = customer,
            NgayDat = DateTimeOffset.UtcNow,
            TrangThai = TrangThaiDatVe.DaXacNhan,
            TongTien = (sold + held) * flight.GiaCoBan,
            DonViTienTe = "VND"
        };

        db.Add(customer);
        db.Add(booking);

        var seats = Enumerable.Range(1, sold + held).Select(index => $"{index}A").ToArray();
        for (var i = 0; i < seats.Length; i++)
        {
            var passenger = new HanhKhach
            {
                HoTen = $"Hành khách {i + 1}",
                QuocTich = "Việt Nam",
                SoHoChieu = $"P{Guid.NewGuid():N}"[..12]
            };

            db.Add(passenger);
            db.Add(new Ve
            {
                MaVe = $"VE-{Guid.NewGuid():N}"[..20],
                ChuyenBay = flight,
                DatVe = booking,
                HanhKhach = passenger,
                SoGhe = seats[i],
                HangVe = "PhoThong",
                GiaVe = flight.GiaCoBan,
                TrangThai = i < sold ? TrangThaiVe.DaPhatHanh : TrangThaiVe.TamGiu
            });
        }

        db.SaveChanges();
    }
}

public class ChuyenBayRuleTests
{
    [Fact]
    public void KiemTraThoiGian_ChapNhanHaCanhSauKhoiHanh()
    {
        var departure = new DateTimeOffset(2026, 11, 20, 8, 0, 0, TestFixture.Vietnam);
        Assert.Null(ChuyenBayBUS.KiemTraThoiGian(departure, departure.AddMinutes(130)));
    }

    [Fact]
    public void KiemTraThoiGian_TuChoiHaCanhTruocHoacBangKhoiHanh()
    {
        var departure = new DateTimeOffset(2026, 11, 20, 8, 0, 0, TestFixture.Vietnam);
        Assert.NotNull(ChuyenBayBUS.KiemTraThoiGian(departure, departure));
        Assert.NotNull(ChuyenBayBUS.KiemTraThoiGian(departure, departure.AddMinutes(-1)));
    }

    [Fact]
    public void KiemTraGia_TuChoiGiaAm()
    {
        Assert.Null(ChuyenBayBUS.KiemTraGia(0));
        Assert.NotNull(ChuyenBayBUS.KiemTraGia(-1));
    }

    [Fact]
    public void KiemTraTuyen_TuChoiThieuHoacTrungSanBay()
    {
        Assert.NotNull(ChuyenBayBUS.KiemTraTuyen(0, 1));
        Assert.NotNull(ChuyenBayBUS.KiemTraTuyen(1, 0));
        Assert.NotNull(ChuyenBayBUS.KiemTraTuyen(1, 1));
        Assert.Null(ChuyenBayBUS.KiemTraTuyen(1, 2));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public void KiemTraSoKhach_ChiChapNhan1Den9(int count, bool valid)
    {
        Assert.Equal(valid, ChuyenBayBUS.KiemTraSoKhach(count) is null);
    }

    [Theory]
    [InlineData(180, 147, 0, 33)]
    [InlineData(180, 170, 10, 0)]
    [InlineData(10, 0, 0, 10)]
    public void TinhSoGheTrong_KhongAm(int total, int sold, int held, int expected)
    {
        Assert.Equal(expected, ChuyenBayBUS.TinhSoGheTrong(total, sold, held));
    }

    [Theory]
    [InlineData(TrangThaiChuyenBay.DuKien, "Mở bán")]
    [InlineData(TrangThaiChuyenBay.DangBay, "Đang bay")]
    [InlineData(TrangThaiChuyenBay.HoanTat, "Hoàn tất")]
    [InlineData(TrangThaiChuyenBay.BiHuy, "Đã hủy")]
    public void HienThiTrangThai_DungNhan(TrangThaiChuyenBay status, string expected)
    {
        Assert.Equal(expected, ChuyenBayBUS.HienThiTrangThai(status));
    }
}

public class ChuyenBaySearchTests
{
    [Fact]
    public async Task TimChuyenBay_LocTheoNgayTheoMuiGioVietNam()
    {
        using var db = TestFixture.CreateDb();
        var (_, _, _, route, _) = TestFixture.Seed(db);

        var aircraft = new MayBay
        {
            MaMayBay = "A320",
            TenMayBay = "Airbus A320",
            HangSanXuat = "Airbus",
            SoGhe = 180,
            DangHoatDong = true
        };
        db.Add(aircraft);
        db.AddRange(
            new ChuyenBay
            {
                SoHieu = "VN001",
                TuyenBayId = route.Id,
                MayBay = aircraft,
                GioKhoiHanh = new DateTimeOffset(2026, 11, 20, 0, 30, 0, TestFixture.Vietnam).ToUniversalTime(),
                GioHaCanh = new DateTimeOffset(2026, 11, 20, 2, 0, 0, TestFixture.Vietnam).ToUniversalTime(),
                GiaCoBan = 1_000_000m,
                TrangThai = TrangThaiChuyenBay.DuKien
            },
            new ChuyenBay
            {
                SoHieu = "VN002",
                TuyenBayId = route.Id,
                MayBay = aircraft,
                GioKhoiHanh = new DateTimeOffset(2026, 11, 20, 23, 30, 0, TestFixture.Vietnam).ToUniversalTime(),
                GioHaCanh = new DateTimeOffset(2026, 11, 21, 1, 0, 0, TestFixture.Vietnam).ToUniversalTime(),
                GiaCoBan = 1_100_000m,
                TrangThai = TrangThaiChuyenBay.DuKien
            },
            new ChuyenBay
            {
                SoHieu = "VN003",
                TuyenBayId = route.Id,
                MayBay = aircraft,
                GioKhoiHanh = new DateTimeOffset(2026, 11, 21, 0, 30, 0, TestFixture.Vietnam).ToUniversalTime(),
                GioHaCanh = new DateTimeOffset(2026, 11, 21, 2, 0, 0, TestFixture.Vietnam).ToUniversalTime(),
                GiaCoBan = 1_200_000m,
                TrangThai = TrangThaiChuyenBay.DuKien
            });
        await db.SaveChangesAsync();

        var bus = TestFixture.CreateFlightBus(db);
        var results = await bus.TimChuyenBayAsync(new TimChuyenBayDTO
        {
            SanBayDiId = route.SanBayDiId,
            SanBayDenId = route.SanBayDenId,
            NgayDi = new DateOnly(2026, 11, 20),
            SoHanhKhach = 1
        });

        Assert.Equal(3, results.Count);
        Assert.DoesNotContain(results, item => item.SoHieu == "VN003");
        Assert.All(results, item => Assert.Equal(new DateOnly(2026, 11, 20), DateOnly.FromDateTime(item.GioKhoiHanh.ToOffset(TestFixture.Vietnam).Date)));
    }

    [Fact]
    public async Task TimChuyenBay_LoaiChuyenKhongDuGhe()
    {
        using var db = TestFixture.CreateDb();
        var (_, _, _, route, flight) = TestFixture.Seed(db, seats: 3);
        TestFixture.AddTickets(db, flight, sold: 2, held: 0);

        var bus = TestFixture.CreateFlightBus(db);

        var oneSeat = await bus.TimChuyenBayAsync(new TimChuyenBayDTO
        {
            SanBayDiId = route.SanBayDiId,
            SanBayDenId = route.SanBayDenId,
            NgayDi = new DateOnly(2026, 11, 20),
            SoHanhKhach = 1
        });
        var twoSeats = await bus.TimChuyenBayAsync(new TimChuyenBayDTO
        {
            SanBayDiId = route.SanBayDiId,
            SanBayDenId = route.SanBayDenId,
            NgayDi = new DateOnly(2026, 11, 20),
            SoHanhKhach = 2
        });

        Assert.Single(oneSeat);
        Assert.Empty(twoSeats);
        Assert.Equal(1, oneSeat[0].SoGheTrong);
    }

    [Fact]
    public async Task TimChuyenBay_TuChoiSanBayTrungNhau()
    {
        using var db = TestFixture.CreateDb();
        var (sgn, _, _, _, _) = TestFixture.Seed(db);
        var bus = TestFixture.CreateFlightBus(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => bus.TimChuyenBayAsync(new TimChuyenBayDTO
        {
            SanBayDiId = sgn.Id,
            SanBayDenId = sgn.Id,
            NgayDi = new DateOnly(2026, 11, 20),
            SoHanhKhach = 1
        }));

        Assert.Contains("khác nhau", ex.Message);
    }
}

public class ChuyenBayCommandTests
{
    [Fact]
    public async Task TaoChuyenBay_TuChoiTrungSoHieuCungGio()
    {
        using var db = TestFixture.CreateDb();
        var (_, _, aircraft, route, _) = TestFixture.Seed(db);
        var bus = TestFixture.CreateFlightBus(db);

        var result = await bus.TaoChuyenBayAsync(new TaoChuyenBayDTO
        {
            SoHieu = "vn210",
            TuyenBayId = route.Id,
            MayBayId = aircraft.Id,
            GioKhoiHanh = new DateTimeOffset(2026, 11, 20, 8, 0, 0, TestFixture.Vietnam),
            GioHaCanh = new DateTimeOffset(2026, 11, 20, 10, 0, 0, TestFixture.Vietnam),
            GiaCoBan = 1_800_000m,
            TrangThai = "DuKien"
        });

        Assert.False(result.ThanhCong);
        Assert.Contains("Số hiệu", result.Loi);
    }

    [Fact]
    public async Task TaoChuyenBay_TuChoiTrungLichTauBay()
    {
        using var db = TestFixture.CreateDb();
        var (_, _, aircraft, route, _) = TestFixture.Seed(db);
        var bus = TestFixture.CreateFlightBus(db);

        var result = await bus.TaoChuyenBayAsync(new TaoChuyenBayDTO
        {
            SoHieu = "VN999",
            TuyenBayId = route.Id,
            MayBayId = aircraft.Id,
            GioKhoiHanh = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TestFixture.Vietnam),
            GioHaCanh = new DateTimeOffset(2026, 11, 20, 11, 0, 0, TestFixture.Vietnam),
            GiaCoBan = 1_800_000m,
            TrangThai = "DuKien"
        });

        Assert.False(result.ThanhCong);
        Assert.Contains("Máy bay", result.Loi);
    }

    [Fact]
    public async Task TaoChuyenBay_TuChoiTuyenHoacTauBayTamDung()
    {
        using var db = TestFixture.CreateDb();
        var (_, _, aircraft, route, _) = TestFixture.Seed(db, routeActive: false);
        var bus = TestFixture.CreateFlightBus(db);

        var routeResult = await bus.TaoChuyenBayAsync(new TaoChuyenBayDTO
        {
            SoHieu = "VN998",
            TuyenBayId = route.Id,
            MayBayId = aircraft.Id,
            GioKhoiHanh = new DateTimeOffset(2026, 11, 21, 8, 0, 0, TestFixture.Vietnam),
            GioHaCanh = new DateTimeOffset(2026, 11, 21, 10, 0, 0, TestFixture.Vietnam),
            GiaCoBan = 1_800_000m,
            TrangThai = "DuKien"
        });

        Assert.False(routeResult.ThanhCong);
        Assert.Contains("tạm dừng", routeResult.Loi);

        using var db2 = TestFixture.CreateDb();
        var (_, _, inactiveAircraft, activeRoute, _) = TestFixture.Seed(db2, aircraftActive: false);
        var bus2 = TestFixture.CreateFlightBus(db2);

        var aircraftResult = await bus2.TaoChuyenBayAsync(new TaoChuyenBayDTO
        {
            SoHieu = "VN997",
            TuyenBayId = activeRoute.Id,
            MayBayId = inactiveAircraft.Id,
            GioKhoiHanh = new DateTimeOffset(2026, 11, 21, 8, 0, 0, TestFixture.Vietnam),
            GioHaCanh = new DateTimeOffset(2026, 11, 21, 10, 0, 0, TestFixture.Vietnam),
            GiaCoBan = 1_800_000m,
            TrangThai = "DuKien"
        });

        Assert.False(aircraftResult.ThanhCong);
        Assert.Contains("bảo trì", aircraftResult.Loi);
    }

    [Fact]
    public async Task CapNhatChuyenBay_TuChoiDoiSangKhungGioDaCoChuyen()
    {
        using var db = TestFixture.CreateDb();
        var (_, _, aircraft, route, flight) = TestFixture.Seed(db);

        var other = new ChuyenBay
        {
            SoHieu = "VN211",
            TuyenBayId = route.Id,
            MayBayId = aircraft.Id,
            GioKhoiHanh = new DateTimeOffset(2026, 11, 20, 13, 0, 0, TestFixture.Vietnam).ToUniversalTime(),
            GioHaCanh = new DateTimeOffset(2026, 11, 20, 15, 0, 0, TestFixture.Vietnam).ToUniversalTime(),
            GiaCoBan = 1_600_000m,
            TrangThai = TrangThaiChuyenBay.DuKien
        };
        db.Add(other);
        await db.SaveChangesAsync();

        var bus = TestFixture.CreateFlightBus(db);
        var result = await bus.CapNhatChuyenBayAsync(new CapNhatChuyenBayDTO
        {
            Id = flight.Id,
            SoHieu = "VN210",
            TuyenBayId = route.Id,
            MayBayId = aircraft.Id,
            GioKhoiHanh = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TestFixture.Vietnam),
            GioHaCanh = new DateTimeOffset(2026, 11, 20, 14, 0, 0, TestFixture.Vietnam),
            GiaCoBan = 1_500_000m,
            TrangThai = "DuKien"
        });

        Assert.False(result.ThanhCong);
        Assert.Contains("Máy bay", result.Loi);
    }

    [Fact]
    public async Task DoiGia_CapNhatVaTuChoiGiaAm()
    {
        using var db = TestFixture.CreateDb();
        var (_, _, _, _, flight) = TestFixture.Seed(db);
        var bus = TestFixture.CreateFlightBus(db);

        var invalid = await bus.DoiGiaAsync(flight.Id, -1);
        Assert.False(invalid.ThanhCong);

        var valid = await bus.DoiGiaAsync(flight.Id, 2_100_000m);
        Assert.True(valid.ThanhCong);
        Assert.Equal(2_100_000m, (await bus.LayChuyenBayAsync(flight.Id))!.GiaCoBan);
    }

    [Fact]
    public async Task HuyChuyenBay_ChuyenSangTrangThaiDaHuy()
    {
        using var db = TestFixture.CreateDb();
        var (_, _, _, _, flight) = TestFixture.Seed(db);
        var bus = TestFixture.CreateFlightBus(db);

        var result = await bus.HuyChuyenBayAsync(flight.Id);

        Assert.True(result.ThanhCong);
        Assert.Equal("BiHuy", (await bus.LayChuyenBayAsync(flight.Id))!.TrangThai);
    }

    [Fact]
    public async Task LaySoDoGhe_DanhDauGheDaBanVaDangGiu()
    {
        using var db = TestFixture.CreateDb();
        var (_, _, _, _, flight) = TestFixture.Seed(db, seats: 8);
        TestFixture.AddTickets(db, flight, sold: 1, held: 1);
        var bus = TestFixture.CreateFlightBus(db);

        var seatMap = await bus.LaySoDoGheAsync(flight.Id);

        Assert.Equal(8, seatMap.TongSoGhe);
        Assert.Equal(8, seatMap.Ghes.Count);
        Assert.Equal("DaBan", seatMap.Ghes.Single(seat => seat.SoGhe == "1A").TrangThai);
        Assert.Equal("DangGiu", seatMap.Ghes.Single(seat => seat.SoGhe == "2A").TrangThai);
        Assert.Equal("Trong", seatMap.Ghes.Single(seat => seat.SoGhe == "2B").TrangThai);
    }
}

public class DanhMucBUS_Tests
{
    [Fact]
    public async Task LuuSanBay_TuChoiMaSaiVaMaTrung()
    {
        using var db = TestFixture.CreateDb();
        var (_, _, _, _, _) = TestFixture.Seed(db);
        var bus = TestFixture.CreateCatalogBus(db);

        var invalid = await bus.LuuSanBayAsync(new SanBayDTO
        {
            MaSanBay = "VN",
            TenSanBay = "Sân bay thử",
            ThanhPho = "Hà Nội",
            QuocGia = "Việt Nam",
            DangHoatDong = true
        });
        Assert.False(invalid.ThanhCong);

        var duplicate = await bus.LuuSanBayAsync(new SanBayDTO
        {
            MaSanBay = "SGN",
            TenSanBay = "Tân Sơn Nhất",
            ThanhPho = "TP. Hồ Chí Minh",
            QuocGia = "Việt Nam",
            DangHoatDong = true
        });
        Assert.False(duplicate.ThanhCong);
        Assert.Contains("đã tồn tại", duplicate.Loi);

        var valid = await bus.LuuSanBayAsync(new SanBayDTO
        {
            MaSanBay = "dad",
            TenSanBay = "Đà Nẵng",
            ThanhPho = "Đà Nẵng",
            QuocGia = "Việt Nam",
            DangHoatDong = true
        });
        Assert.True(valid.ThanhCong);
        Assert.Contains(await bus.LaySanBayAsync(), item => item.MaSanBay == "DAD");
    }

    [Fact]
    public async Task LuuTuyenBay_TuChoiTrungSanBayVaTrungCap()
    {
        using var db = TestFixture.CreateDb();
        var (sgn, han, _, route, _) = TestFixture.Seed(db);
        var bus = TestFixture.CreateCatalogBus(db);

        var sameAirport = await bus.LuuTuyenBayAsync(new TuyenBayDTO
        {
            SanBayDiId = sgn.Id,
            SanBayDenId = sgn.Id,
            ThoiLuongDuKienPhut = 60,
            DangKhaiThac = true
        });
        Assert.False(sameAirport.ThanhCong);

        var duplicate = await bus.LuuTuyenBayAsync(new TuyenBayDTO
        {
            SanBayDiId = sgn.Id,
            SanBayDenId = han.Id,
            ThoiLuongDuKienPhut = 120,
            DangKhaiThac = true
        });
        Assert.False(duplicate.ThanhCong);
        Assert.Contains("đã tồn tại", duplicate.Loi);
        Assert.Equal(130, route.ThoiLuongDuKienPhut);
    }

    [Fact]
    public async Task LuuTuyenBay_TuChoiMoTuyenKhiSanBayTamDung()
    {
        using var db = TestFixture.CreateDb();
        var (sgn, han, _, _, _) = TestFixture.Seed(db, airportsActive: false);
        var bus = TestFixture.CreateCatalogBus(db);

        var result = await bus.LuuTuyenBayAsync(new TuyenBayDTO
        {
            SanBayDiId = sgn.Id,
            SanBayDenId = han.Id,
            ThoiLuongDuKienPhut = 120,
            DangKhaiThac = true
        });

        Assert.False(result.ThanhCong);
        Assert.Contains("tạm dừng", result.Loi);
    }

    [Fact]
    public async Task LuuMayBay_TuChoiGiamSucChuaDuoiSoGheDaBan()
    {
        using var db = TestFixture.CreateDb();
        var (_, _, aircraft, _, flight) = TestFixture.Seed(db, seats: 10);
        TestFixture.AddTickets(db, flight, sold: 5, held: 2);
        var bus = TestFixture.CreateCatalogBus(db);

        var tooSmall = await bus.LuuMayBayAsync(new MayBayDTO
        {
            Id = aircraft.Id,
            MaMayBay = aircraft.MaMayBay,
            TenMayBay = aircraft.TenMayBay,
            HangSanXuat = aircraft.HangSanXuat,
            SoGhe = 6,
            DangHoatDong = true
        });
        Assert.False(tooSmall.ThanhCong);
        Assert.Contains("Không giảm sức chứa", tooSmall.Loi);

        var fits = await bus.LuuMayBayAsync(new MayBayDTO
        {
            Id = aircraft.Id,
            MaMayBay = aircraft.MaMayBay,
            TenMayBay = aircraft.TenMayBay,
            HangSanXuat = aircraft.HangSanXuat,
            SoGhe = 7,
            DangHoatDong = true
        });
        Assert.True(fits.ThanhCong);
    }
}
