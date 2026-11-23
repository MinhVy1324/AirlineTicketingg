using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AirlineTicketing.Web.Infrastructure.Payments;

public sealed class VnpayOperations(AppDbContext db, VnpayClient client, VnpayBookingService bookings,
    PaymentReconciliationService reconciliation, IOptions<VnpayOptions> configuration, TimeProvider clock)
{
    public async Task<string> ReconcileAsync(int id, CancellationToken ct)
    {
        var payment = await db.ThanhToans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
            && x.NhaCungCap == VnpayOptions.Provider, ct)
            ?? throw new VnpayApiException("Không tìm thấy giao dịch VNPAY.");
        var notice = await client.QueryAsync(payment, ct);
        var result = await bookings.ApplyNoticeAsync(notice, ct);
        if (result.Code == "02") return "Khoản thu này đã được ghi nhận.";
        if (result.Code != "00") return $"Kết quả VNPAY không khớp giao dịch (mã {result.Code}).";
        if (notice.ResponseCode != "00" || notice.Status != "00")
            return $"Đã ghi nhận trạng thái thu từ VNPAY ({notice.ResponseCode}/{notice.Status}).";

        var needsRefund = await db.HoanTiens.AnyAsync(r => r.ThanhToanId == id && !r.YeuCauDoiHoanId.HasValue, ct);
        return needsRefund ? "Đã ghi nhận khoản thu. Xem lệnh hoàn toàn bộ trong mục Hoàn tiền."
            : "Đã ghi nhận khoản thu VNPAY và phát hành vé.";
    }

    public async Task<string> SendRefundAsync(int id, string? reference, string actor, CancellationToken ct)
    {
        var claimed = await ClaimRefundAsync(id, reference, actor, ct);
        (string Code, string Status, string? Number)? result = null;
        try
        {
            result = await client.RefundAsync(claimed, actor, ct);
        }
        catch (VnpayApiException)
        {
            // The gateway may have received the request before the connection failed.
        }

        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var refund = await reconciliation.Refunds().SingleAsync(r => r.Id == id, ct);
        if (refund.TrangThai != TrangThaiHoanTien.ChoXuLy || refund.MaGiaoDich != claimed.MaGiaoDich
            || refund.KetQuaNhaCungCap != "DangGui")
            return "Lệnh hoàn đã được cập nhật trong lúc chờ phản hồi.";

        var now = clock.GetUtcNow();
        refund.KetQuaNhaCungCap = result is { } response ? $"{response.Code}/{response.Status}" : "ChuaXacMinh";
        if (result is { Code: "00", Status: "00", Number: { } number })
        {
            try
            {
                await reconciliation.EnsureRefundCapacityAsync(refund, ct);
                await reconciliation.CompleteRefundAsync(refund, number, now, ct);
            }
            catch (VnpayApiException)
            {
                refund.KetQuaNhaCungCap = "ChuaXacMinh";
            }
        }
        else if (result is { Code: "00", Status: "09" })
        {
            refund.TrangThai = TrangThaiHoanTien.ThatBai;
            refund.NgayHoanTat = now;
        }

        reconciliation.AddHistory(refund.ThanhToan.DatVe, "PhanHoiHoanVnpay", "Đã lưu phản hồi lệnh hoàn VNPAY.", actor, now,
            new { refund.MaGiaoDich, refund.MaGiaoDichNhaCungCap, refund.KetQuaNhaCungCap, refund.SoTien });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return refund.TrangThai switch
        {
            TrangThaiHoanTien.ThanhCong => "VNPAY đã xác nhận hoàn tiền.",
            TrangThaiHoanTien.ThatBai => "VNPAY đã từ chối hoàn. Có thể lập lần gửi mới sau khi kiểm tra.",
            _ => "Chưa xác minh kết quả cuối. Tra soát trên cổng merchant bằng mã yêu cầu trước khi xử lý tiếp."
        };
    }

    private async Task<HoanTien> ClaimRefundAsync(int id, string? reference, string actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var refund = await reconciliation.Refunds()
            .SingleOrDefaultAsync(r => r.Id == id && r.NhaCungCap == VnpayOptions.Provider, ct)
            ?? throw new VnpayApiException("Không tìm thấy lệnh hoàn VNPAY.");
        var options = configuration.Value;
        if (!options.Ready || refund.ThanhToan.MaDiemThanhToan != options.MerchantCode)
            throw new VnpayApiException("VNPAY Sandbox chưa được cấu hình cho điểm thanh toán này.");
        if (!string.Equals(reference, refund.MaGiaoDich, StringComparison.Ordinal))
            throw new VnpayApiException("Mã lệnh đã thay đổi. Tải lại trang để kiểm tra lần gửi mới.");
        if (refund.TrangThai != TrangThaiHoanTien.ChoXuLy || refund.NgayGuiCongThanhToan.HasValue)
            throw new VnpayApiException("Lệnh hoàn đã được xử lý hoặc đang chờ tra soát.");
        if (string.IsNullOrEmpty(refund.ThanhToan.MaGiaoDichNhaCungCap) || string.IsNullOrEmpty(refund.MaGiaoDich)
            || !VnpayProtocol.TryAmount(refund.SoTien, out _))
            throw new VnpayApiException("Lệnh hoàn chưa đủ điều kiện gửi.");

        await reconciliation.EnsureRefundCapacityAsync(refund, ct);
        var now = clock.GetUtcNow();
        refund.NgayGuiCongThanhToan = now;
        refund.KetQuaNhaCungCap = "DangGui";
        reconciliation.AddHistory(refund.ThanhToan.DatVe, "GuiLenhHoanVnpay", "Đã tiếp nhận lần gửi lệnh hoàn.", actor, now,
            new { refund.MaGiaoDich, refund.SoTien });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return refund;
    }
}
