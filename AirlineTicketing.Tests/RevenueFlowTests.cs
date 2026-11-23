using System.Globalization;
using System.Net;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Tests;

[Collection(PostgresCollection.Name)]
public sealed class RevenueFlowTests
{
    [PostgresFact]
    public async Task PeriodAndFlightTotals_UseSettlementDateSuccessfulTransactionsAndOneCurrency()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(PostgresTestConnection.Require()).Options);
        var booking = await db.DatVes.Include(b => b.Ves).ThenInclude(v => v.ChuyenBay)
            .SingleAsync(b => b.MaDatVe == host.Data.PnrA);
        var flight = booking.Ves.Single().ChuyenBay;
        var from = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.FromHours(7));
        var settled = from.ToUniversalTime().AddMinutes(5);
        var payment = Charge(1_234_000, "VND", TrangThaiThanhToan.ThanhCong, settled.AddDays(-1), settled);
        var future = Charge(9_000_000, "VND", TrangThaiThanhToan.ThanhCong, settled, settled.AddDays(1));
        var foreign = Charge(75, "USD", TrangThaiThanhToan.ThanhCong, settled, settled);
        var pending = Charge(8_000_000, "VND", TrangThaiThanhToan.ChoXuLy, settled, null);
        db.ThanhToans.AddRange(payment, future, foreign, pending);
        db.HoanTiens.AddRange(Return(payment, 234_000, "VND", TrangThaiHoanTien.ThanhCong),
            Return(payment, 9, "USD", TrangThaiHoanTien.ThanhCong),
            Return(payment, 60_000, "VND", TrangThaiHoanTien.ChoXuLy));
        await db.SaveChangesAsync();

        var reports = new ThongKeBUS(db);
        var result = await reports.LayBaoCaoAsync(from, from.AddDays(1), from.AddDays(-1),
            from.AddMonths(-1), from.AddMonths(1), null, null, flight.SoHieu, default);
        var row = Assert.Single(result.TheoChuyen);
        Assert.Equal(flight.Id, row.Chuyen.Id);
        Assert.Equal(1_234_000, row.DaThu);
        Assert.Equal(234_000, row.DaHoan);
        Assert.Equal(1_000_000, row.ThuRong);
        Assert.Equal(0, result.DaThuKyTruoc);
        Assert.Equal(1, result.SoGiaoDichHoan);

        using var manager = host.CreateClient(AppRoles.Manager);
        var day = from.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var route = $"/quan-ly/bao-cao?ky={Uri.EscapeDataString(day)}&chuyen={flight.SoHieu.ToLowerInvariant()}";
        var html = WebUtility.HtmlDecode(await manager.GetStringAsync(route));
        Assert.Contains("Doanh thu theo chuyến bay", html);
        Assert.Contains(flight.SoHieu, html);
        Assert.Contains("1.000.000", html);
        Assert.DoesNotContain("9.000.000", html);
        var invalid = await manager.GetStringAsync("/quan-ly/bao-cao?ky=01%2F01%2F0001");
        Assert.Contains("Kỳ báo cáo chưa hợp lệ", WebUtility.HtmlDecode(invalid));

        ThanhToan Charge(decimal amount, string currency, TrangThaiThanhToan status, DateTimeOffset created, DateTimeOffset? done) =>
            new()
            {
                DatVeId = booking.Id, NhaCungCap = "Report demo", SoTien = amount, DonViTienTe = currency,
                PhuongThuc = PhuongThucThanhToan.TheNganHang, TrangThai = status, NgayTao = created, NgayHoanTat = done
            };

        HoanTien Return(ThanhToan original, decimal amount, string currency, TrangThaiHoanTien status) => new()
        {
            ThanhToan = original, NhaCungCap = original.NhaCungCap, SoTien = amount, DonViTienTe = currency,
            TrangThai = status, NgayTao = settled, NgayHoanTat = status == TrangThaiHoanTien.ThanhCong ? settled : null
        };
    }
}
