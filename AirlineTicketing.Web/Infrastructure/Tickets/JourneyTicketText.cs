using AirlineTicketing.BUS;
using AirlineTicketing.Domain;

namespace AirlineTicketing.Web.Infrastructure.Tickets;

public static class JourneyTicketText
{
    public static string Flight(Ve ticket) => $"{ticket.ChuyenBay.SoHieu} · {ticket.ChuyenBay.TuyenBay.SanBayDi.MaSanBay} → {ticket.ChuyenBay.TuyenBay.SanBayDen.MaSanBay} · {FlightMapping.ToVietnamTime(ticket.ChuyenBay.GioKhoiHanh):dd/MM/yyyy HH:mm} (UTC+7)";
    public static string Seat(DatVe booking, Ve ticket) => ticket.LoaiHanhKhach == LoaiHanhKhach.EmBe
        ? "Đi cùng " + (booking.Ves.FirstOrDefault(x => x.HanhKhachId == ticket.NguoiDiKemId)?.HanhKhach.HoTen ?? "người lớn")
        : ticket.SoGhe;
    public static string Person(Ve ticket) => $"{ticket.HanhKhach.HoTen} · {JourneyRules.TypeName(ticket.LoaiHanhKhach)}";
}
