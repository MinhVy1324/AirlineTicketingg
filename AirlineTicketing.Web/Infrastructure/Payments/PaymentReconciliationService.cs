using System.Globalization;
using System.Text.Json;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Infrastructure.Payments;

public sealed class PaymentReconciliationService(AppDbContext db, TimeProvider clock)
{
    internal const string ReviewedSuccess = "DoiSoatThanhCong";
    internal const string ReviewedFailure = "DoiSoatThatBai";

    public async Task<string> RecordTransferAsync(int id, string? reference, string? receipt, string? amount,
        string actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var payment = await db.ThanhToans.AsSplitQuery()
            .Include(p => p.DatVe).ThenInclude(b => b.Ves).ThenInclude(v => v.ChuyenBay)
            .Include(p => p.DatVe).ThenInclude(b => b.ThanhToans).ThenInclude(p => p.HoanTiens)
            .SingleOrDefaultAsync(p => p.Id == id && p.NhaCungCap == VietQr.Provider, ct)
            ?? throw new VnpayApiException("Không tìm thấy khoản thu VietQR.");
        var proof = CheckEvidence(payment.MaGiaoDich, reference, receipt, amount, payment.SoTien);
        if (payment.TrangThai == TrangThaiThanhToan.ThanhCong)
            return "Khoản thu này đã được ghi nhận.";
        if (payment.TrangThai != TrangThaiThanhToan.ChoXuLy)
            throw new VnpayApiException("Khoản thu không còn ở trạng thái chờ đối soát.");
        await EnsureUnusedEvidenceAsync(payment.NhaCungCap, proof, ct);

        var booking = payment.DatVe;
        var now = clock.GetUtcNow();
        var issue = booking.TongTien == payment.SoTien && BookingCheckoutService.CanIssue(booking, now)
            && !TicketAfterSalesService.Captures(booking).Any();
        payment.TrangThai = TrangThaiThanhToan.ThanhCong;
        payment.NgayHoanTat = now;
        payment.KetQuaNhaCungCap = ReviewedSuccess;
        if (issue)
        {
            BookingCheckoutService.IssueTickets(booking, now);
            EmailDeliveryQueue.StageTicket(db, booking.Id, actor, now);
        }
        else
        {
            db.HoanTiens.Add(RefundForUnusedCapture(payment, now));
        }

        AddEvidence(payment, null, proof, ReviewedSuccess, payment.SoTien, actor, now);
        AddHistory(booking, "DoiSoatThuVietQR", issue ? "Đã xác nhận khoản thu và phát hành vé." : "Đã ghi nhận khoản thu cần hoàn toàn bộ.",
            actor, now, new { payment.MaGiaoDich, Proof = proof, payment.SoTien });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return issue ? "Đã ghi nhận khoản thu VietQR và phát hành vé."
            : "Đã ghi nhận khoản thu VietQR. Đơn không còn đủ điều kiện phát hành nên đã tạo lệnh hoàn toàn bộ.";
    }

    public async Task<string> RecordRefundAsync(int id, string? reference, string? receipt, string? amount,
        string? refundNumber, bool succeeded, string actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var refund = await Refunds().SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new VnpayApiException("Không tìm thấy lệnh hoàn.");
        var proof = CheckEvidence(refund.MaGiaoDich, reference, receipt, amount, refund.SoTien);
        if (refund.TrangThai != TrangThaiHoanTien.ChoXuLy)
            return "Lệnh hoàn này đã được xử lý.";
        var now = clock.GetUtcNow();
        if (refund.NhaCungCap == VnpayOptions.Provider)
        {
            if (!refund.NgayGuiCongThanhToan.HasValue)
                throw new VnpayApiException("Cần gửi lệnh hoàn VNPAY trước khi đối soát kết quả.");
            if (refund.KetQuaNhaCungCap == "DangGui" && now - refund.NgayGuiCongThanhToan.Value < TimeSpan.FromSeconds(30))
                throw new VnpayApiException("VNPAY đang nhận lệnh. Vui lòng đợi phản hồi trước khi đối soát.");
            if (succeeded && !ValidRefundNumber(refundNumber))
                throw new VnpayApiException("Nhập mã giao dịch hoàn ghi trên hệ thống VNPAY.", "refundNumber");
        }
        else if (!succeeded)
        {
            throw new VnpayApiException("Chỉ ghi nhận hoàn VietQR khi chứng từ xác nhận khoản tiền đã được chuyển trả.");
        }

        await EnsureUnusedEvidenceAsync(refund.NhaCungCap, proof, ct);
        await EnsureRefundCapacityAsync(refund, ct);
        var result = succeeded ? ReviewedSuccess : ReviewedFailure;
        AddEvidence(refund.ThanhToan, refund, proof, result, refund.SoTien, actor, now);
        refund.KetQuaNhaCungCap = result;
        if (succeeded)
            await CompleteRefundAsync(refund, refund.NhaCungCap == VnpayOptions.Provider ? refundNumber : null, now, ct);
        else
        {
            refund.TrangThai = TrangThaiHoanTien.ThatBai;
            refund.NgayHoanTat = now;
        }
        AddHistory(refund.ThanhToan.DatVe, "DoiSoatHoan", "Đã đối chiếu chứng từ lệnh hoàn.",
            actor, now, new { refund.MaGiaoDich, Proof = proof, refund.SoTien, Result = result });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return succeeded ? "Đã ghi nhận hoàn tiền theo chứng từ." : "Đã ghi nhận VNPAY từ chối hoàn. Có thể lập lần gửi mới sau khi kiểm tra.";
    }

    public async Task<string> PrepareRefundRetryAsync(int id, string? reference, string actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var refund = await Refunds().SingleOrDefaultAsync(r => r.Id == id && r.NhaCungCap == VnpayOptions.Provider, ct)
            ?? throw new VnpayApiException("Không tìm thấy lệnh hoàn VNPAY.");
        if (!string.Equals(refund.MaGiaoDich, reference, StringComparison.OrdinalIgnoreCase) || !CanRetry(refund))
            throw new VnpayApiException("Chỉ lập lần gửi mới sau khi VNPAY đã xác nhận từ chối lệnh hiện tại.");
        await EnsureRefundCapacityAsync(refund, ct);
        var previous = refund.MaGiaoDich;
        refund.MaGiaoDich = VnpayProtocol.NewReference();
        refund.TrangThai = TrangThaiHoanTien.ChoXuLy;
        refund.NgayGuiCongThanhToan = null;
        refund.NgayHoanTat = null;
        refund.MaGiaoDichNhaCungCap = null;
        refund.KetQuaNhaCungCap = null;
        AddHistory(refund.ThanhToan.DatVe, "LapLaiLenhHoan", "Đã tạo mã yêu cầu mới sau khi đối soát thất bại.", actor,
            clock.GetUtcNow(), new { Previous = previous, Current = refund.MaGiaoDich, refund.SoTien });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return "Đã lập lần gửi mới. Kiểm tra số tiền và chọn Gửi lệnh hoàn khi sẵn sàng.";
    }

    internal static bool CanRetry(HoanTien refund) => refund.NhaCungCap == VnpayOptions.Provider
        && refund.TrangThai == TrangThaiHoanTien.ThatBai && refund.KetQuaNhaCungCap is ReviewedFailure or "00/09";

    internal IQueryable<HoanTien> Refunds() => db.HoanTiens.AsSplitQuery()
        .Include(r => r.ThanhToan).ThenInclude(p => p.DatVe).ThenInclude(b => b.Ves)
        .Include(r => r.YeuCauDoiHoan)
        .Where(r => r.NhaCungCap == VnpayOptions.Provider || r.NhaCungCap == VietQr.Provider);

    internal async Task EnsureRefundCapacityAsync(HoanTien refund, CancellationToken ct)
    {
        if (refund.ThanhToan.TrangThai != TrangThaiThanhToan.ThanhCong || refund.NhaCungCap != refund.ThanhToan.NhaCungCap
            || refund.DonViTienTe != refund.ThanhToan.DonViTienTe || refund.SoTien <= 0
            || (refund.YeuCauDoiHoanId.HasValue && (refund.YeuCauDoiHoan?.TrangThai != TrangThaiYeuCau.DaDuyet
                || refund.YeuCauDoiHoan.SoTienHoan != refund.SoTien
                || refund.YeuCauDoiHoan.DatVeId != refund.ThanhToan.DatVeId)))
            throw new VnpayApiException("Lệnh hoàn chưa khớp khoản thu hoặc yêu cầu đã duyệt.");
        var committed = await db.HoanTiens.Where(r => r.ThanhToanId == refund.ThanhToanId && r.Id != refund.Id
            && (r.TrangThai == TrangThaiHoanTien.ThanhCong
                || r.TrangThai == TrangThaiHoanTien.ChoXuLy && r.NgayGuiCongThanhToan.HasValue))
            .SumAsync(r => r.SoTien, ct);
        if (committed + refund.SoTien > refund.ThanhToan.SoTien)
            throw new VnpayApiException("Tổng số tiền hoàn vượt số tiền đã thu.");
    }

    internal async Task CompleteRefundAsync(HoanTien refund, string? providerNumber, DateTimeOffset now, CancellationToken ct)
    {
        if (providerNumber is not null && (providerNumber == refund.ThanhToan.MaGiaoDichNhaCungCap
            || await db.HoanTiens.AnyAsync(r => r.Id != refund.Id && r.NhaCungCap == refund.NhaCungCap
                && r.ThanhToan.MaDiemThanhToan == refund.ThanhToan.MaDiemThanhToan
                && r.MaGiaoDichNhaCungCap == providerNumber, ct)))
            throw new VnpayApiException("Mã giao dịch hoàn đã được dùng. Kiểm tra lại phản hồi hoặc chứng từ.");
        refund.TrangThai = TrangThaiHoanTien.ThanhCong;
        refund.NgayHoanTat = now;
        refund.MaGiaoDichNhaCungCap = providerNumber;
        if (refund.YeuCauDoiHoan is not { } request) return;

        request.TrangThai = TrangThaiYeuCau.HoanTat;
        request.NgayXuLy = now;
        request.GhiChuXuLy = $"Đã ghi nhận hoàn {Money.Vnd(refund.SoTien)} qua {refund.NhaCungCap}.";
        var booking = refund.ThanhToan.DatVe;
        booking.TrangThai = TrangThaiDatVe.HoanTien;
        foreach (var ticket in booking.Ves.Where(v => v.TrangThai == TrangThaiVe.DaPhatHanh))
        {
            ticket.TrangThai = TrangThaiVe.DaHoanTien;
            ticket.NgayHuy = now;
            ticket.LyDoHuy = $"Hoàn vé qua {refund.NhaCungCap}";
        }
        await FlightDisruptionService.CloseForRefundAsync(db, booking.Id, now, ct);
        await EmailDeliveryQueue.CancelPendingTicketAsync(db, booking.Id, now, "Đặt chỗ đã hoàn vé.", ct);
    }

    internal static HoanTien RefundForUnusedCapture(ThanhToan payment, DateTimeOffset now) => new()
    {
        ThanhToan = payment, SoTien = payment.SoTien, DonViTienTe = payment.DonViTienTe,
        NhaCungCap = payment.NhaCungCap, MaGiaoDich = VnpayProtocol.NewReference(), NgayTao = now,
        LyDo = "Đã ghi nhận tiền nhưng đơn không còn đủ điều kiện phát hành. Hoàn toàn bộ giao dịch."
    };

    private static string CheckEvidence(string? current, string? reference, string? receipt, string? amount, decimal expected)
    {
        if (string.IsNullOrEmpty(current) || !current.Equals(reference?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new VnpayApiException("Mã lệnh không khớp. Tải lại trang để kiểm tra lệnh hiện tại.");
        if (!decimal.TryParse(amount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var observed)
            || observed != expected)
            throw new VnpayApiException("Số tiền trên chứng từ phải khớp số tiền của lệnh.", "amount");
        var key = receipt?.Trim().ToUpperInvariant();
        if (key is not { Length: >= 3 and <= 64 } || !char.IsAsciiLetterOrDigit(key[0])
            || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.' or '/')))
            throw new VnpayApiException("Nhập 3–64 ký tự, bắt đầu bằng chữ hoặc số. Có thể dùng dấu -, _, . hoặc /.", "receipt");
        return key;
    }

    private Task<bool> EvidenceUsedAsync(string provider, string receipt, CancellationToken ct) =>
        db.ChungTuDoiSoats.AnyAsync(x => x.NhaCungCap == provider && x.MaChungTu == receipt, ct);

    private async Task EnsureUnusedEvidenceAsync(string provider, string receipt, CancellationToken ct)
    {
        if (await EvidenceUsedAsync(provider, receipt, ct))
            throw new VnpayApiException("Chứng từ đã được dùng cho một lệnh đối soát.", "receipt");
    }

    internal static bool ValidRefundNumber(string? value) =>
        value is { Length: >= 1 and <= 15 } && value.All(char.IsAsciiDigit) && value.Any(c => c != '0');

    private void AddEvidence(ThanhToan payment, HoanTien? refund, string receipt, string result, decimal amount,
        string actor, DateTimeOffset now) => db.ChungTuDoiSoats.Add(new()
        {
            ThanhToanId = payment.Id, HoanTienId = refund?.Id, NhaCungCap = payment.NhaCungCap,
            MaLenh = (refund?.MaGiaoDich ?? payment.MaGiaoDich)!, MaChungTu = receipt,
            KetQua = result, SoTien = amount, NguoiDoiSoat = actor[..Math.Min(150, actor.Length)], ThoiDiem = now
        });

    internal void AddHistory(DatVe booking, string action, string reason, string actor, DateTimeOffset now, object data) =>
        db.LichSuDatVes.Add(new()
        {
            DatVeId = booking.Id, HanhDong = action, LyDo = reason, TrangThaiSau = booking.TrangThai.ToString(),
            NguoiThucHien = actor[..Math.Min(150, actor.Length)], ThoiDiem = now, DuLieuBoSung = JsonSerializer.Serialize(data)
        });
}
