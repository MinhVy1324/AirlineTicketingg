using AirlineTicketing.DAL;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Controllers;

public class LookupController(AppDbContext dbContext) : Controller
{
    [HttpGet("/tra-cuu")]
    public IActionResult Index()
    {
        ViewData["CompactNav"] = true;
        return View(new LookupPageViewModel { Form = new LookupFormModel() });
    }

    [HttpPost("/tra-cuu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LookupFormModel form, CancellationToken cancellationToken)
    {
        ViewData["CompactNav"] = true;

        form.Pnr = (form.Pnr ?? string.Empty).Trim().ToUpperInvariant();
        form.Email = (form.Email ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(form.Pnr) || string.IsNullOrWhiteSpace(form.Email))
        {
            return View(new LookupPageViewModel
            {
                Form = form,
                ErrorMessage = "Vui lòng nhập mã đặt chỗ (PNR) và email liên hệ."
            });
        }

        try
        {
            var emailLower = form.Email.ToLower();
            var booking = await dbContext.DatVes
                .AsNoTracking()
                .Include(x => x.KhachHang)
                .Include(x => x.Ves).ThenInclude(x => x.HanhKhach)
                .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
                .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
                .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.MayBay)
                .FirstOrDefaultAsync(x =>
                    x.MaDatVe.ToUpper() == form.Pnr
                    && ((x.EmailLienHe != null && x.EmailLienHe.ToLower() == emailLower)
                        || x.KhachHang.Email.ToLower() == emailLower),
                    cancellationToken);

            if (booking is null || booking.Ves.Count == 0)
            {
                return View(new LookupPageViewModel
                {
                    Form = form,
                    ErrorMessage = "Không tìm thấy đặt chỗ khớp mã PNR và email liên hệ. Vui lòng kiểm tra lại."
                });
            }

            var ve = booking.Ves.OrderBy(x => x.Id).First();
            var f = ve.ChuyenBay;
            var dep = FlightMapping.ToVietnamTime(f.GioKhoiHanh);
            var arr = FlightMapping.ToVietnamTime(f.GioHaCanh);

            var result = new LookupResultViewModel
            {
                Pnr = booking.MaDatVe,
                Flight = new FlightOffer(
                    Id: f.Id.ToString(),
                    AirlineName: FlightMapping.AirlineNameFor(f.SoHieu),
                    FlightNumber: f.SoHieu,
                    Aircraft: f.MayBay?.TenMayBay ?? f.MayBay?.MaMayBay ?? string.Empty,
                    FareName: string.IsNullOrWhiteSpace(ve.HangVe) ? "Phổ thông" : ve.HangVe,
                    From: new AirportInfo(f.TuyenBay.SanBayDi.MaSanBay, f.TuyenBay.SanBayDi.ThanhPho, f.TuyenBay.SanBayDi.TenSanBay),
                    To: new AirportInfo(f.TuyenBay.SanBayDen.MaSanBay, f.TuyenBay.SanBayDen.ThanhPho, f.TuyenBay.SanBayDen.TenSanBay),
                    Departure: dep,
                    Arrival: arr,
                    PricePerPassenger: ve.GiaVe > 0 ? ve.GiaVe : f.GiaCoBan,
                    Stops: "Bay thẳng",
                    CabinBaggage: FlightMapping.CabinBaggage,
                    CheckedBaggage: FlightMapping.CheckedBaggage,
                    ChangeFee: FlightMapping.ChangeFee,
                    RefundFee: FlightMapping.RefundFee),
                PassengerMasked = MaskName(ve.HanhKhach.HoTen),
                IdentityMasked = MaskId(ve.HanhKhach.SoHoChieu),
                PhoneMasked = MaskPhone(booking.KhachHang.SoDienThoai),
                EmailMasked = MaskEmail(booking.KhachHang.Email),
                SeatCode = string.IsNullOrWhiteSpace(ve.SoGhe) ? "Chưa chọn" : ve.SoGhe,
                BaggageText = $"{FlightMapping.CabinBaggage} + {FlightMapping.CheckedBaggage}",
                TicketNumber = string.IsNullOrWhiteSpace(ve.MaVe) ? $"Chưa phát hành ({booking.MaDatVe})" : ve.MaVe,
                BookedAtText = FlightMapping.ToVietnamTime(booking.NgayDat).ToString("dd/MM/yyyy · HH:mm '(UTC+7)'"),
                Total = booking.TongTien
            };

            return View(new LookupPageViewModel { Form = form, HasResult = true, Result = result });
        }
        catch (Exception)
        {
            return View(new LookupPageViewModel
            {
                Form = form,
                ErrorMessage = "Không thể tra cứu lúc này. Vui lòng kiểm tra kết nối cơ sở dữ liệu và thử lại."
            });
        }
    }

    private static string MaskName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Hành khách";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1) return parts[0][0] + "***";
        return string.Join(' ', parts.Select((p, i) => i == 0 ? p : p.Length <= 1 ? "*" : p[0] + new string('*', Math.Min(3, p.Length - 1))));
    }

    private static string MaskId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "—";
        var keep = Math.Min(4, id.Length);
        return new string('•', id.Length - keep) + id[^keep..];
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "—";
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 4) return phone;
        return phone[..Math.Min(4, phone.Length)] + " ••• ••• " + digits[^3..];
    }

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "—";
        var at = email.IndexOf('@');
        if (at <= 1) return email;
        return email[0] + new string('•', Math.Min(6, at - 1)) + email[at..];
    }
}
