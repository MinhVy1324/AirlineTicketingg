using AirlineTicketing.BUS;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public sealed class ReservationsController(DatVeBUS datVeBUS, VeBUS veBUS) : Controller
{
    [HttpGet("/dat-ve/tao")]
    public IActionResult Create(int chuyenBayId, string? soGhe) => View(new CreateReservationViewModel
    {
        ChuyenBayId = chuyenBayId,
        SoGhe = soGhe ?? string.Empty
    });

    [HttpPost("/dat-ve/tao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReservationViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            var result = await datVeBUS.TaoDatVeAsync(new TaoDatVeDTO
            {
                NguoiLienHe = new KhachHangDTO
                {
                    HoTen = model.HoTenLienHe, Email = model.Email, SoDienThoai = model.SoDienThoai
                },
                Ves = [new TaoVeDTO
                {
                    ChuyenBayId = model.ChuyenBayId, SoGhe = model.SoGhe, HangVe = model.HangVe,
                    HanhKhach = new HanhKhachDTO
                    {
                        HoTen = model.HoTenHanhKhach, NgaySinh = model.NgaySinh,
                        QuocTich = model.QuocTich, SoHoChieu = model.SoHoChieu
                    }
                }]
            }, User.Identity?.Name, cancellationToken);
            TempData["BookingEmail"] = model.Email.Trim().ToLowerInvariant();
            return RedirectToAction(nameof(Confirmation), new { maDatVe = result.MaDatVe });
        }
        catch (NgoaiLeNghiepVu exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
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
            TempData["BookingError"] = exception.Message;
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
            model.Loi = exception.Message;
        }
        return View(model);
    }

    [HttpPost("/quan-ly-dat-ve/huy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string maVe, string email, string lyDo,
        CancellationToken cancellationToken)
    {
        try
        {
            var ticket = await veBUS.HuyVeAsync(new HuyVeDTO { MaVe = maVe, LyDo = lyDo },
                email, User.Identity?.Name, cancellationToken);
            TempData["BookingEmail"] = email;
            return RedirectToAction(nameof(Confirmation), new { maDatVe = ticket.MaDatVe });
        }
        catch (NgoaiLeNghiepVu exception)
        {
            TempData["BookingError"] = exception.Message;
            return RedirectToAction(nameof(Manage));
        }
    }

    [HttpPost("/quan-ly-dat-ve/doi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(int veId, int chuyenBayMoiId, string soGheMoi,
        string hangVeMoi, string email, string lyDo, CancellationToken cancellationToken)
    {
        try
        {
            var ticket = await veBUS.DoiVeAsync(new DoiVeDTO
            {
                VeId = veId, ChuyenBayMoiId = chuyenBayMoiId, SoGheMoi = soGheMoi,
                HangVeMoi = hangVeMoi, LyDo = lyDo
            }, email, User.Identity?.Name, cancellationToken);
            TempData["BookingEmail"] = email;
            return RedirectToAction(nameof(Confirmation), new { maDatVe = ticket.MaDatVe });
        }
        catch (NgoaiLeNghiepVu exception)
        {
            TempData["BookingError"] = exception.Message;
            return RedirectToAction(nameof(Manage));
        }
    }
}
