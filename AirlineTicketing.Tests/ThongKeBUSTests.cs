using AirlineTicketing.BUS;
using AirlineTicketing.Domain;
using Xunit;

namespace AirlineTicketing.Tests;

public class ThongKeBUSTests
{
    [Fact]
    public async Task GetDoanhThuTongQuanAsync_ShouldCalculatePureNetRevenue()
    {
        var db = TestDbContextFactory.Create(nameof(GetDoanhThuTongQuanAsync_ShouldCalculatePureNetRevenue));
        var bus = new ThongKeBUS(db);

        var now = DateTimeOffset.UtcNow;

        // Payment 1: 3,000,000 VND (Success)
        db.ThanhToans.Add(new ThanhToan
        {
            DatVeId = 1,
            SoTien = 3000000m,
            DonViTienTe = "VND",
            PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
            NhaCungCap = "VNPAY",
            TrangThai = TrangThaiThanhToan.ThanhCong,
            MaGiaoDich = "TX1",
            NgayTao = now,
            NgayHoanTat = now
        });

        // Payment 2: 2,000,000 VND (Success)
        var p2 = new ThanhToan
        {
            DatVeId = 2,
            SoTien = 2000000m,
            DonViTienTe = "VND",
            PhuongThuc = PhuongThucThanhToan.TheNganHang,
            NhaCungCap = "MoMo",
            TrangThai = TrangThaiThanhToan.ThanhCong,
            MaGiaoDich = "TX2",
            NgayTao = now,
            NgayHoanTat = now
        };
        db.ThanhToans.Add(p2);

        // Payment 3: 1,000,000 VND (Failed - should NOT be included in revenue)
        db.ThanhToans.Add(new ThanhToan
        {
            DatVeId = 3,
            SoTien = 1000000m,
            DonViTienTe = "VND",
            PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
            NhaCungCap = "VNPAY",
            TrangThai = TrangThaiThanhToan.ThatBai,
            MaGiaoDich = "TX3",
            NgayTao = now
        });

        await db.SaveChangesAsync();

        // Refund: 500,000 VND on payment 2 (Success)
        db.HoanTiens.Add(new HoanTien
        {
            ThanhToanId = p2.Id,
            SoTien = 500000m,
            DonViTienTe = "VND",
            NhaCungCap = "MoMo",
            MaGiaoDich = "REF1",
            TrangThai = TrangThaiHoanTien.ThanhCong,
            NgayTao = now,
            NgayHoanTat = now
        });
        await db.SaveChangesAsync();

        var stats = await bus.GetDoanhThuTongQuanAsync();

        // Tong Thu: 3,000,000 + 2,000,000 = 5,000,000 VND
        Assert.Equal(5000000m, stats.TongTienDaThu);
        // Tong Hoan: 500,000 VND
        Assert.Equal(500000m, stats.TongTienDaHoan);
        // Doanh Thu Thuan: 5,000,000 - 500,000 = 4,500,000 VND
        Assert.Equal(4500000m, stats.DoanhThuThuan);
        Assert.Equal(2, stats.SoGiaoDichThanhCong);
        Assert.Equal(1, stats.SoGiaoDichHoanTien);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_ShouldAggregateCountsCorrectly()
    {
        var db = TestDbContextFactory.Create(nameof(GetDashboardStatsAsync_ShouldAggregateCountsCorrectly));
        var bus = new ThongKeBUS(db);

        db.KhachHangs.Add(new KhachHang { HoTen = "K1", Email = "k1@test.com", SoDienThoai = "01" });
        db.KhachHangs.Add(new KhachHang { HoTen = "K2", Email = "k2@test.com", SoDienThoai = "02" });

        db.DatVes.Add(new DatVe { MaDatVe = "D1", KhachHangId = 1, TongTien = 1000000m, TrangThai = TrangThaiDatVe.DaXacNhan });
        db.DatVes.Add(new DatVe { MaDatVe = "D2", KhachHangId = 2, TongTien = 1500000m, TrangThai = TrangThaiDatVe.ChoThanhToan });

        db.Ves.Add(new Ve { MaVe = "V1", DatVeId = 1, ChuyenBayId = 1, GiaVe = 1000000m, TrangThai = TrangThaiVe.DaPhatHanh });
        db.Ves.Add(new Ve { MaVe = "V2", DatVeId = 2, ChuyenBayId = 1, GiaVe = 1500000m, TrangThai = TrangThaiVe.TamGiu });

        db.ChuyenBays.Add(new ChuyenBay { SoHieu = "VN-100", TuyenBayId = 1, MayBayId = 1, GiaCoBan = 1000000m, TrangThai = TrangThaiChuyenBay.DuKien });

        await db.SaveChangesAsync();

        var dashboard = await bus.GetDashboardStatsAsync();

        Assert.Equal(2, dashboard.TongKhachHang);
        Assert.Equal(2, dashboard.TongDatVe);
        Assert.Equal(1, dashboard.TongVeDaBan); // Only DaPhatHanh
        Assert.Equal(1, dashboard.TongChuyenBay);
    }
}
