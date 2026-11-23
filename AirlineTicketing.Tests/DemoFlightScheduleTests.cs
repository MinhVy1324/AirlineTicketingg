using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Tests;

[Collection(PostgresCollection.Name)]
public sealed class DemoFlightScheduleTests
{
    [PostgresFact]
    public async Task Refresh_CrossesNewYear_AndPreservesCancelledFlightsAndAircraftAvailability()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var original = await db.ChuyenBays.SingleAsync(f => f.Ves.Any(v => v.DatVe.MaDatVe == host.Data.PnrA));
        var now = new DateTimeOffset(2027, 12, 31, 5, 0, 0, TimeSpan.Zero);
        var start = new DateTimeOffset(2027, 12, 31, 0, 0, 0, TimeSpan.FromHours(7)).ToUniversalTime();
        var cancelledDay = start.AddDays(2);
        var cancelled = new ChuyenBay
        {
            SoHieu = $"CX{Guid.NewGuid():N}"[..10], TuyenBayId = original.TuyenBayId, MayBayId = original.MayBayId,
            HangHangKhongId = original.HangHangKhongId, GioKhoiHanh = cancelledDay.AddHours(8),
            GioHaCanh = cancelledDay.AddHours(10), GiaCoBan = original.GiaCoBan, TrangThai = TrangThaiChuyenBay.BiHuy
        };
        db.ChuyenBays.Add(cancelled);
        await db.SaveChangesAsync();

        await using var worker1 = Database();
        await using var worker2 = Database();
        var counts = await Task.WhenAll(DbSeeder.RefreshFlightScheduleAsync(worker1, now),
            DbSeeder.RefreshFlightScheduleAsync(worker2, now));
        Assert.Single(counts, count => count > 0);
        Assert.Single(counts, count => count == 0);

        var flights = await db.ChuyenBays.AsNoTracking().Include(f => f.HangVes).Include(f => f.MayBay)
            .Where(f => f.TuyenBayId == original.TuyenBayId && f.GioKhoiHanh >= start && f.GioKhoiHanh < start.AddDays(45))
            .ToListAsync();
        var maintained = flights.Where(f => f.Id != cancelled.Id).ToArray();
        Assert.NotEmpty(maintained);
        Assert.All(maintained, f =>
        {
            Assert.True(f.GioKhoiHanh > now);
            Assert.Equal(TrangThaiChuyenBay.DuKien, f.TrangThai);
            Assert.Equal(f.MayBay.SoGhe, f.HangVes.Sum(fare => fare.SoGhe));
            Assert.All(f.HangVes, fare => Assert.Equal(0, fare.SoGheDaBan));
        });
        Assert.Contains(maintained, f => FlightMapping.ToVietnamTime(f.GioKhoiHanh).Year == 2028);
        Assert.Equal(cancelled.Id, Assert.Single(flights, f =>
            f.GioKhoiHanh >= cancelledDay && f.GioKhoiHanh < cancelledDay.AddDays(1)).Id);
        Assert.Equal(TrangThaiChuyenBay.BiHuy, flights.Single(f => f.Id == cancelled.Id).TrangThai);

        var occupied = await db.ChuyenBays.AsNoTracking()
            .Where(f => f.MayBayId == original.MayBayId && f.TrangThai != TrangThaiChuyenBay.BiHuy
                && f.GioKhoiHanh >= start && f.GioKhoiHanh < start.AddDays(45))
            .OrderBy(f => f.GioKhoiHanh).ToListAsync();
        Assert.All(occupied.Zip(occupied.Skip(1)), pair => Assert.True(pair.First.GioHaCanh <= pair.Second.GioKhoiHanh));
        Assert.Equal(0, await DbSeeder.RefreshFlightScheduleAsync(db, now));
    }

    private static AppDbContext Database() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(PostgresTestConnection.Require()).Options);
}
