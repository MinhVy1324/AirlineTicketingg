using AirlineTicketing.BUS;
using AirlineTicketing.Domain;

namespace AirlineTicketing.Tests;

public sealed class TicketRulesTests
{
    [Theory]
    [InlineData(179, false)]
    [InlineData(180, false)]
    [InlineData(181, true)]
    public void YeuCauDoiHoan_ChiNhanTruocHanBaTieng(int minutes, bool expected)
    {
        var now = new DateTimeOffset(2026, 11, 20, 1, 0, 0, TimeSpan.Zero);
        var booking = new DatVe
        {
            TrangThai = TrangThaiDatVe.DaXacNhan,
            Ves = [new Ve { TrangThai = TrangThaiVe.DaPhatHanh, ChuyenBay = new ChuyenBay { GioKhoiHanh = now.AddMinutes(minutes) } }]
        };
        Assert.Equal(expected, TicketRules.CanRequestChangeOrRefund(booking, now));
        booking.TrangThai = TrangThaiDatVe.ChoThanhToan;
        Assert.False(TicketRules.CanRequestChangeOrRefund(booking, now));
    }
}
