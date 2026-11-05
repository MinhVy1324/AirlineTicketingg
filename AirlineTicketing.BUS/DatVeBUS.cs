using System.Data;
using System.Text.Json;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AirlineTicketing.BUS;

public sealed class DatVeBUS(AppDbContext dbContext, TimeProvider timeProvider, DatVeOptions options)
{
    public async Task<DatVeDTO> TaoDatVeAsync(TaoDatVeDTO request, string? nguoiThucHien = null,
        CancellationToken cancellationToken = default)
    {
        if (request.Ves.Count is < 1 or > 9)
            throw new NgoaiLeNghiepVu("SO_LUONG_HANH_KHACH", "Số hành khách phải từ 1 đến 9.");
        if (string.IsNullOrWhiteSpace(request.NguoiLienHe.HoTen) ||
            string.IsNullOrWhiteSpace(request.NguoiLienHe.Email) ||
            string.IsNullOrWhiteSpace(request.NguoiLienHe.SoDienThoai) ||
            request.Ves.Any(x => string.IsNullOrWhiteSpace(x.SoGhe) ||
                                 string.IsNullOrWhiteSpace(x.HangVe) ||
                                 string.IsNullOrWhiteSpace(x.HanhKhach.HoTen)))
            throw new NgoaiLeNghiepVu("DU_LIEU_KHONG_HOP_LE", "Thông tin liên hệ, hành khách và ghế không đầy đủ.");

        var now = timeProvider.GetUtcNow();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await HetHanCacGiuChoAsync(now, cancellationToken);
            var flightIds = request.Ves.Select(x => x.ChuyenBayId).Distinct().ToArray();
            var flights = await dbContext.ChuyenBays.Include(x => x.MayBay)
                .Where(x => flightIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            if (flights.Count != flightIds.Length) throw new KhongTimThayException("chuyến bay");

            foreach (var group in request.Ves.GroupBy(x => x.ChuyenBayId))
            {
                var flight = flights[group.Key];
                if (flight.TrangThai != TrangThaiChuyenBay.DuKien || flight.GioKhoiHanh <= now)
                    throw new NgoaiLeNghiepVu("CHUYEN_BAY_KHONG_MO_BAN", "Chuyến bay không còn mở bán.");
                if (group.Select(x => ChuanHoaGhe(x.SoGhe)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != group.Count())
                    throw new NgoaiLeNghiepVu("TRUNG_GHE_TRONG_YEU_CAU", "Một ghế không thể cấp cho hai hành khách.");
                var active = await dbContext.Ves.CountAsync(x => x.ChuyenBayId == group.Key &&
                    (x.TrangThai == TrangThaiVe.TamGiu || x.TrangThai == TrangThaiVe.DaPhatHanh), cancellationToken);
                if (active + group.Count() > flight.MayBay.SoGhe)
                    throw new NgoaiLeNghiepVu("CHUYEN_BAY_HET_CHO", "Chuyến bay không còn đủ chỗ.");
            }

            var email = ChuanHoaEmail(request.NguoiLienHe.Email);
            var customer = await dbContext.KhachHangs.FirstOrDefaultAsync(x => x.Email.ToLower() == email, cancellationToken);
            if (customer is null)
            {
                customer = new KhachHang
                {
                    HoTen = request.NguoiLienHe.HoTen.Trim(), Email = email,
                    SoDienThoai = request.NguoiLienHe.SoDienThoai.Trim(), NgayTao = now
                };
                dbContext.KhachHangs.Add(customer);
            }

            var booking = new DatVe
            {
                MaDatVe = TaoMa("DV", 10), KhachHang = customer, NgayDat = now,
                HetHanGiuCho = now.Add(options.ThoiHanGiuCho),
                TrangThai = TrangThaiDatVe.ChoThanhToan, DonViTienTe = "VND"
            };
            foreach (var item in request.Ves)
            {
                var flight = flights[item.ChuyenBayId];
                if (!options.HeSoHangVe.TryGetValue(item.HangVe.Trim(), out var multiplier))
                    throw new NgoaiLeNghiepVu("HANG_VE_KHONG_HOP_LE", $"Hạng vé {item.HangVe} không được mở bán.");
                var passenger = new HanhKhach
                {
                    HoTen = item.HanhKhach.HoTen.Trim(), NgaySinh = item.HanhKhach.NgaySinh,
                    QuocTich = item.HanhKhach.QuocTich?.Trim(),
                    SoHoChieu = string.IsNullOrWhiteSpace(item.HanhKhach.SoHoChieu)
                        ? null : item.HanhKhach.SoHoChieu.Trim().ToUpperInvariant()
                };
                var price = decimal.Round(flight.GiaCoBan * multiplier, 2, MidpointRounding.AwayFromZero);
                booking.Ves.Add(new Ve
                {
                    MaVe = TaoMa("HOLD", 14), ChuyenBayId = item.ChuyenBayId, HanhKhach = passenger,
                    SoGhe = ChuanHoaGhe(item.SoGhe), HangVe = item.HangVe.Trim(), GiaVe = price,
                    TrangThai = TrangThaiVe.TamGiu
                });
                booking.TongTien += price;
            }
            booking.LichSus.Add(TaoLichSu(booking, null, "TaoDatCho", null, booking.TrangThai.ToString(),
                "Tạo đặt chỗ và giữ ghế", nguoiThucHien, now));
            dbContext.DatVes.Add(booking);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await LayChiTietAsync(booking.Id, cancellationToken);
        }
        catch (DbUpdateException exception) when (LaLoiTrungKhoa(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new GheKhongConException(request.Ves.FirstOrDefault()?.SoGhe ?? string.Empty);
        }
    }

    public async Task<DatVeDTO> TraCuuAsync(string maDatVe, string emailXacMinh,
        CancellationToken cancellationToken = default)
    {
        await HetHanDatVeTheoMaAsync(maDatVe, cancellationToken);
        var email = ChuanHoaEmail(emailXacMinh);
        var booking = await BookingQuery().AsNoTracking().FirstOrDefaultAsync(x =>
            x.MaDatVe == maDatVe.Trim().ToUpperInvariant() && x.KhachHang.Email == email, cancellationToken);
        return booking is null ? throw new KhongTimThayException("đặt chỗ") : DatVeMapper.ToDto(booking);
    }

    public async Task<IReadOnlyList<DatVeDTO>> DanhSachQuanLyAsync(string? tuKhoa, int gioiHan = 100,
        CancellationToken cancellationToken = default)
    {
        await HetHanCacDatVeAsync(cancellationToken);
        gioiHan = Math.Clamp(gioiHan, 1, 200);
        var query = BookingQuery().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var keyword = tuKhoa.Trim().ToLowerInvariant();
            query = query.Where(x => x.MaDatVe.ToLower().Contains(keyword) ||
                                     x.KhachHang.Email.ToLower().Contains(keyword) ||
                                     x.KhachHang.HoTen.ToLower().Contains(keyword) ||
                                     x.Ves.Any(v => v.MaVe.ToLower().Contains(keyword)));
        }
        return (await query.OrderByDescending(x => x.NgayDat).Take(gioiHan).ToListAsync(cancellationToken))
            .Select(DatVeMapper.ToDto).ToArray();
    }

    public async Task<KetQuaThanhToanDTO> XuLyKetQuaThanhToanAsync(XuLyThanhToanDTO request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var booking = await BookingQuery().FirstOrDefaultAsync(
            x => x.MaDatVe == request.MaDatVe.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new KhongTimThayException("đặt chỗ");

        var previous = await dbContext.ThanhToans.AsNoTracking().FirstOrDefaultAsync(x =>
            x.NhaCungCap == request.NhaCungCap && x.MaGiaoDich == request.MaGiaoDich, cancellationToken);
        if (previous is not null)
        {
            if (previous.DatVeId != booking.Id)
                throw new NgoaiLeNghiepVu("MA_GIAO_DICH_DA_DUNG", "Mã giao dịch đã được sử dụng.");
            return new KetQuaThanhToanDTO { DaXacNhan = booking.TrangThai == TrangThaiDatVe.DaXacNhan,
                DaXuLyTruocDo = true, DatVe = DatVeMapper.ToDto(booking) };
        }
        if (booking.TrangThai == TrangThaiDatVe.DaXacNhan)
            return new KetQuaThanhToanDTO { DaXacNhan = true, DaXuLyTruocDo = true, DatVe = DatVeMapper.ToDto(booking) };
        if (booking.TrangThai != TrangThaiDatVe.ChoThanhToan)
            throw new NgoaiLeNghiepVu("DAT_VE_KHONG_THE_THANH_TOAN", "Đặt chỗ không còn chờ thanh toán.");
        if (booking.HetHanGiuCho <= now)
        {
            HetHan(booking, now, "Thanh toán sau thời hạn giữ chỗ");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new NgoaiLeNghiepVu("DAT_VE_HET_HAN", "Đặt chỗ đã hết thời hạn thanh toán.");
        }

        booking.ThanhToans.Add(new ThanhToan
        {
            SoTien = booking.TongTien, DonViTienTe = booking.DonViTienTe,
            PhuongThuc = PhuongThucThanhToan.TheNganHang, NhaCungCap = request.NhaCungCap.Trim(),
            MaGiaoDich = request.MaGiaoDich.Trim(),
            TrangThai = request.ThanhCong ? TrangThaiThanhToan.ThanhCong : TrangThaiThanhToan.ThatBai,
            NgayTao = now, NgayHoanTat = now
        });
        if (request.ThanhCong)
        {
            var old = booking.TrangThai;
            booking.TrangThai = TrangThaiDatVe.DaXacNhan;
            foreach (var ticket in booking.Ves.Where(x => x.TrangThai == TrangThaiVe.TamGiu))
            {
                ticket.TrangThai = TrangThaiVe.DaPhatHanh;
                ticket.MaVe = TaoMa("VE", 14);
                ticket.NgayPhatHanh = now;
            }
            booking.LichSus.Add(TaoLichSu(booking, null, "ThanhToanThanhCong", old.ToString(),
                booking.TrangThai.ToString(), "Đã xác minh thanh toán và phát hành vé", null, now));
        }
        else
        {
            booking.LichSus.Add(TaoLichSu(booking, null, "ThanhToanThatBai", booking.TrangThai.ToString(),
                booking.TrangThai.ToString(), "Cổng thanh toán trả kết quả thất bại", null, now));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new KetQuaThanhToanDTO { DaXacNhan = request.ThanhCong,
            DatVe = await LayChiTietAsync(booking.Id, cancellationToken) };
    }

    public async Task<int> HetHanCacDatVeAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var count = await HetHanCacGiuChoAsync(timeProvider.GetUtcNow(), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return count;
    }

    private async Task HetHanDatVeTheoMaAsync(string code, CancellationToken cancellationToken)
    {
        var booking = await dbContext.DatVes.Include(x => x.Ves).Include(x => x.LichSus)
            .FirstOrDefaultAsync(x => x.MaDatVe == code.Trim().ToUpperInvariant(), cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (booking is not null && booking.TrangThai == TrangThaiDatVe.ChoThanhToan && booking.HetHanGiuCho <= now)
        {
            HetHan(booking, now, "Hết thời hạn giữ chỗ");
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<int> HetHanCacGiuChoAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expired = await dbContext.DatVes.Include(x => x.Ves).Include(x => x.LichSus)
            .Where(x => x.TrangThai == TrangThaiDatVe.ChoThanhToan && x.HetHanGiuCho <= now)
            .ToListAsync(cancellationToken);
        foreach (var booking in expired) HetHan(booking, now, "Hết thời hạn giữ chỗ");
        if (expired.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    private static void HetHan(DatVe booking, DateTimeOffset now, string reason)
    {
        var old = booking.TrangThai;
        booking.TrangThai = TrangThaiDatVe.HetHan;
        foreach (var ticket in booking.Ves.Where(x => x.TrangThai == TrangThaiVe.TamGiu))
        {
            ticket.TrangThai = TrangThaiVe.DaHuy; ticket.NgayHuy = now; ticket.LyDoHuy = reason;
        }
        booking.LichSus.Add(TaoLichSu(booking, null, "HetHanGiuCho", old.ToString(), booking.TrangThai.ToString(),
            reason, "system", now));
    }

    private IQueryable<DatVe> BookingQuery() => dbContext.DatVes.Include(x => x.KhachHang)
        .Include(x => x.Ves).ThenInclude(x => x.HanhKhach)
        .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
        .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
        .Include(x => x.ThanhToans).Include(x => x.LichSus);

    private async Task<DatVeDTO> LayChiTietAsync(int id, CancellationToken cancellationToken) =>
        DatVeMapper.ToDto(await BookingQuery().AsNoTracking().SingleAsync(x => x.Id == id, cancellationToken));

    internal static LichSuDatVe TaoLichSu(DatVe booking, Ve? ticket, string action, string? before,
        string after, string? reason, string? actor, DateTimeOffset at) => new()
    {
        DatVe = booking, Ve = ticket, HanhDong = action, TrangThaiTruoc = before, TrangThaiSau = after,
        LyDo = reason, NguoiThucHien = actor, ThoiDiem = at,
        DuLieuBoSung = JsonSerializer.Serialize(new { booking.MaDatVe, ticket?.MaVe })
    };

    internal static string TaoMa(string prefix, int length) => $"{prefix}{Guid.NewGuid():N}"[..length].ToUpperInvariant();
    private static string ChuanHoaGhe(string value) => value.Trim().ToUpperInvariant();
    private static string ChuanHoaEmail(string value) => value.Trim().ToLowerInvariant();
    private static bool LaLoiTrungKhoa(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Ves_ChuyenBayId_SoGhe"
        };
}
