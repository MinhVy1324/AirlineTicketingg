using System.Net;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Tests;

[Collection(PostgresCollection.Name)]
public sealed class TicketFilteringTests
{
    [PostgresFact]
    public async Task Requests_FilterBeforePagination_AndNeverShowAnotherCustomersRequest()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var owned = await db.DatVes.SingleAsync(x => x.MaDatVe == host.Data.PnrA);
        var other = await db.DatVes.SingleAsync(x => x.MaDatVe == host.Data.PnrB);
        var now = DateTimeOffset.UtcNow;
        var open = Enumerable.Range(1, 31).Select(i => Request(owned.Id,
            i % 2 == 0 ? TrangThaiYeuCau.ChoDuyet : TrangThaiYeuCau.DaDuyet, now.AddMinutes(i))).ToArray();
        var completed = Request(owned.Id, TrangThaiYeuCau.HoanTat, now.AddMinutes(40));
        var denied = Request(owned.Id, TrangThaiYeuCau.TuChoi, now.AddMinutes(41));
        var foreign = Request(other.Id, TrangThaiYeuCau.HoanTat, now.AddMinutes(42));
        db.YeuCauDoiHoans.AddRange(open);
        db.YeuCauDoiHoans.AddRange(completed, denied, foreign);
        await db.SaveChangesAsync();

        using var client = host.CreateClient(AppRoles.KhachHang, host.Data.CustomerAEmail, host.Data.CustomerAId);
        const string path = "/ve-cua-toi/yeu-cau";
        var inProgress = WebUtility.HtmlDecode(await client.GetStringAsync(path + "?tab=processing"));
        Assert.Contains("Tất cả (33)", inProgress);
        Assert.Contains("Đang xử lý (31)", inProgress);
        Assert.Contains("Đã hoàn tất (2)", inProgress);
        Assert.Contains("trang=2", inProgress);
        Assert.Contains("Trang 1 / 2", inProgress);
        Assert.Matches(@"href=""[^""]*tab=processing""\s+aria-current=""page""", inProgress);
        Assert.DoesNotContain(open[0].MaYeuCau, inProgress);
        Assert.DoesNotContain(completed.MaYeuCau, inProgress);
        Assert.DoesNotContain(denied.MaYeuCau, inProgress);
        Assert.DoesNotContain(foreign.MaYeuCau, inProgress);

        var next = WebUtility.HtmlDecode(await client.GetStringAsync(path + "?tab=processing&trang=2"));
        Assert.Contains("Trang 2 / 2", next);
        Assert.Contains(open[0].MaYeuCau, next);
        Assert.DoesNotContain(open[30].MaYeuCau, next);
        var direct = WebUtility.HtmlDecode(await client.GetStringAsync(path + "?id=" + open[0].MaYeuCau));
        Assert.Contains("Yêu cầu hoàn · " + open[0].MaYeuCau, direct);

        var done = WebUtility.HtmlDecode(await client.GetStringAsync(path + "?tab=done&id=" + foreign.MaYeuCau));
        Assert.Contains(completed.MaYeuCau, done);
        Assert.Contains(denied.MaYeuCau, done);
        Assert.DoesNotContain(foreign.MaYeuCau, done);
        Assert.DoesNotContain(open[30].MaYeuCau, done);
        Assert.DoesNotContain(other.MaDatVe, done);

        using var otherClient = host.CreateClient(AppRoles.KhachHang, host.Data.CustomerBEmail, host.Data.CustomerBId);
        var empty = WebUtility.HtmlDecode(await otherClient.GetStringAsync(path + "?tab=processing"));
        Assert.Contains("Không có yêu cầu đổi hoặc hoàn vé trong mục này.", empty);
        Assert.DoesNotContain("Chi tiết đặt chỗ", empty);
    }

    [PostgresFact]
    public async Task MyTickets_SearchesOwnedBookingsByPnrOrDestinationAndAppliesDepartureFilter()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var ticket = await db.Ves.Include(v => v.ChuyenBay).ThenInclude(f => f.TuyenBay).ThenInclude(r => r.SanBayDen)
            .SingleAsync(v => v.DatVe.MaDatVe == host.Data.PnrA);
        var future = ticket.ChuyenBay;
        var pastFlight = new ChuyenBay
        {
            SoHieu = $"OLD{Guid.NewGuid():N}"[..11], MayBayId = future.MayBayId, HangHangKhongId = future.HangHangKhongId,
            TuyenBayId = future.TuyenBayId, GioKhoiHanh = DateTimeOffset.UtcNow.AddDays(-3),
            GioHaCanh = DateTimeOffset.UtcNow.AddDays(-3).AddHours(2), GiaCoBan = future.GiaCoBan,
            TrangThai = TrangThaiChuyenBay.HoanTat
        };
        var past = new DatVe
        {
            MaDatVe = $"OLD{Guid.NewGuid():N}"[..11].ToUpperInvariant(), KhachHangId = host.Data.CustomerAId,
            NgayDat = DateTimeOffset.UtcNow.AddDays(-7), HetHanGiuCho = DateTimeOffset.UtcNow.AddDays(-7).AddMinutes(15),
            TongTien = 1_000_000m, TrangThai = TrangThaiDatVe.DaXacNhan,
            Ves = [new Ve { MaVe = Guid.NewGuid().ToString("N")[..20], ChuyenBay = pastFlight, SoGhe = "1B",
                HangVe = "PhoThong", HanhKhachId = ticket.HanhKhachId, TrangThai = TrangThaiVe.DaPhatHanh }]
        };
        db.DatVes.Add(past);
        await db.SaveChangesAsync();

        using var client = host.CreateClient(AppRoles.KhachHang, host.Data.CustomerAEmail, host.Data.CustomerAId);
        var pnr = host.Data.PnrA.ToLowerInvariant();
        var upcoming = WebUtility.HtmlDecode(await client.GetStringAsync($"/ve-cua-toi?q={pnr}&date=upcoming"));
        Assert.Contains(host.Data.PnrA, upcoming);
        Assert.DoesNotContain(host.Data.PnrB, upcoming);
        Assert.DoesNotContain(past.MaDatVe, upcoming);
        Assert.Contains("date=upcoming", upcoming);
        Assert.Matches(@"value=""upcoming""\s+selected", upcoming);

        var destination = future.TuyenBay.SanBayDen.MaSanBay.ToLowerInvariant();
        var history = await client.GetStringAsync($"/ve-cua-toi?tab=done&q={destination}&date=past");
        Assert.Contains(past.MaDatVe, history);
        Assert.DoesNotContain(host.Data.PnrA, history);
        var empty = WebUtility.HtmlDecode(await client.GetStringAsync($"/ve-cua-toi?q={host.Data.PnrB}"));
        Assert.Contains("Không có đặt chỗ phù hợp trong mục này.", empty);
        Assert.DoesNotContain("Chi tiết đặt chỗ", empty);
    }

    private static YeuCauDoiHoan Request(int bookingId, TrangThaiYeuCau status, DateTimeOffset created) => new()
    {
        MaYeuCau = $"RF-{Guid.NewGuid():N}"[..19], DatVeId = bookingId, LoaiYeuCau = LoaiYeuCau.HoanVe,
        LyDo = "Kiểm tra lọc yêu cầu", TrangThai = status, NgayTao = created
    };

    private static AppDbContext Database() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(PostgresTestConnection.Require()).Options);
}
