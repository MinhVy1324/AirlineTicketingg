using System.Net;
using System.Text.Json;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.Infrastructure.Email;
using AirlineTicketing.Web.Infrastructure.Tickets;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Infrastructure;

public sealed class FlightDisruptionService(AppDbContext db, TicketAfterSalesService afterSales,
    EmailDeliveryQueue email, TimeProvider clock, IConfiguration configuration)
{
    public static bool NeedsAttention(DatVe booking) => booking.TrangThai == TrangThaiDatVe.DaXacNhan
        && (booking.BienDongs.Any(x => x.NgayXuLy == null)
            || booking.Ves.Any(x => x.TrangThai == TrangThaiVe.DaPhatHanh && x.ChuyenBay.TrangThai == TrangThaiChuyenBay.BiHuy));

    public static bool HasRefundInProgress(DatVe booking) => TicketAfterSalesService.Captures(booking)
        .SelectMany(x => x.HoanTiens).Any(x => x.TrangThai != TrangThaiHoanTien.ThatBai);

    public static bool CanRefundDemo(DatVe booking, DateTimeOffset now) =>
        booking.TrangThai == TrangThaiDatVe.DaXacNhan && !HasRefundInProgress(booking)
        && TicketAfterSalesService.Captures(booking).Any()
        && TicketAfterSalesService.Captures(booking).All(TicketAfterSalesService.IsDemoCapture)
        && booking.Ves.Where(v => v.TrangThai == TrangThaiVe.DaPhatHanh).All(v =>
            v.ChuyenBay.TrangThai == TrangThaiChuyenBay.BiHuy
            || (v.ChuyenBay.TrangThai == TrangThaiChuyenBay.DuKien && v.ChuyenBay.GioKhoiHanh > now));

    public Task<KetQuaXuLy> UpdateScheduleAsync(CapNhatChuyenBayDTO edit, string actor, CancellationToken ct) =>
        ChangeScheduleAsync(edit.Id, edit, actor, ct);

    public Task<KetQuaXuLy> CancelAsync(int id, string? reason, string? customerNote, string actor, CancellationToken ct) =>
        ChangeScheduleAsync(id, null, actor, ct, Limit(reason, 300), Limit(customerNote, 1000));

    private async Task<KetQuaXuLy> ChangeScheduleAsync(int id, CapNhatChuyenBayDTO? edit, string actor, CancellationToken ct,
        string? reason = null, string? customerNote = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var flight = await db.ChuyenBays.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (flight is null) return KetQuaXuLy.ThatBai("Không tìm thấy chuyến bay cần cập nhật.");

        var cancelled = edit is null || edit.TrangThai.Trim().Equals("Đã hủy", StringComparison.OrdinalIgnoreCase)
            || edit.TrangThai.Trim().Equals("hủy", StringComparison.OrdinalIgnoreCase)
            || edit.TrangThai.Trim().Equals(nameof(TrangThaiChuyenBay.BiHuy), StringComparison.OrdinalIgnoreCase);
        if (flight.TrangThai == TrangThaiChuyenBay.BiHuy)
            return cancelled ? KetQuaXuLy.Ok() : KetQuaXuLy.ThatBai("Chuyến đã hủy. Vui lòng tạo chuyến mới để mở bán.");
        var old = (flight.SoHieu, flight.GioKhoiHanh, flight.GioHaCanh);
        var changed = edit is not null && (old.GioKhoiHanh != edit.GioKhoiHanh || old.GioHaCanh != edit.GioHaCanh
            || !old.SoHieu.Equals(edit.SoHieu.Trim(), StringComparison.OrdinalIgnoreCase));
        var now = clock.GetUtcNow();
        if ((cancelled || changed) && (flight.TrangThai != TrangThaiChuyenBay.DuKien || old.GioKhoiHanh <= now
            || (!cancelled && edit!.GioKhoiHanh <= now)))
            return KetQuaXuLy.ThatBai("Chỉ hủy hoặc đổi giờ chuyến chưa khởi hành. Giờ mới phải ở tương lai.");
        var flights = FlightServices.ChuyenBay(db);
        var result = cancelled ? await flights.HuyChuyenBayAsync(id, ct) : await flights.CapNhatChuyenBayAsync(edit!, ct);
        if (!result.ThanhCong) return result;
        if (changed && !cancelled && flight.TrangThai != TrangThaiChuyenBay.DuKien)
            return KetQuaXuLy.ThatBai("Giữ trạng thái Mở bán khi cập nhật giờ hoặc số hiệu chuyến.");
        if (!cancelled && (flight.SoHieu, flight.GioKhoiHanh, flight.GioHaCanh) == old)
        {
            await transaction.CommitAsync(ct);
            return result;
        }

        var bookings = await db.DatVes.AsSplitQuery().Include(x => x.KhachHang).Include(x => x.Ves)
            .Include(x => x.YeuCaus).Include(x => x.BienDongs.Where(change => change.NgayXuLy == null))
            .Where(x => (x.TrangThai == TrangThaiDatVe.DaXacNhan || x.TrangThai == TrangThaiDatVe.ChoThanhToan)
                && x.Ves.Any(v => v.ChuyenBayId == id && (v.TrangThai == TrangThaiVe.DaPhatHanh || v.TrangThai == TrangThaiVe.TamGiu)))
            .ToListAsync(ct);
        foreach (var booking in bookings)
        {
            var pending = booking.TrangThai == TrangThaiDatVe.ChoThanhToan;
            foreach (var prior in booking.BienDongs.Where(x => x.ChuyenBayId == id && x.NgayXuLy == null))
                Resolve(prior, now, "Đã có thông báo lịch bay mới hơn.");
            var disruption = new BienDongDatVe
            {
                DatVeId = booking.Id, ChuyenBayId = id, NgayTao = now,
                SoHieuCu = old.SoHieu, KhoiHanhCu = old.GioKhoiHanh, HaCanhCu = old.GioHaCanh,
                SoHieuMoi = cancelled ? null : flight.SoHieu,
                KhoiHanhMoi = cancelled ? null : flight.GioKhoiHanh, HaCanhMoi = cancelled ? null : flight.GioHaCanh
            };
            db.BienDongDatVes.Add(disruption);
            var title = cancelled ? $"Chuyến {old.SoHieu} đã bị hủy" : $"Chuyến {old.SoHieu} đã cập nhật lịch bay";
            var detail = $"Lịch cũ {DateText(old.GioKhoiHanh, old.GioHaCanh)}.";
            if (!cancelled) detail += $" Lịch mới {flight.SoHieu}, {DateText(flight.GioKhoiHanh, flight.GioHaCanh)}.";
            if (customerNote is not null) detail += " " + customerNote;
            if (pending)
            {
                booking.TrangThai = TrangThaiDatVe.DaHuy;
                foreach (var ticket in booking.Ves.Where(x => x.TrangThai == TrangThaiVe.TamGiu))
                {
                    ticket.TrangThai = TrangThaiVe.DaHuy;
                    ticket.NgayHuy = now;
                    ticket.LyDoHuy = title;
                }
                Resolve(disruption, now, "Đã giải phóng ghế của đặt chỗ chưa xác nhận.");
                detail += " Giữ chỗ đã kết thúc. Nếu đã chuyển khoản, hãy liên hệ hỗ trợ để đối soát trước khi đặt lại.";
            }
            else
            {
                foreach (var request in booking.YeuCaus.Where(x => x.TrangThai == TrangThaiYeuCau.ChoDuyet
                    || (x.TrangThai == TrangThaiYeuCau.DaDuyet && x.LoaiYeuCau == LoaiYeuCau.DoiVe)))
                {
                    request.TrangThai = TrangThaiYeuCau.TuChoi;
                    request.NgayXuLy = now;
                    request.GhiChuXuLy = "Chuyển sang xử lý do hãng cập nhật lịch bay. Xem thông báo trong Vé của tôi.";
                }
                await EmailDeliveryQueue.CancelPendingTicketAsync(db, booking.Id, now, title, ct);
                detail += " Mở Vé của tôi để xem phương án xử lý.";
            }
            AddHistory(booking, "BienDongLichBay", reason ?? title, actor, now,
                new { FlightId = id, OldDeparture = disruption.KhoiHanhCu, NewDeparture = disruption.KhoiHanhMoi, CustomerNote = customerNote });
            Notify(booking, title, detail, now);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public IQueryable<ChuyenBay> Alternatives(BienDongDatVe change)
    {
        var (previous, next) = Neighbors(change);
        var query = afterSales.ChangeCandidates(change.ChuyenBay);
        if (previous is not null) query = query.Where(x => x.GioKhoiHanh > previous.GioHaCanh);
        if (next is not null) query = query.Where(x => x.GioHaCanh < next.GioKhoiHanh);
        return query;
    }

    public async Task AcceptAsync(int id, IQueryable<DatVe> owned, string actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var change = await LoadAsync(Changes().Where(x => owned.Any(b => b.Id == x.DatVeId)), id, ct);
        var flight = change.ChuyenBay;
        var now = clock.GetUtcNow();
        var (previous, next) = Neighbors(change);
        if (change.BiHuy || flight.TrangThai != TrangThaiChuyenBay.DuKien || flight.GioKhoiHanh <= now
            || (previous is not null && previous.GioHaCanh >= flight.GioKhoiHanh)
            || (next is not null && flight.GioHaCanh >= next.GioKhoiHanh))
            throw new BookingValidationException("schedule", "Lịch mới không phù hợp với hành trình. Vui lòng liên hệ hỗ trợ.");
        ValidatePassengerAges(AffectedTickets(change), flight);
        await FinishAsync(change, "Đã chấp nhận lịch bay mới.", actor, now, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RebookAsync(int id, int flightId, string actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var change = await LoadAsync(Changes(), id, ct);
        var target = await Alternatives(change).SingleOrDefaultAsync(x => x.Id == flightId, ct)
            ?? throw new BookingValidationException("flight", "Chọn chuyến cùng tuyến, còn mở bán và phù hợp với các chặng còn lại.");
        var tickets = AffectedTickets(change);
        if (tickets.Length == 0)
            throw new BookingValidationException("ticket", "Không còn vé đã phát hành trên chặng cần xử lý.");
        var persons = tickets.Select(v => v.HanhKhachId).ToArray();
        if (await db.Ves.AnyAsync(v => v.ChuyenBayId == flightId && persons.Contains(v.HanhKhachId)
            && (v.TrangThai == TrangThaiVe.TamGiu || v.TrangThai == TrangThaiVe.DaPhatHanh), ct))
            throw new BookingValidationException("ticket", "Một hành khách đã có ghế hoặc vé trên chuyến thay thế.");
        ValidatePassengerAges(tickets, target);
        var map = await FlightServices.ChuyenBay(db).LaySoDoGheAsync(flightId, ct);
        var seats = map.Ghes.Where(x => x.TrangThai == "Trong").OrderBy(x => x.PhuThu).Select(x => x.SoGhe).ToList();
        if (seats.Count < tickets.Count(v => v.LoaiHanhKhach != LoaiHanhKhach.EmBe))
            throw new BookingValidationException("seat", "Chuyến thay thế không đủ ghế cho cả nhóm.");

        var now = clock.GetUtcNow();
        var previousTickets = tickets.Select(v => new { v.Id, v.MaVe, v.SoGhe }).ToArray();
        var preferredSeats = tickets.Where(v => v.LoaiHanhKhach != LoaiHanhKhach.EmBe && seats.Remove(v.SoGhe))
            .ToDictionary(v => v.Id, v => v.SoGhe);
        foreach (var ticket in tickets)
        {
            ticket.ChuyenBay = target;
            if (ticket.LoaiHanhKhach == LoaiHanhKhach.EmBe)
                ticket.SoGhe = "";
            else if (!preferredSeats.TryGetValue(ticket.Id, out var seat))
            {
                ticket.SoGhe = seats[0];
                seats.RemoveAt(0);
            }
            else
                ticket.SoGhe = seat;
            ticket.MaVe = TicketAfterSalesService.NewTicketNumber();
            ticket.NgayPhatHanh = now;
        }
        AddHistory(change.DatVe, "ChuyenThayThe", $"Đã chuyển sang {target.SoHieu}", actor, now,
            new { change.Id, change.ChuyenBayId, ReplacementId = target.Id, PreviousTickets = previousTickets });
        await FinishAsync(change, $"Đã chuyển sang {target.SoHieu}, {DateText(target.GioKhoiHanh, target.GioHaCanh)}. Phí đổi 0 ₫.", actor, now, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RefundDemoAsync(int id, int? staffId, string actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(db, ct);
        var change = await LoadAsync(Changes(), id, ct);
        var now = clock.GetUtcNow();
        var booking = change.DatVe;
        if (!CanRefundDemo(booking, now))
            throw new BookingValidationException("refund", "Hoàn toàn bộ demo áp dụng cho đơn thẻ mô phỏng chưa bay chặng nào.");
        var request = new YeuCauDoiHoan
        {
            DatVe = booking, LoaiYeuCau = LoaiYeuCau.HoanVe, PhiHoan = 0m,
            MaYeuCau = $"RF-{Guid.NewGuid().ToString("N")[..16]}", NgayTao = now,
            LyDo = $"Hoàn toàn bộ do thay đổi khai thác · chuyến {change.SoHieuCu}",
            PhuongThucHoan = "Nguồn thanh toán mô phỏng"
        };
        db.YeuCauDoiHoans.Add(request);
        var refunds = afterSales.PrepareRefund(request, now, fullRefund: true);
        TicketAfterSalesService.MarkDemoRefundCompleted(request, refunds, staffId, now);
        foreach (var item in booking.BienDongs.Where(x => x.NgayXuLy == null))
            Resolve(item, now, $"Đã hoàn toàn bộ {Money.Vnd(request.SoTienHoan)} qua thanh toán mô phỏng.");
        AddHistory(booking, "HoanDoBienDong", request.LyDo, actor, now, new { change.Id, request.MaYeuCau, request.SoTienHoan });
        Notify(booking, "Đã hoàn vé mô phỏng", request.GhiChuXuLy!, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    internal static Task<int> CloseForRefundAsync(AppDbContext db, int bookingId, DateTimeOffset now, CancellationToken ct) =>
        db.BienDongDatVes.Where(x => x.DatVeId == bookingId && x.NgayXuLy == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.NgayXuLy, now)
                .SetProperty(x => x.KetQua, "Đặt chỗ đã hoàn tiền."), ct);

    public IQueryable<BienDongDatVe> Changes() => db.BienDongDatVes.AsSplitQuery()
        .Include(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
        .Include(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
        .Include(x => x.DatVe).ThenInclude(x => x.KhachHang)
        .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.HanhKhach)
        .Include(x => x.DatVe).ThenInclude(x => x.Ves).ThenInclude(x => x.ChuyenBay)
        .Include(x => x.DatVe).ThenInclude(x => x.ThanhToans).ThenInclude(x => x.HoanTiens);

    private async Task<BienDongDatVe> LoadAsync(IQueryable<BienDongDatVe> query, int id, CancellationToken ct)
    {
        var change = await query.SingleOrDefaultAsync(x => x.Id == id && x.NgayXuLy == null, ct);
        if (change is null || change.DatVe.TrangThai != TrangThaiDatVe.DaXacNhan)
            throw new BookingValidationException("schedule", "Thông báo không còn chờ xử lý.");
        if (HasRefundInProgress(change.DatVe))
            throw new BookingValidationException("refund", "Đặt chỗ đã có lệnh hoàn. Vui lòng xử lý lệnh hiện có.");
        await db.Entry(change.DatVe).Collection(b => b.BienDongs).Query().Where(x => x.NgayXuLy == null).LoadAsync(ct);
        return change;
    }

    private static Ve[] AffectedTickets(BienDongDatVe change) => change.DatVe.Ves
        .Where(v => v.ChuyenBayId == change.ChuyenBayId && v.TrangThai == TrangThaiVe.DaPhatHanh).OrderBy(v => v.Id).ToArray();

    private static void ValidatePassengerAges(IEnumerable<Ve> tickets, ChuyenBay target)
    {
        var date = DateOnly.FromDateTime(FlightMapping.ToVietnamTime(target.GioKhoiHanh).DateTime);
        if (tickets.Any(v => v.HanhKhach.NgaySinh is { } birth && JourneyRules.ValidateAge(birth, date, v.LoaiHanhKhach) is not null))
            throw new BookingValidationException("ticket", "Nhóm tuổi của hành khách thay đổi ở ngày bay mới. Vui lòng liên hệ hỗ trợ.");
    }

    private static (ChuyenBay? Previous, ChuyenBay? Next) Neighbors(BienDongDatVe change)
    {
        var legs = change.DatVe.Ves.Where(v => v.TrangThai == TrangThaiVe.DaPhatHanh)
            .OrderBy(v => v.Id).Select(v => v.ChuyenBay).DistinctBy(f => f.Id).ToArray();
        var index = Array.FindIndex(legs, x => x.Id == change.ChuyenBayId);
        return (index > 0 ? legs[index - 1] : null, index >= 0 && index + 1 < legs.Length ? legs[index + 1] : null);
    }

    private async Task FinishAsync(BienDongDatVe change, string result, string actor, DateTimeOffset now, CancellationToken ct)
    {
        Resolve(change, now, result);
        var booking = change.DatVe;
        AddHistory(booking, "XuLyBienDong", result, actor, now, new { change.Id, change.ChuyenBayId });
        if (!NeedsAttention(booking))
            await EmailDeliveryQueue.StageUpdatedTicketAsync(db, booking.Id, actor, now, ct);
        Notify(booking, "Đã xử lý chặng bị ảnh hưởng", result, now);
        await db.SaveChangesAsync(ct);
    }

    private void Notify(DatVe booking, string subject, string detail, DateTimeOffset now)
    {
        if (!GuiVeDienTuService.HasDeliverableAddress(booking)) return;
        var text = $"Mã đặt chỗ {booking.MaDatVe}. {detail}";
        var origin = configuration["Auth:PublicBaseUrl"] ?? "http://127.0.0.1:8080";
        var path = $"/ve-cua-toi?pnr={Uri.EscapeDataString(booking.MaDatVe)}";
        var link = Uri.TryCreate(origin, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https"
            ? new Uri(parsed, path).AbsoluteUri : null;
        email.StageMessage(new EmailMessage
        {
            To = VeEmailContent.EmailCuaKhach(booking), Subject = subject,
            TextBody = link is null ? text : $"{text}\n\nVé của tôi: {link}",
            HtmlBody = $"<p>{WebUtility.HtmlEncode(text)}</p>" + (link is null ? ""
                : $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Mở Vé của tôi</a></p>")
        }, now, now.AddDays(2));
    }

    private static string? Limit(string? value, int length)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text[..Math.Min(length, text.Length)];
    }

    private static void Resolve(BienDongDatVe change, DateTimeOffset now, string result)
    {
        change.NgayXuLy = now;
        change.KetQua = result;
    }

    private void AddHistory(DatVe booking, string action, string reason, string actor, DateTimeOffset now, object data) =>
        db.LichSuDatVes.Add(new LichSuDatVe
        {
            DatVeId = booking.Id, HanhDong = action, TrangThaiSau = booking.TrangThai.ToString(),
            LyDo = reason, NguoiThucHien = actor[..Math.Min(150, actor.Length)], ThoiDiem = now,
            DuLieuBoSung = JsonSerializer.Serialize(data)
        });

    public static string DateText(DateTimeOffset departure, DateTimeOffset arrival) =>
        $"{FlightMapping.ToVietnamTime(departure):dd/MM/yyyy HH:mm} → {FlightMapping.ToVietnamTime(arrival):dd/MM/yyyy HH:mm} (UTC+7)";
}
