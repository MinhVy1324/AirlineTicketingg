using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.ViewModels;
using AirlineTicketing.Web.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Infrastructure;

public sealed class BookingValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}

/// <summary>Giữ chỗ và thanh toán dùng cùng một đặt chỗ, cùng ảnh chụp giá.</summary>
public sealed class BookingCheckoutService(
    AppDbContext db, CauHinhHeThongBUS policies, TimeProvider clock, BookingPromotion promotion)
{
    public async Task<DatVe> HoldAsync(int flightId, string seatCode, PassengerFormModel passenger,
        string baggageCode, int? previousBookingId, CancellationToken ct, int? registeredCustomerId = null)
    {
        var selection = new JourneySelection(flightId, null, new PassengerParty(), JourneyRules.Economy.Code);
        var form = new JourneyBookingForm
        {
            ContactName = passenger.FullName, Email = passenger.Email, Phone = passenger.Phone,
            People = [new JourneyPassengerForm { Person = passenger, Seats = [seatCode], Baggage = [baggageCode] }]
        };
        try { return await HoldJourneyAsync(selection, form, previousBookingId, ct, registeredCustomerId); }
        catch (BookingValidationException e)
        {
            var field = e.Field.StartsWith("person-0-") ? e.Field["person-0-".Length..]
                : e.Field.StartsWith("seat-") ? "seat" : e.Field.StartsWith("baggage-") ? "baggage" : e.Field;
            throw new BookingValidationException(field, e.Message);
        }
    }

    public async Task<DatVe> HoldJourneyAsync(JourneySelection selection, JourneyBookingForm form,
        int? previousBookingId, CancellationToken ct, int? registeredCustomerId = null)
    {
        JourneyValidation.Normalize(form);
        selection.Party.Validate();
        var cabin = JourneyRules.Cabin(selection.Cabin);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(db, ct);
        var now = clock.GetUtcNow();
        var ids = selection.FlightIds.ToArray();
        var loaded = await db.ChuyenBays.Include(x => x.MayBay)
            .Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        var flights = ids.Select(id => loaded.FirstOrDefault(x => x.Id == id)).Where(x => x is not null).Cast<ChuyenBay>().ToArray();
        var errors = JourneyValidation.Validate(selection, form, flights, now);
        if (errors.Count > 0) throw new BookingValidationException(errors.First().Key, errors.First().Value);
        var types = selection.Party.Types;
        var previous = previousBookingId.HasValue
            ? await db.DatVes.Include(x => x.Ves).Include(x => x.LichSus).Include(x => x.ThanhToans)
                .FirstOrDefaultAsync(x => x.Id == previousBookingId
                    && (registeredCustomerId == null || x.KhachHangId == registeredCustomerId), ct) : null;
        var ownHold = previous is { TrangThai: TrangThaiDatVe.ChoThanhToan } && previous.HetHanGiuCho > now;
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { selection, form }))));
        var fingerprintJson = JsonSerializer.Serialize(new { fingerprint });
        if (ownHold && previous!.LichSus.Any(x => x.HanhDong == "TaoDatCho" && MatchesFingerprint(x.DuLieuBoSung, fingerprint)))
        {
            await transaction.CommitAsync(ct);
            return previous;
        }
        if (ownHold && previous!.ThanhToans.Any(x => x.TrangThai == TrangThaiThanhToan.ChoXuLy))
            throw new BookingValidationException("journey", "Đơn đang chờ đối soát chuyển khoản. Liên hệ hỗ trợ để thay đổi đơn.");

        var fees = new decimal[selection.LegCount, form.People.Count];
        for (var leg = 0; leg < flights.Length; leg++)
        {
            var flight = flights[leg];
            var map = await FlightServices.ChuyenBay(db).LaySoDoGheAsync(flight.Id, ct);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < form.People.Count; i++)
            {
                if (types[i] == LoaiHanhKhach.EmBe) continue;
                var selected = form.People[i].Seats[leg];
                var seat = map.Ghes.FirstOrDefault(x => x.SoGhe == selected);
                var heldByUs = ownHold && previous!.Ves.Any(x => x.ChuyenBayId == flight.Id && x.SoGhe == selected);
                if (seat is null || (seat.TrangThai != "Trong" && !heldByUs) || !used.Add(selected))
                    throw new BookingValidationException($"seat-{i}-{leg}", "Ghế đã có khách hoặc được chọn trùng trong nhóm. Vui lòng chọn lại.");
                fees[leg, i] = seat.PhuThu;
            }
        }

        var persons = new List<HanhKhach>();
        for (var i = 0; i < form.People.Count; i++)
        {
            var person = form.People[i].Person;
            var identity = person.IdentityNumber.Length == 0 ? null : person.IdentityNumber;
            var dob = ParseBirthDate(person);
            var duplicateQuery = db.Ves.Where(x => ids.Contains(x.ChuyenBayId)
                && x.DatVeId != (ownHold ? previousBookingId ?? 0 : 0)
                && (x.TrangThai == TrangThaiVe.TamGiu || x.TrangThai == TrangThaiVe.DaPhatHanh));
            var duplicate = identity is not null
                ? await duplicateQuery.AnyAsync(x => x.HanhKhach.SoHoChieu == identity, ct)
                : await duplicateQuery.AnyAsync(x => x.HanhKhach.SoHoChieu == null && x.HanhKhach.HoTen == person.FullName
                    && x.HanhKhach.NgaySinh == dob && x.DatVe.EmailLienHe == form.Email, ct);
            if (duplicate) throw new BookingValidationException($"person-{i}-identityNumber",
                "Hành khách đã có vé hoặc đang giữ chỗ trên một chuyến trong hành trình.");
            var existing = identity is not null ? await db.HanhKhachs.FirstOrDefaultAsync(x => x.SoHoChieu == identity, ct) : null;
            if (existing is not null && (!SameName(existing.HoTen, person.FullName)
                || (existing.NgaySinh.HasValue && existing.NgaySinh != dob)))
                throw new BookingValidationException($"person-{i}-identityNumber",
                    "Giấy tờ đã được sử dụng với thông tin khác. Kiểm tra họ tên và ngày sinh.");
            persons.Add(existing ?? new HanhKhach
            {
                HoTen = person.FullName, SoHoChieu = identity, NgaySinh = dob,
                QuocTich = person.Nationality, Email = form.Email, SoDienThoai = form.Phone,
                LoaiHanhKhach = types[i],
                GioiTinh = person.Gender switch { "Nam" => GioiTinh.Nam, "Nữ" => GioiTinh.Nu, _ => GioiTinh.Khac }
            });
        }

        if (previous is { TrangThai: TrangThaiDatVe.ChoThanhToan })
        {
            previous.TrangThai = previous.HetHanGiuCho <= now ? TrangThaiDatVe.HetHan : TrangThaiDatVe.DaHuy;
            foreach (var ticket in previous.Ves.Where(x => x.TrangThai == TrangThaiVe.TamGiu))
            {
                ticket.TrangThai = TrangThaiVe.DaHuy; ticket.NgayHuy = now;
                ticket.LyDoHuy = "Thay đổi hành trình, hành khách hoặc dịch vụ";
            }
            previous.LichSus.Add(History(previous, "ThayDoiDatCho", "Thay thế toàn bộ nhóm chỗ trong cùng giao dịch", now));
            await db.SaveChangesAsync(ct);
        }
        var policy = await policies.LayChinhSachAsync(ct);
        var customer = registeredCustomerId is int ownerId
            ? await db.KhachHangs.SingleAsync(x => x.Id == ownerId, ct)
            : await db.KhachHangs.FirstOrDefaultAsync(x => x.Email.ToLower() == form.Email
                && !x.Email.EndsWith(TaiKhoan.InternalEmailSuffix), ct)
                ?? new KhachHang { HoTen = form.ContactName, Email = form.Email, SoDienThoai = form.Phone, NgayTao = now };
        var booking = new DatVe
        {
            MaDatVe = NewPnr(), KhachHang = customer, NgayDat = now,
            HetHanGiuCho = ownHold ? previous!.HetHanGiuCho : now.Add(policy.HoldDuration),
            TrangThai = TrangThaiDatVe.ChoThanhToan, DonViTienTe = "VND",
            TenLienHe = form.ContactName, EmailLienHe = form.Email, SoDienThoaiLienHe = form.Phone
        };
        for (var leg = 0; leg < flights.Length; leg++)
        {
            for (var i = 0; i < persons.Count; i++)
            {
                var infant = types[i] == LoaiHanhKhach.EmBe;
                var bag = BaggageCatalog.Find(form.People[i].Baggage[leg])!;
                var fare = JourneyRules.BaseFare(flights[leg].GiaCoBan, cabin, types[i]);
                var tax = JourneyRules.Tax(policy, types[i]);
                var service = JourneyRules.ServiceFee(policy, types[i]);
                booking.Ves.Add(new Ve
                {
                    MaVe = "HOLD" + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant(),
                    ChuyenBay = flights[leg], HanhKhach = persons[i], HangVe = cabin.Code,
                    SoGhe = infant ? "" : form.People[i].Seats[leg], GiaVe = fare,
                    LoaiHanhKhach = types[i], NguoiDiKem = infant ? persons[form.People[i].CompanionIndex!.Value] : null,
                    TrangThai = TrangThaiVe.TamGiu, ThuePhi = tax, PhiDichVu = service,
                    PhiHanhLy = bag.Price, PhiChoNgoi = fees[leg, i],
                    HanhLyXachTayKg = infant ? 0 : BaggageCatalog.CabinKg,
                    HanhLyKyGuiKg = infant ? 0 : BaggageCatalog.IncludedCheckedKg + bag.ExtraKg,
                    PhiDoi = policy.ChangeFee, PhiHoan = policy.RefundFee
                });
                booking.ThuePhi += tax; booking.PhiDichVu += service; booking.PhiHanhLy += bag.Price;
                booking.TongTien += fare + tax + service + bag.Price + fees[leg, i];
            }
        }
        var history = History(booking, "TaoDatCho", "Giữ toàn bộ hành trình và chốt giá cho nhóm", now);
        history.DuLieuBoSung = fingerprintJson;
        booking.LichSus.Add(history);
        db.DatVes.Add(booking);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return booking;
    }
    private static bool MatchesFingerprint(string? json, string expected)
    {
        if (string.IsNullOrEmpty(json)) return false;
        using var data = JsonDocument.Parse(json);
        return data.RootElement.TryGetProperty("fingerprint", out var value) && value.GetString() == expected;
    }

    public async Task ApplyPromotionAsync(int bookingId, string? code, bool remove, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(db, ct);
        var booking = await db.DatVes.Include(x => x.ThanhToans).SingleAsync(x => x.Id == bookingId, ct);
        var now = clock.GetUtcNow();
        if (booking.TrangThai != TrangThaiDatVe.ChoThanhToan || booking.HetHanGiuCho <= now
            || booking.ThanhToans.Any(x => x.TrangThai != TrangThaiThanhToan.ThatBai))
            throw new BookingValidationException("promotion", "Chỉ có thể sửa mã trên đặt chỗ còn hạn và chưa tạo giao dịch.");

        var subtotal = booking.TongTien + booking.GiamGia;
        var discount = 0m;
        if (!remove)
        {
            if (!string.Equals(code?.Trim(), BookingPromotion.Code, StringComparison.OrdinalIgnoreCase))
                throw new BookingValidationException("promotion", "Mã ưu đãi không hợp lệ.");
            if (!promotion.IsActive && booking.MaKhuyenMai != BookingPromotion.Code)
                throw new BookingValidationException("promotion", "Mã ưu đãi đã hết hạn.");
            discount = BookingPromotion.Discount(subtotal, booking.PhiDichVu);
            if (discount <= 0)
                throw new BookingValidationException("promotion", "Đơn cần có phí dịch vụ và tổng trước ưu đãi từ 3.000.000 ₫.");
        }

        if (discount != booking.GiamGia)
        {
            booking.GiamGia = discount;
            booking.MaKhuyenMai = remove ? null : BookingPromotion.Code;
            booking.TongTien = subtotal - discount;
            db.LichSuDatVes.Add(History(booking, remove ? "XoaKhuyenMai" : "ApDungKhuyenMai",
                BookingPromotion.Code, now));
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    public async Task<DatVe> CompleteAsync(int bookingId, bool transfer, string brand, string last4,
        decimal? expectedTotal, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(db, ct);
        var booking = await db.DatVes.Include(x => x.Ves).ThenInclude(x => x.ChuyenBay)
            .Include(x => x.ThanhToans).Include(x => x.LichSus).SingleAsync(x => x.Id == bookingId, ct);
        if (booking.TrangThai == TrangThaiDatVe.DaXacNhan)
        {
            await transaction.CommitAsync(ct);
            return booking;
        }
        var now = clock.GetUtcNow();
        if (!CanIssue(booking, now))
            throw new BookingValidationException("hold", "Đặt chỗ đã hết hạn hoặc chuyến bay không còn mở bán. Vui lòng đặt lại.");

        var pending = booking.ThanhToans.FirstOrDefault(x => x.TrangThai == TrangThaiThanhToan.ChoXuLy);
        if (pending is not null)
        {
            if (!transfer || pending.NhaCungCap != VietQr.Provider)
                throw new BookingValidationException("payment", "Đơn đang có giao dịch chờ xác nhận. Vui lòng tiếp tục với phương thức đã chọn.");
            await transaction.CommitAsync(ct);
            return booking;
        }
        if (expectedTotal.HasValue ? expectedTotal.Value != booking.TongTien : booking.GiamGia > 0)
            throw new BookingValidationException("promotion", "Tổng tiền đã thay đổi. Vui lòng kiểm tra lại trước khi thanh toán.");

        booking.ThanhToans.Add(new ThanhToan
        {
            SoTien = booking.TongTien, DonViTienTe = "VND",
            PhuongThuc = transfer ? PhuongThucThanhToan.ChuyenKhoan : PhuongThucThanhToan.TheNganHang,
            NhaCungCap = transfer ? VietQr.Provider : $"Sandbox {brand} •••• {last4}",
            TrangThai = transfer ? TrangThaiThanhToan.ChoXuLy : TrangThaiThanhToan.ThanhCong,
            MaGiaoDich = (transfer ? "QR" : "DEMO") + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant(),
            NgayTao = now, NgayHoanTat = transfer ? null : now
        });
        booking.GhiChu = transfer ? "Chuyển khoản demo, chờ nhân viên đối soát" : $"Thanh toán sandbox {brand} •••• {last4}";
        if (!transfer)
        {
            IssueTickets(booking, now);
            EmailDeliveryQueue.StageTicket(db, booking.Id, "Thanh toán demo", now);
        }
        booking.LichSus.Add(History(booking, transfer ? "ChoChuyenKhoan" : "ThanhToanSandbox",
            transfer ? "Chờ nhân viên xác nhận chuyển khoản" : "Thanh toán mô phỏng và phát hành vé", now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return booking;
    }

    internal static bool CanIssue(DatVe booking, DateTimeOffset now) =>
        booking.TrangThai == TrangThaiDatVe.ChoThanhToan && booking.HetHanGiuCho > now
        && booking.Ves.Count > 0 && booking.Ves.All(ticket => ticket.TrangThai == TrangThaiVe.TamGiu
            && ticket.ChuyenBay.TrangThai == TrangThaiChuyenBay.DuKien && ticket.ChuyenBay.GioKhoiHanh > now);

    internal static void IssueTickets(DatVe booking, DateTimeOffset now)
    {
        booking.TrangThai = TrangThaiDatVe.DaXacNhan;
        booking.NgayXacNhan = now;
        foreach (var ticket in booking.Ves)
        {
            ticket.TrangThai = TrangThaiVe.DaPhatHanh;
            ticket.MaVe = "738" + Convert.ToHexString(RandomNumberGenerator.GetBytes(10));
            ticket.NgayPhatHanh = now;
        }
    }

    internal static Task<int> LockAsync(AppDbContext db, CancellationToken ct) => db.Database.IsNpgsql()
        // Cùng khóa với job hết hạn để việc thanh toán và trả ghế không xen kẽ.
        ? db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(918273645)", ct)
        : Task.FromResult(0);

    private static DateOnly ParseBirthDate(PassengerFormModel person) =>
        FlightMapping.TryParseDate(person.DateOfBirth, out var date) ? date : throw new InvalidOperationException();
    private static bool SameName(string left, string right) =>
        string.Equals(string.Join(' ', left.Split(' ', StringSplitOptions.RemoveEmptyEntries)),
            string.Join(' ', right.Split(' ', StringSplitOptions.RemoveEmptyEntries)), StringComparison.OrdinalIgnoreCase);
    private static string NewPnr()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 8).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
    }
    private static LichSuDatVe History(DatVe booking, string action, string reason, DateTimeOffset at) => new()
    {
        DatVe = booking, HanhDong = action, LyDo = reason, ThoiDiem = at,
        TrangThaiSau = booking.TrangThai.ToString(), NguoiThucHien = "Khách đặt vé"
    };
}
