using System.Security.Cryptography;
using System.Text.Json;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure.Email;
using AirlineTicketing.Web.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AirlineTicketing.Web.Infrastructure;

public sealed class TicketAfterSalesService(AppDbContext db, TimeProvider clock)
{
    private const string ChangeProvider = "DoiVe Demo";

    public static bool IsDemoCapture(ThanhToan payment) =>
        payment.NhaCungCap == ChangeProvider ||
        (payment.NhaCungCap.StartsWith("Sandbox ", StringComparison.Ordinal) &&
            payment.MaGiaoDich?.StartsWith("DEMO", StringComparison.Ordinal) == true);

    public static IEnumerable<ThanhToan> Captures(DatVe booking) =>
        booking.ThanhToans.Where(x => x.TrangThai == TrangThaiThanhToan.ThanhCong && !VnpayBookingService.IsRefundOnly(x));

    public static bool SupportsDemoChange(DatVe booking) =>
        booking.Ves.Count == 1 && !JourneyBookingBuilder.UsesJourney(booking)
        && Captures(booking).Any() && Captures(booking).All(IsDemoCapture);

    public static decimal ExtraCharge(decimal originalFare, decimal quotedFare, decimal fee) =>
        Math.Max(0m, quotedFare - originalFare) + fee;

    public static decimal RefundAmount(DatVe booking, YeuCauDoiHoan request) =>
        Math.Max(0m, booking.TongTien - request.PhiHoan - booking.PhiDoiDaThu
            - request.PhiDichVuKhongHoan - request.PhiHanhLyKhongHoan);

    public IQueryable<ChuyenBay> ChangeCandidates(ChuyenBay original)
    {
        var cutoff = clock.GetUtcNow().AddHours(TicketRules.ChangeCutoffHours);
        return db.ChuyenBays.Where(x => x.Id != original.Id && x.TuyenBayId == original.TuyenBayId
            && x.TuyenBay.DangKhaiThac && x.MayBay.DangHoatDong
            && x.TuyenBay.SanBayDi.DangHoatDong && x.TuyenBay.SanBayDen.DangHoatDong
            && x.TrangThai == TrangThaiChuyenBay.DuKien && x.GioKhoiHanh > cutoff);
    }

    public static bool CanCompleteDemoRefund(YeuCauDoiHoan request) =>
        request.LoaiYeuCau == LoaiYeuCau.HoanVe && request.TrangThai == TrangThaiYeuCau.DaDuyet
        && request.DatVe.TrangThai == TrangThaiDatVe.DaXacNhan && request.SoTienHoan > 0
        && Captures(request.DatVe).All(IsDemoCapture)
        && DemoRefunds(request).Sum(r => r.SoTien) == request.SoTienHoan;

    private static IEnumerable<HoanTien> DemoRefunds(YeuCauDoiHoan request) =>
        Captures(request.DatVe).Where(IsDemoCapture).SelectMany(p => p.HoanTiens)
            .Where(r => r.YeuCauDoiHoanId == request.Id && r.TrangThai == TrangThaiHoanTien.ChoXuLy);

    public async Task PrepareChangeAsync(YeuCauDoiHoan request, CancellationToken ct)
    {
        var ticket = await ValidateChangeAsync(request, ct);
        if (await FindChangeSeatAsync(request.ChuyenBayMoiId!.Value, ct) is null)
            throw new BookingValidationException("seat", "Chuyến mới đã hết ghế tiêu chuẩn.");
        request.ChuyenBayGocId = ticket.ChuyenBayId;
        request.GiaVeMoi = request.ChuyenBayMoi!.GiaCoBan;
    }

    public IReadOnlyList<HoanTien> PrepareRefund(YeuCauDoiHoan request, DateTimeOffset now, bool fullRefund = false)
    {
        var booking = request.DatVe;
        if (booking.TrangThai != TrangThaiDatVe.DaXacNhan || !booking.Ves.Any(x => x.TrangThai == TrangThaiVe.DaPhatHanh))
            throw new BookingValidationException("refund", "Đặt chỗ không còn trong trạng thái đã xác nhận.");
        if (!fullRefund && FlightDisruptionService.NeedsAttention(booking))
            throw new BookingValidationException("refund", "Dùng mục Biến động chuyến bay để hoàn đơn bị ảnh hưởng.");
        var payments = Captures(booking).OrderBy(x => x.NgayTao).ThenBy(x => x.Id).ToArray();
        if (payments.Length == 0)
            throw new BookingValidationException("refund", "Đặt chỗ chưa có giao dịch đã thanh toán.");
        var amount = fullRefund ? booking.TongTien : RefundAmount(booking, request);
        if (amount <= 0)
            throw new BookingValidationException("refund", "Đơn không có khoản tiền có thể hoàn sau khi trừ phí.");

        if (payments.Any(x => x.HoanTiens.Any(r => r.TrangThai != TrangThaiHoanTien.ThatBai)))
            throw new BookingValidationException("refund", "Đặt chỗ đã có lệnh hoàn cần xử lý hoặc đã hoàn.");
        if (amount > payments.Sum(x => x.SoTien))
            throw new BookingValidationException("refund", "Số tiền hoàn vượt số tiền đã thanh toán.");
        if (payments.Length > 1 && !payments.All(IsDemoCapture))
            throw new BookingValidationException("refund", "Cần đối soát thủ công vì đơn có nhiều giao dịch ngoài mô phỏng.");

        request.SoTienHoan = amount;
        var refunds = new List<HoanTien>();
        foreach (var payment in payments)
        {
            var part = Math.Min(amount, payment.SoTien);
            var refund = new HoanTien
            {
                ThanhToanId = payment.Id, YeuCauDoiHoan = request, SoTien = part,
                DonViTienTe = payment.DonViTienTe, NhaCungCap = payment.NhaCungCap,
                MaGiaoDich = VnpayProtocol.NewReference(), LyDo = request.LyDo, NgayTao = now
            };
            refunds.Add(refund);
            db.HoanTiens.Add(refund);
            amount -= part;
            if (amount == 0) break;
        }
        return refunds;
    }

    public async Task CompleteChangeAsync(string code, IQueryable<DatVe> ownedBookings, decimal expectedCharge,
        string actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var request = await Requests().SingleOrDefaultAsync(x => x.MaYeuCau == code && x.LoaiYeuCau == LoaiYeuCau.DoiVe
            && ownedBookings.Any(booking => booking.Id == x.DatVeId), ct);
        if (request is null)
            throw new BookingValidationException("request", "Không tìm thấy yêu cầu đổi vé.");
        if (request.TrangThai == TrangThaiYeuCau.HoanTat) return;
        if (request.TrangThai != TrangThaiYeuCau.DaDuyet || !request.GiaVeMoi.HasValue)
            throw new BookingValidationException("request", "Yêu cầu chưa có báo giá đã duyệt.");

        var ticket = await ValidateChangeAsync(request, ct);
        if (request.ChuyenBayGocId != ticket.ChuyenBayId)
            throw new BookingValidationException("request", "Chuyến trên vé đã thay đổi. Vui lòng liên hệ hỗ trợ.");
        var charge = ExtraCharge(ticket.GiaVe, request.GiaVeMoi.Value, request.PhiDoi);
        if (charge != expectedCharge)
            throw new BookingValidationException("price", "Số tiền cần xác nhận đã thay đổi. Vui lòng tải lại báo giá.");

        var booking = request.DatVe;
        var seat = await FindChangeSeatAsync(request.ChuyenBayMoiId!.Value, ct);
        if (seat is null)
            throw new BookingValidationException("seat", "Chuyến mới đã hết ghế tiêu chuẩn. Vé cũ vẫn còn hiệu lực.");
        var original = new { ticket.ChuyenBayId, ticket.MaVe, ticket.SoGhe, ticket.GiaVe };
        var now = clock.GetUtcNow();
        ticket.ChuyenBay = request.ChuyenBayMoi!;
        ticket.ChuyenBayId = request.ChuyenBayMoiId.Value;
        ticket.SoGhe = seat;
        ticket.MaVe = NewTicketNumber();
        ticket.NgayPhatHanh = now;
        ticket.GiaVe = Math.Max(ticket.GiaVe, request.GiaVeMoi.Value);
        booking.PhiDoiDaThu += request.PhiDoi;
        booking.TongTien += charge;
        if (charge > 0)
        {
            booking.ThanhToans.Add(new ThanhToan
            {
                SoTien = charge, NhaCungCap = ChangeProvider, PhuongThuc = PhuongThucThanhToan.TheNganHang,
                TrangThai = TrangThaiThanhToan.ThanhCong, MaGiaoDich = $"CHD{request.Id}", NgayTao = now, NgayHoanTat = now
            });
        }
        request.TrangThai = TrangThaiYeuCau.HoanTat;
        request.NgayXuLy = now;
        request.GhiChuXuLy = $"Đã đổi sang {ticket.ChuyenBay.SoHieu}, ghế {seat}. Thu thêm mô phỏng {Money.Vnd(charge)}.";
        WriteHistory(request, actor, "DoiVeHoanTat", now, new { original, NewTicket = ticket.MaVe, Seat = seat, Charge = charge });
        await EmailDeliveryQueue.StageUpdatedTicketAsync(db, booking.Id, actor, now, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new BookingValidationException("seat", "Chỗ trên chuyến mới vừa thay đổi. Vé cũ vẫn còn hiệu lực.");
        }
    }

    public async Task CompleteDemoRefundAsync(int requestId, int? staffId, string actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var request = await Requests().SingleOrDefaultAsync(x => x.Id == requestId && x.LoaiYeuCau == LoaiYeuCau.HoanVe, ct);
        if (request is null)
            throw new BookingValidationException("request", "Không tìm thấy yêu cầu hoàn vé.");
        if (request.TrangThai == TrangThaiYeuCau.HoanTat) return;
        var booking = request.DatVe;
        if (!CanCompleteDemoRefund(request))
            throw new BookingValidationException("refund", "Chỉ hoàn tất được lệnh đã duyệt của thanh toán mô phỏng.");
        var now = clock.GetUtcNow();
        MarkDemoRefundCompleted(request, DemoRefunds(request).ToArray(), staffId, now);
        await FlightDisruptionService.CloseForRefundAsync(db, booking.Id, now, ct);
        WriteHistory(request, actor, "HoanVeDemoHoanTat", now, new { request.SoTienHoan });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    internal static string NewTicketNumber() => "738" + Convert.ToHexString(RandomNumberGenerator.GetBytes(10));

    internal static void MarkDemoRefundCompleted(YeuCauDoiHoan request, IEnumerable<HoanTien> refunds,
        int? staffId, DateTimeOffset now)
    {
        var booking = request.DatVe;
        foreach (var refund in refunds)
        {
            refund.TrangThai = TrangThaiHoanTien.ThanhCong;
            refund.NgayHoanTat = now;
        }
        booking.TrangThai = TrangThaiDatVe.HoanTien;
        foreach (var ticket in booking.Ves.Where(x => x.TrangThai == TrangThaiVe.DaPhatHanh))
        {
            ticket.TrangThai = TrangThaiVe.DaHoanTien;
            ticket.NgayHuy = now;
            ticket.LyDoHuy = "Hoàn vé mô phỏng";
        }
        request.TrangThai = TrangThaiYeuCau.HoanTat;
        request.NgayXuLy = now;
        request.NguoiXuLyId = staffId;
        request.GhiChuXuLy = $"Đã hoàn mô phỏng {Money.Vnd(request.SoTienHoan)} cho toàn bộ đặt chỗ.";
    }

    private IQueryable<YeuCauDoiHoan> Requests() => db.YeuCauDoiHoans.AsSplitQuery()
        .Include(x => x.ChuyenBayMoi!).ThenInclude(x => x.MayBay)
        .Include(x => x.ChuyenBayMoi!).ThenInclude(x => x.TuyenBay)
        .Include(x => x.DatVe).ThenInclude(x => x.BienDongs.Where(change => change.NgayXuLy == null))
        .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay)
        .Include(x => x.DatVe).ThenInclude(x => x.ThanhToans).ThenInclude(x => x.HoanTiens);

    private async Task<Ve> ValidateChangeAsync(YeuCauDoiHoan request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (FlightDisruptionService.NeedsAttention(request.DatVe))
            throw new BookingValidationException("schedule", "Đặt chỗ đang chờ hỗ trợ do thay đổi lịch bay.");
        if (!SupportsDemoChange(request.DatVe) || !TicketRules.CanRequestChangeOrRefund(request.DatVe, now))
            throw new BookingValidationException("request", "Chỉ đổi vé Phổ thông một chiều, một khách đã thanh toán mô phỏng và còn điều kiện đổi.");
        var ticket = request.DatVe.Ves.Single();
        if (request.ChuyenBayMoiId is not int candidateId || candidateId == ticket.ChuyenBayId)
            throw new BookingValidationException("flight", "Vui lòng chọn chuyến bay thay thế.");
        var next = await ChangeCandidates(ticket.ChuyenBay)
            .SingleOrDefaultAsync(x => x.Id == candidateId, ct);
        if (next is null)
            throw new BookingValidationException("flight", "Chuyến mới phải cùng tuyến, còn mở bán và khởi hành sau ít nhất 3 giờ.");
        request.ChuyenBayMoi = next;
        return ticket;
    }

    private async Task<string?> FindChangeSeatAsync(int flightId, CancellationToken ct)
    {
        var map = await FlightServices.ChuyenBay(db).LaySoDoGheAsync(flightId, ct);
        return map.Ghes.FirstOrDefault(gh => gh.TrangThai == "Trong" && gh.PhuThu == 0)?.SoGhe;
    }

    private void WriteHistory(YeuCauDoiHoan request, string actor, string action, DateTimeOffset now, object details) =>
        db.LichSuDatVes.Add(new LichSuDatVe
        {
            DatVeId = request.DatVeId, HanhDong = action, TrangThaiSau = request.DatVe.TrangThai.ToString(),
            LyDo = request.GhiChuXuLy, NguoiThucHien = actor, ThoiDiem = now,
            DuLieuBoSung = JsonSerializer.Serialize(details)
        });
}
