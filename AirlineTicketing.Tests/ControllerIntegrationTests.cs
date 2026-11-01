using AirlineTicketing.BUS;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AirlineTicketing.Tests;

public class ControllerIntegrationTests
{
    [Fact]
    public async Task AccountController_Login_ReturnsView_WhenCredentialsInvalid()
    {
        var db = TestDbContextFactory.Create(nameof(AccountController_Login_ReturnsView_WhenCredentialsInvalid));
        var bus = new TaiKhoanBUS(db);
        var controller = new AccountController(bus, NullLogger<AccountController>.Instance);

        var result = await controller.Login(new DangNhapDTO
        {
            TenDangNhap = "nonexistent",
            MatKhau = "wrongpassword"
        });

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task BaoCaoController_Index_PopulatesOverviewAndDailyRevenue()
    {
        var db = TestDbContextFactory.Create(nameof(BaoCaoController_Index_PopulatesOverviewAndDailyRevenue));
        var thongKeBus = new ThongKeBUS(db);
        var thanhToanBus = new ThanhToanBUS(db);
        var controller = new BaoCaoController(thongKeBus, thanhToanBus);

        var result = await controller.Index(preset: "month");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(controller.ViewBag.Overview);
        Assert.NotNull(controller.ViewBag.DailyRevenue);
        Assert.NotNull(controller.ViewBag.Transactions);
    }
}
