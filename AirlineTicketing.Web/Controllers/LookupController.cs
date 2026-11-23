<<<<<<< Updated upstream
using AirlineTicketing.Web.Demo;
=======
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.DTO;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
>>>>>>> Stashed changes
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static AirlineTicketing.Web.Infrastructure.UiLanguage;

namespace AirlineTicketing.Web.Controllers;

<<<<<<< Updated upstream
public class LookupController : Controller
=======
[AllowAnonymous]
public sealed class LookupController(DatVeBUS datVeBUS, AppDbContext db) : Controller
>>>>>>> Stashed changes
{
    [HttpGet("/tra-cuu")]
    public IActionResult Index()
    {
        ViewData["CompactNav"] = true;
        return View(DemoCatalog.LookupFound());
    }

    [HttpPost("/tra-cuu")]
    [ValidateAntiForgeryToken]
    public IActionResult Index(LookupFormModel form)
    {
        ViewData["CompactNav"] = true;
<<<<<<< Updated upstream
        return View(DemoCatalog.Lookup(form));
=======
        if (!ModelState.IsValid)
            return View(new LookupPageViewModel { Form = form, ErrorMessage = T("Mã đặt chỗ và email không hợp lệ.", "The booking reference and email are invalid.") });
        try
        {
            var booking = await datVeBUS.TraCuuAsync(form.Pnr, form.Email, cancellationToken);
            var disrupted = await db.DatVes.AnyAsync(b => b.MaDatVe == booking.MaDatVe
                && b.TrangThai == TrangThaiDatVe.DaXacNhan
                && (b.BienDongs.Any(x => x.NgayXuLy == null)
                    || b.Ves.Any(x => x.TrangThai == TrangThaiVe.DaPhatHanh && x.ChuyenBay.TrangThai == TrangThaiChuyenBay.BiHuy)),
                cancellationToken);
            return View(new LookupPageViewModel { Form = form, HasResult = true, Result = ToViewModel(booking, disrupted) });
        }
        catch (NgoaiLeNghiepVu)
        {
            // Không tiết lộ mã đặt chỗ hay email nào là phần sai.
            return View(new LookupPageViewModel
            {
                Form = form,
                ErrorMessage = T("Không tìm thấy đặt chỗ khớp đồng thời mã PNR và email liên hệ.", "No booking matches both the booking reference and contact email.")
            });
        }
    }

    private static LookupResultViewModel ToViewModel(DatVeDTO booking, bool disrupted)
    {
        var ticket = booking.Ves.First();
        return new LookupResultViewModel
        {
            JourneyRows = booking.Ves.OrderBy(x => x.GioKhoiHanh).ThenBy(x => x.Id).Select(x =>
                $"{x.SoHieuChuyenBay} · {x.MaSanBayDi} → {x.MaSanBayDen} · {FlightMapping.ToVietnamTime(x.GioKhoiHanh):dd/MM/yyyy HH:mm} (UTC+7) · " +
                $"{MaskWords(x.HanhKhach.HoTen)} · {Label(JourneyRules.TypeName(Enum.Parse<LoaiHanhKhach>(x.LoaiHanhKhach)))} · {Label(JourneyBookingBuilder.CabinOf(x.HangVe).Name)} · " +
                (x.LoaiHanhKhach == "EmBe"
                    ? T("Đi cùng người lớn", "Travels with an adult")
                    : T($"Ghế {x.SoGhe} · {x.HanhLyXachTayKg} kg xách tay + {x.HanhLyKyGuiKg} kg ký gửi",
                        $"Seat {x.SoGhe} · {x.HanhLyXachTayKg} kg carry-on + {x.HanhLyKyGuiKg} kg checked")) +
                T($" · Vé {x.MaVe}", $" · Ticket {x.MaVe}")).ToArray(),
            Pnr = booking.MaDatVe,
            BookingStatus = disrupted ? T("Cần xử lý lịch bay", "Schedule support required") : booking.TrangThai,
            TicketStatus = disrupted ? T("Chờ cập nhật hành trình", "Awaiting itinerary update") : ticket.TrangThai,
            NeedsScheduleSupport = disrupted,
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
            BaggageText = T("Theo điều kiện hạng vé", "Subject to fare conditions"),
            TicketNumber = ticket.MaVe,
            BookedAtText = FlightMapping.ToVietnamTime(booking.NgayDat).ToString("dd/MM/yyyy · HH:mm '(UTC+7)'"),
            Total = booking.TongTien
        };
    }

    private static string MaskWords(string value) => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Length < 2 ? "*" : $"{x[0]}{new string('*', x.Length - 1)}"));
    private static string MaskTail(string? value, int visible)
    {
        if (string.IsNullOrWhiteSpace(value)) return T("Chưa cung cấp", "Not provided");
        return value.Length <= visible ? new string('*', value.Length) :
            $"{new string('*', value.Length - visible)}{value[^visible..]}";
    }
    private static string MaskEmail(string value)
    {
        var at = value.IndexOf('@');
        return at <= 1 ? $"***{value[at..]}" : $"{value[0]}{new string('*', at - 1)}{value[at..]}";
>>>>>>> Stashed changes
    }
}
