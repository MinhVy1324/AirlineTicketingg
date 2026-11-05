using System.Security.Claims;
using AirlineTicketing.BUS;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

[Authorize(Roles = "Admin")]
public class QuanLyTaiKhoanController(ITaiKhoanBUS taiKhoanBUS) : Controller
{
    public async Task<IActionResult> Index(
        string? vaiTro = null,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var users = await taiKhoanBUS.GetAllAsync(vaiTro, keyword, cancellationToken);
        ViewBag.CurrentVaiTro = vaiTro;
        ViewBag.CurrentKeyword = keyword;

        return View(users);
    }

    [HttpGet]
    public IActionResult Create() => View(new TaoNhanVienDTO());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaoNhanVienDTO model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await taiKhoanBUS.TaoTaiKhoanNoiBoAsync(model, cancellationToken);
            TempData["SuccessMessage"] = $"Tạo tài khoản nội bộ '{model.TenDangNhap}' thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id, CancellationToken cancellationToken = default)
    {
        int currentUserId = GetCurrentUserId();
        if (id == currentUserId)
        {
            TempData["ErrorMessage"] = "Bạn không thể tự khóa tài khoản của chính mình.";
            return RedirectToAction(nameof(Index));
        }

        var user = await taiKhoanBUS.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        var newStatus = user.TrangThai == TrangThaiTaiKhoan.HoatDong.ToString()
            ? TrangThaiTaiKhoan.BiKhoa
            : TrangThaiTaiKhoan.HoatDong;

        await taiKhoanBUS.CapNhatTrangThaiAsync(id, newStatus, cancellationToken);
        TempData["SuccessMessage"] = $"Đã cập nhật trạng thái tài khoản '{user.TenDangNhap}' thành: {newStatus}.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int id, string vaiTro, CancellationToken cancellationToken = default)
    {
        int currentUserId = GetCurrentUserId();
        if (id == currentUserId)
        {
            TempData["ErrorMessage"] = "Bạn không thể tự đổi vai trò của chính mình.";
            return RedirectToAction(nameof(Index));
        }

<<<<<<< Updated upstream
        if (Enum.TryParse<VaiTroTaiKhoan>(vaiTro, true, out var roleEnum))
=======
        if (Enum.TryParse<VaiTroTaiKhoan>(vaiTro, true, out var roleEnum) && Enum.IsDefined(roleEnum))
>>>>>>> Stashed changes
        {
            await taiKhoanBUS.CapNhatVaiTroAsync(id, roleEnum, cancellationToken);
            TempData["SuccessMessage"] = $"Đã cập nhật vai trò người dùng thành: {vaiTro}.";
        }
        else
        {
            TempData["ErrorMessage"] = "Vai trò không hợp lệ.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id, string matKhauMoi, CancellationToken cancellationToken = default)
    {
<<<<<<< Updated upstream
        if (string.IsNullOrWhiteSpace(matKhauMoi) || matKhauMoi.Length < 6)
        {
            TempData["ErrorMessage"] = "Mật khẩu mới phải có tối thiểu 6 ký tự.";
            return RedirectToAction(nameof(Index));
        }

        await taiKhoanBUS.DatLaiMatKhauAsync(id, matKhauMoi, cancellationToken);
=======
        if (string.IsNullOrWhiteSpace(matKhauMoi) || matKhauMoi.Length is < 12 or > 100)
        {
            TempData["ErrorMessage"] = "Mật khẩu mới phải có tối thiểu 12 ký tự.";
            return RedirectToAction(nameof(Index));
        }

        var updated = await taiKhoanBUS.DatLaiMatKhauAsync(id, matKhauMoi, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }
>>>>>>> Stashed changes
        TempData["SuccessMessage"] = "Đã đặt lại mật khẩu cho tài khoản thành công!";

        return RedirectToAction(nameof(Index));
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
    }
}
