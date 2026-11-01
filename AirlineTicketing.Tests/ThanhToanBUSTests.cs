using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AirlineTicketing.Tests;

public class ThanhToanBUSTests
{
    private static async Task<(AppDbContext db, DatVe booking)> SetupBookingAsync(string dbName)
    {
        var db = TestDbContextFactory.Create(dbName);

        var khach = new KhachHang { HoTen = "Test Customer", Email = "c@test.com", SoDienThoai = "0987654321" };
        await db.KhachHangs.AddAsync(khach);
        await db.SaveChangesAsync();

        var booking = new DatVe
        {
            MaDatVe = "DATVE-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            KhachHangId = khach.Id,
            NgayDat = DateTimeOffset.UtcNow,
            TrangThai = TrangThaiDatVe.ChoThanhToan,
            TongTien = 2000000m,
            DonViTienTe = "VND"
        };
        await db.DatVes.AddAsync(booking);
        await db.SaveChangesAsync();

        var ticket = new Ve
        {
            DatVeId = booking.Id,
            ChuyenBayId = 1,
            SoGhe = "10A",
            HangVe = "PhoThong",
            GiaVe = 2000000m,
            TrangThai = TrangThaiVe.TamGiu
        };
        await db.Ves.AddAsync(ticket);
        await db.SaveChangesAsync();

        return (db, booking);
    }

    [Fact]
    public async Task KhoiTaoThanhToanAsync_ShouldCreatePaymentRecord_WithStatusChoXuLy()
    {
        var (db, booking) = await SetupBookingAsync(nameof(KhoiTaoThanhToanAsync_ShouldCreatePaymentRecord_WithStatusChoXuLy));
        var bus = new ThanhToanBUS(db);

        var thanhToan = await bus.KhoiTaoThanhToanAsync(booking.Id, PhuongThucThanhToan.ChuyenKhoan, "VNPAY");

        Assert.NotNull(thanhToan);
        Assert.Equal(2000000m, thanhToan.SoTien);
        Assert.Equal("ChoXuLy", thanhToan.TrangThai);
        Assert.Equal("VNPAY", thanhToan.NhaCungCap);
        Assert.False(string.IsNullOrWhiteSpace(thanhToan.MaGiaoDich));
    }

    [Fact]
    public async Task XuLyThanhToanGiaLapAsync_ShouldConfirmBookingAndIssueTickets_WhenSuccess()
    {
        var (db, booking) = await SetupBookingAsync(nameof(XuLyThanhToanGiaLapAsync_ShouldConfirmBookingAndIssueTickets_WhenSuccess));
        var bus = new ThanhToanBUS(db);

        var payment = await bus.KhoiTaoThanhToanAsync(booking.Id, PhuongThucThanhToan.ViDienTu, "MoMo");
        var result = await bus.XuLyThanhToanGiaLapAsync(payment.Id, thanhCong: true);

        Assert.True(result.IsSuccess);
        Assert.Equal("ThanhCong", result.TrangThai);

        var updatedBooking = await db.DatVes.Include(b => b.Ves).FirstAsync(b => b.Id == booking.Id);
        Assert.Equal(TrangThaiDatVe.DaXacNhan, updatedBooking.TrangThai);
        Assert.All(updatedBooking.Ves, v =>
        {
            Assert.Equal(TrangThaiVe.DaPhatHanh, v.TrangThai);
            Assert.False(string.IsNullOrWhiteSpace(v.MaVe));
        });
    }

    [Fact]
    public async Task XuLyThanhToanGiaLapAsync_ShouldBeIdempotent_WhenCalledMultipleTimes()
    {
        var (db, booking) = await SetupBookingAsync(nameof(XuLyThanhToanGiaLapAsync_ShouldBeIdempotent_WhenCalledMultipleTimes));
        var bus = new ThanhToanBUS(db);

        var payment = await bus.KhoiTaoThanhToanAsync(booking.Id, PhuongThucThanhToan.TheNganHang, "Visa");

        var firstResult = await bus.XuLyThanhToanGiaLapAsync(payment.Id, thanhCong: true);
        Assert.True(firstResult.IsSuccess);

        // Call again - Idempotency should safely return without exception
        var secondResult = await bus.XuLyThanhToanGiaLapAsync(payment.Id, thanhCong: true);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal("ThanhCong", secondResult.TrangThai);
    }

    [Fact]
    public async Task TaoHoanTienAsync_ShouldProcessRefundAndDeductSafely()
    {
        var (db, booking) = await SetupBookingAsync(nameof(TaoHoanTienAsync_ShouldProcessRefundAndDeductSafely));
        var bus = new ThanhToanBUS(db);

        var payment = await bus.KhoiTaoThanhToanAsync(booking.Id, PhuongThucThanhToan.ChuyenKhoan, "VNPAY");
        await bus.XuLyThanhToanGiaLapAsync(payment.Id, thanhCong: true);

        // Process partial refund: 500,000 VND
        var refund = await bus.TaoHoanTienAsync(new TaoHoanTienDTO
        {
            ThanhToanId = payment.Id,
            SoTien = 500000m,
            LyDo = "Khách hàng đổi ý giảm bớt hành lý/chỗ ngồi"
        }, "Staff01");

        Assert.NotNull(refund);
        Assert.Equal(500000m, refund.SoTien);
        Assert.Equal("ThanhCong", refund.TrangThai);

        // Cannot refund more than remaining: remaining is 1,500,000 VND, requesting 1,600,000 should fail
        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.TaoHoanTienAsync(new TaoHoanTienDTO
        {
            ThanhToanId = payment.Id,
            SoTien = 1600000m,
            LyDo = "Yêu cầu quá số tiền"
        }));

        // Full refund of remaining 1,500,000 VND
        var fullRefund = await bus.TaoHoanTienAsync(new TaoHoanTienDTO
        {
            ThanhToanId = payment.Id,
            SoTien = 1500000m,
            LyDo = "Hoàn hết số tiền còn lại"
        });
        Assert.NotNull(fullRefund);

        var reloadedBooking = await db.DatVes.Include(b => b.Ves).FirstAsync(b => b.Id == booking.Id);
        Assert.Equal(TrangThaiDatVe.HoanTien, reloadedBooking.TrangThai);
        Assert.All(reloadedBooking.Ves, v => Assert.Equal(TrangThaiVe.DaHoanTien, v.TrangThai));
    }
}
