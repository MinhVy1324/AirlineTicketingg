using System.Security.Claims;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Controllers;

public class ThanhToanController(
    IThanhToanBUS thanhToanBUS,
    AppDbContext dbContext,
    ILogger<ThanhToanController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int datVeId, CancellationToken cancellationToken = default)
    {
        var datVe = await dbContext.DatVes
            .Include(d => d.KhachHang)
            .Include(d => d.Ves)
                .ThenInclude(v => v.ChuyenBay)
                    .ThenInclude(cb => cb.TuyenBay)
                        .ThenInclude(tb => tb.SanBayDi)
            .Include(d => d.Ves)
                .ThenInclude(v => v.ChuyenBay)
                    .ThenInclude(cb => cb.TuyenBay)
                        .ThenInclude(tb => tb.SanBayDen)
            .Include(d => d.Ves)
                .ThenInclude(v => v.HanhKhach)
            .FirstOrDefaultAsync(d => d.Id == datVeId, cancellationToken);

        if (datVe == null)
        {
            return NotFound("Không tìm thấy thông tin đặt vé.");
        }

        var existingPayments = await thanhToanBUS.GetByDatVeIdAsync(datVeId, cancellationToken);
        ViewBag.DatVe = datVe;
        ViewBag.ExistingPayments = existingPayments;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KhoiTao(
        int datVeId,
        PhuongThucThanhToan phuongThuc,
        string nhaCungCap,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var thanhToan = await thanhToanBUS.KhoiTaoThanhToanAsync(datVeId, phuongThuc, nhaCungCap, cancellationToken);
            return RedirectToAction(nameof(MoPhongCong), new { id = thanhToan.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Index), new { datVeId });
        }
    }

    [HttpGet]
    public async Task<IActionResult> MoPhongCong(int id, CancellationToken cancellationToken = default)
    {
        var thanhToan = await thanhToanBUS.GetByIdAsync(id, cancellationToken);
        if (thanhToan == null)
        {
            return NotFound("Không tìm thấy giao dịch thanh toán.");
        }

        var datVe = await dbContext.DatVes
            .Include(d => d.KhachHang)
            .Include(d => d.Ves)
                .ThenInclude(v => v.HanhKhach)
            .FirstOrDefaultAsync(d => d.Id == thanhToan.DatVeId, cancellationToken);

        ViewBag.DatVe = datVe;
        return View(thanhToan);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XacNhanGiaLap(
        int id,
        bool thanhCong,
        string? maGiaoDichNgoai = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ketQua = await thanhToanBUS.XuLyThanhToanGiaLapAsync(id, thanhCong, maGiaoDichNgoai, cancellationToken);
            TempData["KetQuaThanhToan"] = ketQua.Message;
            return RedirectToAction(nameof(KetQua), new { id });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi xử lý giao dịch thanh toán #{ThanhToanId}", id);
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(MoPhongCong), new { id });
        }
    }

    [HttpGet]
    public async Task<IActionResult> KetQua(int id, CancellationToken cancellationToken = default)
    {
        var thanhToan = await thanhToanBUS.GetByIdAsync(id, cancellationToken);
        if (thanhToan == null)
        {
            return NotFound();
        }

        var datVe = await dbContext.DatVes
            .Include(d => d.KhachHang)
            .Include(d => d.Ves)
                .ThenInclude(v => v.HanhKhach)
            .Include(d => d.Ves)
                .ThenInclude(v => v.ChuyenBay)
                    .ThenInclude(cb => cb.TuyenBay)
            .FirstOrDefaultAsync(d => d.Id == thanhToan.DatVeId, cancellationToken);

        ViewBag.DatVe = datVe;
        return View(thanhToan);
    }

    [Authorize(Roles = "Admin,NhanVien")]
    [HttpGet]
    public async Task<IActionResult> LichSu(
        string? keyword = null,
        TrangThaiThanhToan? trangThai = null,
        CancellationToken cancellationToken = default)
    {
        var payments = await thanhToanBUS.GetAllGiaoDichAsync(keyword, trangThai, cancellationToken);
        var refunds = await thanhToanBUS.GetAllHoanTienAsync(cancellationToken);

        ViewBag.Refunds = refunds;
        ViewBag.CurrentKeyword = keyword;
        ViewBag.CurrentTrangThai = trangThai;

        return View(payments);
    }

    [Authorize(Roles = "Admin,NhanVien")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HoanTien(TaoHoanTienDTO model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu yêu cầu hoàn tiền không hợp lệ.";
            return RedirectToAction(nameof(LichSu));
        }

        try
        {
            string nguoiThucHien = User.Identity?.Name ?? "Nhân viên";
            await thanhToanBUS.TaoHoanTienAsync(model, nguoiThucHien, cancellationToken);
            TempData["SuccessMessage"] = $"Đã thực hiện hoàn tiền {model.SoTien:N0} VND thành công!";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(LichSu));
    }
}
