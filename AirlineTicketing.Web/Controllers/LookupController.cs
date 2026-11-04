using AirlineTicketing.BUS;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public sealed class LookupController(DatVeBUS datVeBUS) : Controller
{
    [HttpGet("/tra-cuu")]
    public IActionResult Index()
    {
        ViewData["CompactNav"] = true;
        return View(new LookupPageViewModel { Form = new LookupFormModel { Pnr = string.Empty, Email = string.Empty } });
    }

    [HttpPost("/tra-cuu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LookupFormModel form, CancellationToken cancellationToken)
    {
        ViewData["CompactNav"] = true;
        if (!ModelState.IsValid)
            return View(new LookupPageViewModel { Form = form, ErrorMessage = "Mã đặt chỗ và email không hợp lệ." });
        try
        {
            var booking = await datVeBUS.TraCuuAsync(form.Pnr, form.Email, cancellationToken);
            return View(new LookupPageViewModel { Form = form, HasResult = true, Result = ToViewModel(booking) });
        }
        catch (NgoaiLeNghiepVu)
        {
            // Không tiết lộ mã đặt chỗ hay email nào là phần sai.
            return View(new LookupPageViewModel
            {
                Form = form,
                ErrorMessage = "Không tìm thấy đặt chỗ khớp đồng thời mã PNR và email liên hệ."
            });
        }
    }

    private static LookupResultViewModel ToViewModel(DatVeDTO booking)
    {
        var ticket = booking.Ves.First();
        return new LookupResultViewModel
        {
            Pnr = booking.MaDatVe,
            BookingStatus = booking.TrangThai,
            TicketStatus = ticket.TrangThai,
            PaymentStatus = booking.ThanhToans.LastOrDefault()?.TrangThai ?? "ChuaThanhToan",
            Flight = new FlightOffer(ticket.Id.ToString(), "AeroViet", ticket.SoHieuChuyenBay, string.Empty,
                ticket.HangVe,
                new AirportInfo(ticket.MaSanBayDi, ticket.TenThanhPhoDi, ticket.TenThanhPhoDi),
                new AirportInfo(ticket.MaSanBayDen, ticket.TenThanhPhoDen, ticket.TenThanhPhoDen),
                ticket.GioKhoiHanh, ticket.GioHaCanh, ticket.GiaVe, "Bay thẳng", string.Empty,
                string.Empty, 0, 0),
            PassengerMasked = MaskWords(ticket.HanhKhach.HoTen),
            IdentityMasked = MaskTail(ticket.HanhKhach.SoHoChieu, 4),
            PhoneMasked = MaskTail(booking.KhachHang.SoDienThoai, 3),
            EmailMasked = MaskEmail(booking.KhachHang.Email),
            SeatCode = ticket.SoGhe,
            BaggageText = "Theo điều kiện hạng vé",
            TicketNumber = ticket.MaVe,
            BookedAtText = booking.NgayDat.ToLocalTime().ToString("dd/MM/yyyy · HH:mm"),
            Total = booking.TongTien
        };
    }

    private static string MaskWords(string value) => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Length < 2 ? "*" : $"{x[0]}{new string('*', x.Length - 1)}"));
    private static string MaskTail(string? value, int visible)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Chưa cung cấp";
        return value.Length <= visible ? new string('*', value.Length) :
            $"{new string('*', value.Length - visible)}{value[^visible..]}";
    }
    private static string MaskEmail(string value)
    {
        var at = value.IndexOf('@');
        return at <= 1 ? $"***{value[at..]}" : $"{value[0]}{new string('*', at - 1)}{value[at..]}";
    }
}
