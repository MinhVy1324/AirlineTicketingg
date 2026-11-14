using System.Net;
using AirlineTicketing.Web.Infrastructure;

namespace AirlineTicketing.Tests;

/// <summary>
/// Kiểm thử tích hợp các quy tắc phân quyền và quyền sở hữu vé ở tầng HTTP:
/// middleware xác thực, policy StaffOnly, trang "Vé của tôi" và tải vé PDF.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ControllerAuthorizationTests
{
    [PostgresFact]
    public async Task KhachChuaDangNhap_VaoQuanLy_ChuyenVeTrangDangNhap()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/quan-ly");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dang-nhap?ReturnUrl=%2Fquan-ly", response.Headers.Location?.OriginalString);
    }

    [PostgresFact]
    public async Task KhachHang_VaoQuanLy_BiTuChoi()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        using var client = host.CreateClient(
            AppRoles.KhachHang, host.Data.CustomerAEmail, host.Data.CustomerAId);

        using var response = await client.GetAsync("/quan-ly");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dang-nhap?tu-choi=1", response.Headers.Location?.OriginalString);
    }

    [PostgresFact]
    public async Task QuanLy_VaoTrangQuanLy_ThanhCong()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        using var client = host.CreateClient(AppRoles.Manager, "quan-ly@aeroviet.test");

        using var response = await client.GetAsync("/quan-ly");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [PostgresFact]
    public async Task Pdf_ChuVeTaiDuoc_KhachKhacVaKhachAnDanhNhan404()
    {
        await using var host = await PostgresTestHost.CreateAsync();

        using var owner = host.CreateClient(
            AppRoles.KhachHang, host.Data.CustomerAEmail, host.Data.CustomerAId);
        using var ownerResponse = await owner.GetAsync($"/ve/{host.Data.PnrA}/pdf");

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Equal("application/pdf", ownerResponse.Content.Headers.ContentType?.MediaType);
        var bytes = await ownerResponse.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 4);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);

        using var otherCustomer = host.CreateClient(
            AppRoles.KhachHang, host.Data.CustomerBEmail, host.Data.CustomerBId);
        using var otherResponse = await otherCustomer.GetAsync($"/ve/{host.Data.PnrA}/pdf");

        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);

        using var anonymous = host.CreateClient();
        using var anonymousResponse = await anonymous.GetAsync($"/ve/{host.Data.PnrA}/pdf");

        Assert.Equal(HttpStatusCode.NotFound, anonymousResponse.StatusCode);
    }

    [PostgresFact]
    public async Task Admin_ChiTaiKhoanAdminVaoDuoc()
    {
        await using var host = await PostgresTestHost.CreateAsync();

        using var admin = host.CreateClient(AppRoles.Admin, "admin@aeroviet.test");
        using var adminResponse = await admin.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

        using var manager = host.CreateClient(AppRoles.Manager, "quan-ly@aeroviet.test");
        using var managerResponse = await manager.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Redirect, managerResponse.StatusCode);
        Assert.Equal("/dang-nhap?tu-choi=1", managerResponse.Headers.Location?.OriginalString);
    }

    [PostgresFact]
    public async Task Pdf_NhanVienQuanLyTaiDuocVeCuaKhach()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        using var manager = host.CreateClient(AppRoles.Manager, "quan-ly@aeroviet.test");

        using var response = await manager.GetAsync($"/ve/{host.Data.PnrA}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }

    [PostgresFact]
    public async Task VeCuaToi_ChiHienThiVeCuaKhachDangDangNhap()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        using var client = host.CreateClient(
            AppRoles.KhachHang, host.Data.CustomerAEmail, host.Data.CustomerAId);

        using var response = await client.GetAsync("/ve-cua-toi");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(host.Data.PnrA, html);
        Assert.DoesNotContain(host.Data.PnrB, html);
    }
}
