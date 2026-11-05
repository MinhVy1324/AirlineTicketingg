using AirlineTicketing.BUS;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AirlineTicketing.Tests;

public class TaiKhoanBUSTests
{
    [Fact]
    public async Task DangKyAsync_ShouldCreateAccountAndKhachHang_WhenValid()
    {
        var db = TestDbContextFactory.Create(nameof(DangKyAsync_ShouldCreateAccountAndKhachHang_WhenValid));
        var bus = new TaiKhoanBUS(db);

        var dto = new DangKyDTO
        {
            TenDangNhap = "testuser",
            MatKhau = "Secret@123",
            XacNhanMatKhau = "Secret@123",
            HoTen = "Nguyen Test",
            Email = "test@example.com",
            SoDienThoai = "0911223344"
        };

        var result = await bus.DangKyAsync(dto);

        Assert.NotNull(result);
        Assert.Equal("testuser", result.TenDangNhap);
        Assert.Equal("KhachHang", result.VaiTro);
        Assert.NotNull(result.KhachHangId);

        var userInDb = await db.TaiKhoans.Include(x => x.KhachHang).FirstOrDefaultAsync(x => x.TenDangNhap == "testuser");
        Assert.NotNull(userInDb);
        Assert.NotEqual("Secret@123", userInDb.MatKhauHash); // Password must be hashed!
        Assert.NotNull(userInDb.KhachHang);
        Assert.Equal("Nguyen Test", userInDb.KhachHang.HoTen);
    }

    [Fact]
    public async Task DangKyAsync_ShouldThrow_WhenDuplicateUsername()
    {
        var db = TestDbContextFactory.Create(nameof(DangKyAsync_ShouldThrow_WhenDuplicateUsername));
        var bus = new TaiKhoanBUS(db);

        var dto = new DangKyDTO
        {
            TenDangNhap = "duplicateuser",
            MatKhau = "Secret@123",
            XacNhanMatKhau = "Secret@123",
            HoTen = "Nguyen A",
            Email = "a@example.com",
            SoDienThoai = "0900000001"
        };
        await bus.DangKyAsync(dto);

        var duplicateDto = new DangKyDTO
        {
            TenDangNhap = "duplicateuser",
            MatKhau = "Different@123",
            XacNhanMatKhau = "Different@123",
            HoTen = "Nguyen B",
            Email = "b@example.com",
            SoDienThoai = "0900000002"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.DangKyAsync(duplicateDto));
    }

    [Fact]
    public async Task DangNhapAsync_ShouldAuthenticateSuccessfully_WithCorrectPassword()
    {
        var db = TestDbContextFactory.Create(nameof(DangNhapAsync_ShouldAuthenticateSuccessfully_WithCorrectPassword));
        var bus = new TaiKhoanBUS(db);

        await bus.DangKyAsync(new DangKyDTO
        {
            TenDangNhap = "loginuser",
            MatKhau = "Pass@123456",
            XacNhanMatKhau = "Pass@123456",
            HoTen = "Le User",
            Email = "login@example.com",
            SoDienThoai = "0912345678"
        });

        var user = await bus.DangNhapAsync("loginuser", "Pass@123456");

        Assert.NotNull(user);
        Assert.Equal("loginuser", user.TenDangNhap);

        var failedUser = await bus.DangNhapAsync("loginuser", "WrongPassword");
        Assert.Null(failedUser);
    }

    [Fact]
    public async Task DangNhapAsync_ShouldThrow_WhenAccountIsLocked()
    {
        var db = TestDbContextFactory.Create(nameof(DangNhapAsync_ShouldThrow_WhenAccountIsLocked));
        var bus = new TaiKhoanBUS(db);

        var registered = await bus.DangKyAsync(new DangKyDTO
        {
            TenDangNhap = "lockeduser",
            MatKhau = "Pass@123456",
            XacNhanMatKhau = "Pass@123456",
            HoTen = "Locked Account",
            Email = "locked@example.com",
            SoDienThoai = "0912345679"
        });

        await bus.CapNhatTrangThaiAsync(registered.Id, TrangThaiTaiKhoan.BiKhoa);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => bus.DangNhapAsync("lockeduser", "Pass@123456"));
        Assert.Contains("tạm khóa", ex.Message);
    }

    [Fact]
    public async Task DoiMatKhauAsync_ShouldVerifyOldPasswordAndSetNew()
    {
        var db = TestDbContextFactory.Create(nameof(DoiMatKhauAsync_ShouldVerifyOldPasswordAndSetNew));
        var bus = new TaiKhoanBUS(db);

        var registered = await bus.DangKyAsync(new DangKyDTO
        {
            TenDangNhap = "changeuser",
            MatKhau = "OldPass@123",
            XacNhanMatKhau = "OldPass@123",
            HoTen = "Change Pass",
            Email = "change@example.com",
            SoDienThoai = "0912345680"
        });

        // Fail with wrong old password
        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.DoiMatKhauAsync(registered.Id, new DoiMatKhauDTO
        {
            MatKhauHienTai = "WrongOldPass",
            MatKhauMoi = "NewPass@456",
            XacNhanMatKhauMoi = "NewPass@456"
        }));

        // Succeed with correct old password
        bool success = await bus.DoiMatKhauAsync(registered.Id, new DoiMatKhauDTO
        {
            MatKhauHienTai = "OldPass@123",
            MatKhauMoi = "NewPass@456",
            XacNhanMatKhauMoi = "NewPass@456"
        });
        Assert.True(success);

        // Can login with new password
        var loggedIn = await bus.DangNhapAsync("changeuser", "NewPass@456");
        Assert.NotNull(loggedIn);
    }
}
