using AirlineTicketing.BUS;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.ViewModels;
using AirlineTicketing.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using static AirlineTicketing.Web.Infrastructure.UiLanguage;

namespace AirlineTicketing.Web.Controllers;

[AllowAnonymous]
public sealed class ReservationsController(DatVeBUS datVeBUS, VeBUS veBUS) : Controller
{
    [HttpGet("/dat-ve/tao")]
    public IActionResult Create(int chuyenBayId, string? soGhe) =>
        RedirectToAction("Passengers", "Booking", new { flightId = chuyenBayId });

    [HttpPost("/dat-ve/tao")]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CreateReservationViewModel model)
    {
        var name = (model.HoTenHanhKhach ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        HttpContext.Session.SetString(BookingSession.Passenger, System.Text.Json.JsonSerializer.Serialize(
            new PassengerFormModel
            {
                LastName = name.ElementAtOrDefault(0) ?? "",
                FirstName = name.ElementAtOrDefault(1) ?? "",
                Gender = "Khác", DateOfBirth = model.NgaySinh?.ToString("dd/MM/yyyy") ?? "",
                Nationality = model.QuocTich ?? "", IdentityNumber = model.SoHoChieu ?? "",
                Email = model.Email ?? "", Phone = model.SoDienThoai ?? ""
            }));
        return RedirectToAction("Passengers", "Booking", new { flightId = model.ChuyenBayId });
    }
    [HttpGet("/dat-ve/xac-nhan/{maDatVe}")]
    public async Task<IActionResult> Confirmation(string maDatVe, CancellationToken cancellationToken)
    {
        var email = TempData.Peek("BookingEmail") as string;
        if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(Manage));
        try
        {
            var result = await datVeBUS.TraCuuAsync(maDatVe, email, cancellationToken);
            return View(new ReservationConfirmationViewModel { DatVe = result });
        }
        catch (NgoaiLeNghiepVu exception)
        {
            TempData["BookingError"] = T(exception.Message, "No booking matches the verification details.");
            return RedirectToAction(nameof(Manage));
        }
    }

    [HttpGet("/quan-ly-dat-ve")]
    public IActionResult Manage() => View(new ManageReservationViewModel
    {
        Loi = TempData["BookingError"] as string
    });

    [HttpPost("/quan-ly-dat-ve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Manage(ManageReservationViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            model.KetQua = await datVeBUS.TraCuuAsync(model.MaDatVe, model.Email, cancellationToken);
        }
        catch (NgoaiLeNghiepVu exception)
        {
            model.Loi = T(exception.Message, "No booking matches the verification details.");
        }
        return View(model);
    }

    // Các URL cũ chuyển sang quy trình yêu cầu và phê duyệt, không đổi/hủy trực tiếp.
    [HttpPost("/quan-ly-dat-ve/huy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string maVe, string email, string lyDo, CancellationToken cancellationToken)
    {
        var actorEmail = ResolveActorEmail(email);
        if (actorEmail is null) return RedirectToAction(nameof(Manage));
        try
        {
            var ticket = await veBUS.TraCuuAsync(maVe, actorEmail, cancellationToken);
            return RedirectToAction("RefundRequest", "Tickets", new { pnr = ticket.MaDatVe });
        }
        catch (NgoaiLeNghiepVu)
        {
            TempData["BookingError"] = T("Không tìm thấy vé khớp với thông tin xác minh.", "No ticket matches the verification details.");
            return RedirectToAction(nameof(Manage));
        }
    }

    [HttpPost("/quan-ly-dat-ve/doi")]
    [ValidateAntiForgeryToken]
    public IActionResult Change() => RedirectToAction("Index", "Tickets");
    /// <summary>
    /// Khách đã đăng nhập chỉ thao tác trên đặt chỗ gắn với email của chính tài khoản,
    /// không tin email do form gửi lên. Khách vãng lai vẫn xác minh bằng PNR + email.
    /// </summary>
    private string? ResolveActorEmail(string? submittedEmail)
    {
        var claimEmail = User.FindFirstValue(ClaimTypes.Email)?.Trim();
        if (!string.IsNullOrWhiteSpace(claimEmail))
        {
            return claimEmail.ToLowerInvariant();
        }

        return string.IsNullOrWhiteSpace(submittedEmail)
            ? null
            : submittedEmail.Trim().ToLowerInvariant();
    }
}
