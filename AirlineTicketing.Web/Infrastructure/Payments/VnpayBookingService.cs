using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AirlineTicketing.Web.Infrastructure.Payments;

public sealed record VnpayResult(string Code, string Message);

public sealed class VnpayBookingService(AppDbContext db, IOptions<VnpayOptions> configuration, TimeProvider clock)
{
    private readonly VnpayOptions options = configuration.Value;

    public async Task<string> StartAsync(int bookingId, decimal? expectedTotal, string ip, CancellationToken ct)
    {
        if (!options.Ready)
            throw new BookingValidationException("payment", "VNPAY Sandbox chưa được cấu hình.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var booking = await db.DatVes.Include(x => x.Ves).ThenInclude(x => x.ChuyenBay)
            .Include(x => x.ThanhToans).ThenInclude(x => x.HoanTiens).SingleAsync(x => x.Id == bookingId, ct);
        var now = clock.GetUtcNow();
        if (!BookingCheckoutService.CanIssue(booking, now) || booking.HetHanGiuCho - now < TimeSpan.FromSeconds(2))
            throw new BookingValidationException("payment", "Đặt chỗ đã hết hạn hoặc chuyến bay không còn mở bán.");
        if (expectedTotal != booking.TongTien)
            throw new BookingValidationException("promotion", "Tổng tiền đã thay đổi. Vui lòng kiểm tra lại trước khi thanh toán.");
        if (booking.DonViTienTe != "VND" || !VnpayProtocol.TryAmount(booking.TongTien, out _))
            throw new BookingValidationException("payment", "Số tiền của đặt chỗ nằm ngoài giới hạn VNPAY.");

        var payment = booking.ThanhToans.FirstOrDefault(IsBlockingPayment);
        if (payment is not null && (payment.TrangThai != TrangThaiThanhToan.ChoXuLy
                || payment.NhaCungCap != VnpayOptions.Provider || payment.MaDiemThanhToan != options.MerchantCode))
            throw new BookingValidationException("payment", "Đơn đang có giao dịch khác. Vui lòng kiểm tra trạng thái đặt chỗ.");
        if (payment is null)
        {
            payment = new ThanhToan
            {
                DatVeId = booking.Id, SoTien = booking.TongTien, DonViTienTe = "VND",
                PhuongThuc = PhuongThucThanhToan.TheNganHang, NhaCungCap = VnpayOptions.Provider,
                MaGiaoDich = VnpayProtocol.NewReference(), MaDiemThanhToan = options.MerchantCode,
                NgayTao = now
            };
            db.ThanhToans.Add(payment);
            AddHistory(booking, "TaoGiaoDichVnpay", payment.MaGiaoDich, now);
            await db.SaveChangesAsync(ct);
        }
        var url = VnpayProtocol.CreatePayUrl(options, payment, booking.HetHanGiuCho, ip);
        await transaction.CommitAsync(ct);
        return url;
    }

    public async Task<VnpayResult> ApplyNoticeAsync(VnpayNotice notice, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var payment = await db.ThanhToans.Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay)
            .Include(x => x.DatVe).ThenInclude(x => x.ThanhToans).ThenInclude(x => x.HoanTiens)
            .SingleOrDefaultAsync(x => x.NhaCungCap == VnpayOptions.Provider
                && x.MaDiemThanhToan == options.MerchantCode && x.MaGiaoDich == notice.Reference, ct);
        if (payment is null) return new("01", "Order not found");
        if (!VnpayProtocol.TryAmount(payment.SoTien, out var amount) || amount != notice.Amount)
            return new("04", "Invalid amount");
        if (payment.TrangThai == TrangThaiThanhToan.ThanhCong)
        {
            return notice.ResponseCode == "00" && notice.Status == "00"
                && payment.MaGiaoDichNhaCungCap != notice.TransactionNo
                ? new("99", "Transaction number mismatch") : new("02", "Already recorded");
        }

        var now = clock.GetUtcNow();
        var booking = payment.DatVe;
        payment.KetQuaNhaCungCap = $"{notice.ResponseCode}/{notice.Status}";
        if (notice.ResponseCode != "00" || notice.Status != "00")
        {
            // Pending, reversed and suspected transactions need reconciliation before retrying.
            if (notice.Status == "02" || (notice.Status == "01" && notice.ResponseCode is "11" or "24"))
            {
                payment.TrangThai = TrangThaiThanhToan.ThatBai;
                payment.NgayHoanTat = now;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new("00", "Status recorded");
        }

        var numberInUse = await db.ThanhToans.AnyAsync(x => x.Id != payment.Id && x.MaDiemThanhToan == options.MerchantCode
            && x.MaGiaoDichNhaCungCap == notice.TransactionNo, ct);
        if (numberInUse) return new("99", "Transaction number already used");

        payment.MaGiaoDichNhaCungCap = notice.TransactionNo;
        payment.TrangThai = TrangThaiThanhToan.ThanhCong;
        payment.NgayHoanTat = now;
        var otherPayment = booking.ThanhToans.Any(x => x.Id != payment.Id && IsBlockingPayment(x));
        var issue = !otherPayment && payment.SoTien == booking.TongTien && BookingCheckoutService.CanIssue(booking, now);
        if (issue)
        {
            BookingCheckoutService.IssueTickets(booking, now);
            EmailDeliveryQueue.StageTicket(db, booking.Id, VnpayOptions.Provider, now);
            AddHistory(booking, "VnpayDaThanhToan", notice.TransactionNo, now);
        }
        else
        {
            db.HoanTiens.Add(PaymentReconciliationService.RefundForUnusedCapture(payment, now));
            AddHistory(booking, "VnpayCanHoan", notice.TransactionNo, now);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new("00", "Confirm success");
    }

    // These captures never bought a ticket. Keep them in the ledger without counting them in the booking's receipt.
    internal static bool IsRefundOnly(ThanhToan payment) => payment.NhaCungCap is (VnpayOptions.Provider or VietQr.Provider)
        && payment.HoanTiens.Any(refund => !refund.YeuCauDoiHoanId.HasValue);

    private static bool IsBlockingPayment(ThanhToan payment) =>
        payment.TrangThai != TrangThaiThanhToan.ThatBai && !IsRefundOnly(payment);

    internal void AddHistory(DatVe booking, string action, string? detail, DateTimeOffset now) =>
        db.LichSuDatVes.Add(new LichSuDatVe
        {
            DatVeId = booking.Id, HanhDong = action, LyDo = detail,
            ThoiDiem = now, TrangThaiSau = booking.TrangThai.ToString(), NguoiThucHien = VnpayOptions.Provider
        });
}
