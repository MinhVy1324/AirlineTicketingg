using AirlineTicketing.Domain;

namespace AirlineTicketing.BUS;

public static class TicketRules
{
    public const int ChangeCutoffHours = 3;

    public static bool CanRequestChangeOrRefund(DatVe booking, DateTimeOffset at) =>
        booking.TrangThai == TrangThaiDatVe.DaXacNhan
        && booking.Ves.Any(x => x.TrangThai == TrangThaiVe.DaPhatHanh)
        && booking.Ves.Where(x => x.TrangThai == TrangThaiVe.DaPhatHanh)
            .All(x => x.ChuyenBay.GioKhoiHanh > at.AddHours(ChangeCutoffHours));
}
