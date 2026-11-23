using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.Infrastructure.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AirlineTicketing.Web.Controllers;

[AllowAnonymous]
public sealed class VnpayController(AppDbContext db, VnpayBookingService gateway, IOptions<VnpayOptions> configuration,
    ILogger<VnpayController> logger) : Controller
{
    [HttpGet("/dat-ve/vnpay/ipn")]
    public async Task<IActionResult> Notify(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var notice = VnpayProtocol.ReadNotice(configuration.Value, Request.Query);
        if (notice is null) return Acknowledge(new("97", "Invalid signature or merchant"));
        try
        {
            return Acknowledge(await gateway.ApplyNoticeAsync(notice, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "VNPAY IPN could not be recorded for reference {Reference}.", notice.Reference);
            return Acknowledge(new("99", "Temporary error"));
        }
    }

    [HttpGet(VnpayProtocol.ReturnPath)]
    public async Task<IActionResult> Return(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var notice = VnpayProtocol.ReadNotice(configuration.Value, Request.Query);
        if (notice is null) return View("Status", "Không xác minh được kết quả trả về. Kiểm tra trạng thái đặt chỗ.");
        var bookingId = HttpContext.Session.GetInt32(BookingSession.BookingId);
        if (bookingId.HasValue && !await db.ThanhToans.AnyAsync(x => x.DatVeId == bookingId
                && x.NhaCungCap == VnpayOptions.Provider && x.MaGiaoDich == notice.Reference, ct))
            return NotFound();
        return await Status(ct);
    }

    [HttpGet("/dat-ve/vnpay/trang-thai")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var bookingId = HttpContext.Session.GetInt32(BookingSession.BookingId);
        if (!bookingId.HasValue) return View("Status", "Mở Tra cứu đặt chỗ bằng PNR và email để kiểm tra kết quả.");
        var payment = await db.ThanhToans.AsNoTracking().Include(x => x.DatVe).Include(x => x.HoanTiens)
            .Where(x => x.DatVeId == bookingId && x.NhaCungCap == VnpayOptions.Provider)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (payment is null) return Redirect("/dat-ve/thanh-toan");
        if (payment.DatVe.TrangThai == TrangThaiDatVe.DaXacNhan)
            return RedirectToAction("Success", "Booking", new { pnr = payment.DatVe.MaDatVe });
        var message = payment.TrangThai switch
        {
            TrangThaiThanhToan.ThanhCong when payment.HoanTiens.Any(x => x.TrangThai == TrangThaiHoanTien.ThanhCong)
                => "Giao dịch đã được hoàn. Đặt chỗ này không phát hành vé.",
            TrangThaiThanhToan.ThanhCong => "VNPAY đã ghi nhận tiền nhưng không thể phát hành vé. Giao dịch đang chờ xử lý hoàn tiền.",
            TrangThaiThanhToan.ThatBai => "VNPAY không xác nhận thanh toán thành công. Bạn có thể thử lại nếu đặt chỗ còn hạn.",
            _ => "Đang chờ máy chủ VNPAY xác nhận. Nếu đã thanh toán, hãy kiểm tra lại trạng thái trước khi tạo đặt chỗ mới."
        };
        return View("Status", message);
    }

    private IActionResult Acknowledge(VnpayResult result) => new JsonResult(
        new Dictionary<string, string> { ["RspCode"] = result.Code, ["Message"] = result.Message });

}
