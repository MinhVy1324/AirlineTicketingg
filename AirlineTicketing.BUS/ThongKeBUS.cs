<<<<<<< Updated upstream
=======
using System.Data;
>>>>>>> Stashed changes
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.BUS;

<<<<<<< Updated upstream
public sealed class ThongKeBUS(AppDbContext dbContext) : IThongKeBUS
{
    public async Task<ThongKeDoanhThuDTO> GetDoanhThuTongQuanAsync(
        DateOnly? tuNgay = null,
        DateOnly? denNgay = null,
        string donViTienTe = "VND",
        CancellationToken cancellationToken = default)
    {
        if (tuNgay.HasValue && denNgay.HasValue && tuNgay.Value > denNgay.Value)
        {
            throw new ArgumentException("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");
        }

        IQueryable<ThanhToan> queryThanhToan = dbContext.ThanhToans
            .Where(x => x.TrangThai == TrangThaiThanhToan.ThanhCong && x.DonViTienTe == donViTienTe);

        IQueryable<HoanTien> queryHoanTien = dbContext.HoanTiens
            .Where(x => x.TrangThai == TrangThaiHoanTien.ThanhCong && x.DonViTienTe == donViTienTe);

        if (tuNgay.HasValue)
        {
            var tuNgayOffset = new DateTimeOffset(tuNgay.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            queryThanhToan = queryThanhToan.Where(x => (x.NgayHoanTat ?? x.NgayTao) >= tuNgayOffset);
            queryHoanTien = queryHoanTien.Where(x => (x.NgayHoanTat ?? x.NgayTao) >= tuNgayOffset);
        }

        if (denNgay.HasValue)
        {
            var denNgayOffset = new DateTimeOffset(denNgay.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            queryThanhToan = queryThanhToan.Where(x => (x.NgayHoanTat ?? x.NgayTao) <= denNgayOffset);
            queryHoanTien = queryHoanTien.Where(x => (x.NgayHoanTat ?? x.NgayTao) <= denNgayOffset);
        }

        var payments = await queryThanhToan.Select(x => x.SoTien).ToListAsync(cancellationToken);
        var refunds = await queryHoanTien.Select(x => x.SoTien).ToListAsync(cancellationToken);

        decimal tongThu = payments.Sum();
        decimal tongHoan = refunds.Sum();
        decimal doanhThuThuan = tongThu - tongHoan;

        return new ThongKeDoanhThuDTO
        {
            TuNgay = tuNgay ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            DenNgay = denNgay ?? DateOnly.FromDateTime(DateTime.UtcNow),
            DonViTienTe = donViTienTe,
            TongTienDaThu = tongThu,
            TongTienDaHoan = tongHoan,
            DoanhThuThuan = doanhThuThuan,
            SoGiaoDichThanhCong = payments.Count,
            SoGiaoDichHoanTien = refunds.Count
        };
    }

    public async Task<IReadOnlyList<DoanhThuTheoNgayDTO>> GetDoanhThuTheoNgayAsync(
        DateOnly tuNgay,
        DateOnly denNgay,
        string donViTienTe = "VND",
        CancellationToken cancellationToken = default)
    {
        if (denNgay < tuNgay)
        {
            throw new ArgumentException("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
        }

        if (denNgay.DayNumber - tuNgay.DayNumber > 366)
        {
            throw new ArgumentException("Báo cáo theo ngày giới hạn trong 367 ngày mỗi lần tra cứu.");
        }

        var tuNgayOffset = new DateTimeOffset(tuNgay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var denNgayOffset = new DateTimeOffset(denNgay.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var payments = await dbContext.ThanhToans
            .Where(x => x.TrangThai == TrangThaiThanhToan.ThanhCong
                     && x.DonViTienTe == donViTienTe
                     && (x.NgayHoanTat ?? x.NgayTao) >= tuNgayOffset
                     && (x.NgayHoanTat ?? x.NgayTao) <= denNgayOffset)
            .Select(x => new { Ngay = DateOnly.FromDateTime((x.NgayHoanTat ?? x.NgayTao).UtcDateTime), x.SoTien })
            .ToListAsync(cancellationToken);

        var refunds = await dbContext.HoanTiens
            .Where(x => x.TrangThai == TrangThaiHoanTien.ThanhCong
                     && x.DonViTienTe == donViTienTe
                     && (x.NgayHoanTat ?? x.NgayTao) >= tuNgayOffset
                     && (x.NgayHoanTat ?? x.NgayTao) <= denNgayOffset)
            .Select(x => new { Ngay = DateOnly.FromDateTime((x.NgayHoanTat ?? x.NgayTao).UtcDateTime), x.SoTien })
            .ToListAsync(cancellationToken);

        var allDates = Enumerable.Range(0, denNgay.DayNumber - tuNgay.DayNumber + 1)
            .Select(offset => tuNgay.AddDays(offset))
            .ToList();

        var result = new List<DoanhThuTheoNgayDTO>();

        foreach (var date in allDates)
        {
            var dayPayments = payments.Where(p => p.Ngay == date).ToList();
            var dayRefunds = refunds.Where(r => r.Ngay == date).ToList();

            decimal thu = dayPayments.Sum(p => p.SoTien);
            decimal hoan = dayRefunds.Sum(r => r.SoTien);

            result.Add(new DoanhThuTheoNgayDTO
            {
                Ngay = date,
                TienDaThu = thu,
                TienDaHoan = hoan,
                DoanhThuThuan = thu - hoan,
                SoGiaoDich = dayPayments.Count + dayRefunds.Count
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<DoanhThuTheoThangDTO>> GetDoanhThuTheoThangAsync(
        int nam,
        string donViTienTe = "VND",
        CancellationToken cancellationToken = default)
    {
        var startOfYear = new DateTimeOffset(new DateTime(nam, 1, 1), TimeSpan.Zero);
        var endOfYear = new DateTimeOffset(new DateTime(nam, 12, 31, 23, 59, 59), TimeSpan.Zero);

        var payments = await dbContext.ThanhToans
            .Where(x => x.TrangThai == TrangThaiThanhToan.ThanhCong
                     && x.DonViTienTe == donViTienTe
                     && (x.NgayHoanTat ?? x.NgayTao) >= startOfYear
                     && (x.NgayHoanTat ?? x.NgayTao) <= endOfYear)
            .Select(x => new { Month = (x.NgayHoanTat ?? x.NgayTao).Month, x.SoTien })
            .ToListAsync(cancellationToken);

        var refunds = await dbContext.HoanTiens
            .Where(x => x.TrangThai == TrangThaiHoanTien.ThanhCong
                     && x.DonViTienTe == donViTienTe
                     && (x.NgayHoanTat ?? x.NgayTao) >= startOfYear
                     && (x.NgayHoanTat ?? x.NgayTao) <= endOfYear)
            .Select(x => new { Month = (x.NgayHoanTat ?? x.NgayTao).Month, x.SoTien })
            .ToListAsync(cancellationToken);

        var list = new List<DoanhThuTheoThangDTO>();

        for (int month = 1; month <= 12; month++)
        {
            var monthPayments = payments.Where(p => p.Month == month).ToList();
            var monthRefunds = refunds.Where(r => r.Month == month).ToList();

            decimal thu = monthPayments.Sum(p => p.SoTien);
            decimal hoan = monthRefunds.Sum(r => r.SoTien);

            list.Add(new DoanhThuTheoThangDTO
            {
                Nam = nam,
                Thang = month,
                TienDaThu = thu,
                TienDaHoan = hoan,
                DoanhThuThuan = thu - hoan,
                SoGiaoDich = monthPayments.Count + monthRefunds.Count
            });
        }

        return list;
    }

    public async Task<IReadOnlyList<DoanhThuTheoChuyenBayDTO>> GetDoanhThuTheoChuyenBayAsync(
        DateOnly? tuNgay = null,
        DateOnly? denNgay = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Ves
            .AsNoTracking()
            .Include(v => v.ChuyenBay)
                .ThenInclude(cb => cb.TuyenBay)
                    .ThenInclude(tb => tb.SanBayDi)
            .Include(v => v.ChuyenBay)
                .ThenInclude(cb => cb.TuyenBay)
                    .ThenInclude(tb => tb.SanBayDen)
            .Where(v => v.TrangThai == TrangThaiVe.DaPhatHanh || v.TrangThai == TrangThaiVe.DaHoanTien);

        if (tuNgay.HasValue)
        {
            var tuOffset = new DateTimeOffset(tuNgay.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(v => v.ChuyenBay.GioKhoiHanh >= tuOffset);
        }

        if (denNgay.HasValue)
        {
            var denOffset = new DateTimeOffset(denNgay.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            query = query.Where(v => v.ChuyenBay.GioKhoiHanh <= denOffset);
        }

        var tickets = await query.ToListAsync(cancellationToken);
        if (tickets.Count == 0)
        {
            return Array.Empty<DoanhThuTheoChuyenBayDTO>();
        }

        var bookingIds = tickets.Select(x => x.DatVeId).Distinct().ToArray();
        var allTickets = await dbContext.Ves.AsNoTracking()
            .Where(x => bookingIds.Contains(x.DatVeId))
            .Select(x => new { x.DatVeId, x.ChuyenBayId, x.GiaVe })
            .ToListAsync(cancellationToken);
        var fareByBooking = allTickets
            .GroupBy(x => x.DatVeId)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.GiaVe));

        var paidByBooking = await dbContext.ThanhToans.AsNoTracking()
            .Where(x => bookingIds.Contains(x.DatVeId)
                && x.TrangThai == TrangThaiThanhToan.ThanhCong
                && x.DonViTienTe == "VND")
            .GroupBy(x => x.DatVeId)
            .Select(group => new { DatVeId = group.Key, Amount = group.Sum(x => x.SoTien) })
            .ToDictionaryAsync(x => x.DatVeId, x => x.Amount, cancellationToken);

        var refundedByBooking = await (
            from refund in dbContext.HoanTiens.AsNoTracking()
            join payment in dbContext.ThanhToans.AsNoTracking() on refund.ThanhToanId equals payment.Id
            where bookingIds.Contains(payment.DatVeId)
                && refund.TrangThai == TrangThaiHoanTien.ThanhCong
                && payment.DonViTienTe == "VND"
                && refund.DonViTienTe == "VND"
            group refund by payment.DatVeId into refundsForBooking
            select new { DatVeId = refundsForBooking.Key, Amount = refundsForBooking.Sum(x => x.SoTien) })
            .ToDictionaryAsync(x => x.DatVeId, x => x.Amount, cancellationToken);

        var result = tickets
            .GroupBy(v => v.ChuyenBayId)
            .Select(g =>
            {
                var flight = g.First().ChuyenBay;
                string tuyenBayText = flight.TuyenBay != null
                    ? $"{flight.TuyenBay.SanBayDi?.MaSanBay ?? "?"} → {flight.TuyenBay.SanBayDen?.MaSanBay ?? "?"}"
                    : "Chưa xác định";

                return new DoanhThuTheoChuyenBayDTO
                {
                    ChuyenBayId = flight.Id,
                    SoHieu = flight.SoHieu,
                    TuyenBay = tuyenBayText,
                    GioKhoiHanh = flight.GioKhoiHanh,
                    SoVeDaBan = g.Count(x => x.TrangThai == TrangThaiVe.DaPhatHanh),
                    DoanhThu = g.GroupBy(x => x.DatVeId).Sum(bookingTickets =>
                    {
                        var bookingId = bookingTickets.Key;
                        var grossFare = fareByBooking.GetValueOrDefault(bookingId);
                        if (grossFare <= 0)
                        {
                            return 0m;
                        }

                        var paid = paidByBooking.GetValueOrDefault(bookingId);
                        var refunded = refundedByBooking.GetValueOrDefault(bookingId);
                        var netPaid = Math.Max(0m, paid - refunded);
                        return netPaid * bookingTickets.Sum(x => x.GiaVe) / grossFare;
                    })
                };
            })
            .OrderByDescending(x => x.DoanhThu)
            .ToList();

        return result;
    }

    public async Task<DashboardStatsDTO> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfToday = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var startOfMonth = new DateTimeOffset(new DateTime(now.Year, now.Month, 1), TimeSpan.Zero);

        var payments = await dbContext.ThanhToans
            .Where(x => x.TrangThai == TrangThaiThanhToan.ThanhCong && x.DonViTienTe == "VND")
            .Select(x => new { x.SoTien, Ngay = x.NgayHoanTat ?? x.NgayTao })
            .ToListAsync(cancellationToken);

        var refunds = await dbContext.HoanTiens
            .Where(x => x.TrangThai == TrangThaiHoanTien.ThanhCong && x.DonViTienTe == "VND")
            .Select(x => new { x.SoTien, Ngay = x.NgayHoanTat ?? x.NgayTao })
            .ToListAsync(cancellationToken);

        decimal tongThu = payments.Sum(x => x.SoTien);
        decimal tongHoan = refunds.Sum(x => x.SoTien);

        decimal thuToday = payments.Where(x => x.Ngay >= startOfToday).Sum(x => x.SoTien);
        decimal hoanToday = refunds.Where(x => x.Ngay >= startOfToday).Sum(x => x.SoTien);

        decimal thuMonth = payments.Where(x => x.Ngay >= startOfMonth).Sum(x => x.SoTien);
        decimal hoanMonth = refunds.Where(x => x.Ngay >= startOfMonth).Sum(x => x.SoTien);

        int tongDatVe = await dbContext.DatVes.CountAsync(cancellationToken);
        int tongVeDaBan = await dbContext.Ves.CountAsync(v => v.TrangThai == TrangThaiVe.DaPhatHanh, cancellationToken);
        int tongKhachHang = await dbContext.KhachHangs.CountAsync(cancellationToken);
        int tongChuyenBay = await dbContext.ChuyenBays.CountAsync(cancellationToken);

        return new DashboardStatsDTO
        {
            TongDoanhThuThuan = tongThu - tongHoan,
            TongTienDaThu = tongThu,
            TongTienDaHoan = tongHoan,
            TongDatVe = tongDatVe,
            TongVeDaBan = tongVeDaBan,
            TongKhachHang = tongKhachHang,
            TongChuyenBay = tongChuyenBay,
            DoanhThuHomNay = thuToday - hoanToday,
            DoanhThuThangNay = thuMonth - hoanMonth
        };
    }
=======
public sealed class ThongKeBUS(AppDbContext db)
{
    public const string DonViBaoCao = "VND";

    public async Task<BaoCaoDoanhThuDTO> LayBaoCaoAsync(DateTimeOffset tuLuc, DateTimeOffset denLuc,
        DateTimeOffset tuLucKyTruoc, DateTimeOffset tuLucXuHuong, DateTimeOffset denLucXuHuong,
        string? hang, string? tuyen, string? soHieu, CancellationToken ct)
    {
        var start = tuLuc.ToUniversalTime();
        var end = denLuc.ToUniversalTime();
        var previous = tuLucKyTruoc.ToUniversalTime();
        var trendStart = tuLucXuHuong.ToUniversalTime();
        var trendEnd = denLucXuHuong.ToUniversalTime();
        var paymentStart = previous < trendStart ? previous : trendStart;
        var paymentEnd = end > trendEnd ? end : trendEnd;
        bool Matches(ChuyenBaoCao flight) =>
            (hang is null || flight.Hang == hang) &&
            (tuyen is null || flight.Tuyen == tuyen) &&
            (soHieu is null || string.Equals(flight.SoHieu, soHieu, StringComparison.OrdinalIgnoreCase));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var payments = await db.ThanhToans.AsNoTracking().AsSplitQuery()
            .Include(p => p.DatVe).ThenInclude(b => b.Ves).ThenInclude(v => v.ChuyenBay).ThenInclude(f => f.TuyenBay).ThenInclude(t => t.SanBayDi)
            .Include(p => p.DatVe).ThenInclude(b => b.Ves).ThenInclude(v => v.ChuyenBay).ThenInclude(f => f.TuyenBay).ThenInclude(t => t.SanBayDen)
            .Include(p => p.DatVe).ThenInclude(b => b.Ves).ThenInclude(v => v.ChuyenBay).ThenInclude(f => f.HangHangKhong)
            .Where(p => p.DonViTienTe == DonViBaoCao && p.TrangThai == TrangThaiThanhToan.ThanhCong
                && (p.NgayHoanTat ?? p.NgayTao) >= paymentStart && (p.NgayHoanTat ?? p.NgayTao) < paymentEnd)
            .ToListAsync(ct);
        var collections = payments.SelectMany(p => Allocate(p.Id, p.DatVe, p.NgayHoanTat ?? p.NgayTao, p.SoTien))
            .Where(p => Matches(p.Chuyen)).ToArray();

        var refunds = await db.HoanTiens.AsNoTracking().AsSplitQuery()
            .Include(r => r.ThanhToan).ThenInclude(p => p.DatVe).ThenInclude(b => b.Ves).ThenInclude(v => v.ChuyenBay).ThenInclude(f => f.TuyenBay).ThenInclude(t => t.SanBayDi)
            .Include(r => r.ThanhToan).ThenInclude(p => p.DatVe).ThenInclude(b => b.Ves).ThenInclude(v => v.ChuyenBay).ThenInclude(f => f.TuyenBay).ThenInclude(t => t.SanBayDen)
            .Include(r => r.ThanhToan).ThenInclude(p => p.DatVe).ThenInclude(b => b.Ves).ThenInclude(v => v.ChuyenBay).ThenInclude(f => f.HangHangKhong)
            .Where(r => r.DonViTienTe == DonViBaoCao && r.ThanhToan.DonViTienTe == DonViBaoCao
                && r.ThanhToan.TrangThai == TrangThaiThanhToan.ThanhCong
                && r.TrangThai == TrangThaiHoanTien.ThanhCong && r.NgayHoanTat >= start && r.NgayHoanTat < end)
            .ToListAsync(ct);
        var returns = refunds.SelectMany(r => Allocate(r.Id, r.ThanhToan.DatVe, r.NgayHoanTat!.Value, r.SoTien))
            .Where(r => Matches(r.Chuyen)).ToArray();

        var issued = await db.Ves.AsNoTracking()
            .Include(v => v.ChuyenBay).ThenInclude(f => f.TuyenBay).ThenInclude(t => t.SanBayDi)
            .Include(v => v.ChuyenBay).ThenInclude(f => f.TuyenBay).ThenInclude(t => t.SanBayDen)
            .Include(v => v.ChuyenBay).ThenInclude(f => f.HangHangKhong)
            .Where(v => v.NgayPhatHanh >= previous && v.NgayPhatHanh < end)
            .ToListAsync(ct);
        var ticketFacts = issued.Select(v => (When: v.NgayPhatHanh!.Value, Flight: Flight(v.ChuyenBay)))
            .Where(v => Matches(v.Flight)).ToArray();

        var bookingCounts = await db.DatVes.AsNoTracking().Where(b => b.NgayDat >= start && b.NgayDat < end)
            .GroupBy(b => b.TrangThai).Select(status => new { Status = status.Key, Count = status.Count() }).ToListAsync(ct);
        await transaction.CommitAsync(ct);

        var activity = collections.Where(p => p.GhiNhanLuc >= start && p.GhiNhanLuc < end)
            .Select(p => new DoanhThuTheoChuyen(p.Chuyen, 0, p.SoTien, 0))
            .Concat(returns.Select(r => new DoanhThuTheoChuyen(r.Chuyen, 0, 0, r.SoTien)))
            .Concat(ticketFacts.Where(v => v.When >= start).Select(v => new DoanhThuTheoChuyen(v.Flight, 1, 0, 0)));
        var byFlight = activity.GroupBy(row => row.Chuyen)
            .Select(group => new DoanhThuTheoChuyen(group.Key, group.Sum(x => x.VeDaXuat), group.Sum(x => x.DaThu), group.Sum(x => x.DaHoan)))
            .OrderBy(row => row.Chuyen.KhoiHanh).ThenBy(row => row.Chuyen.Id).ToArray();
        return new BaoCaoDoanhThuDTO(byFlight,
            collections.Where(p => p.GhiNhanLuc >= trendStart && p.GhiNhanLuc < trendEnd).ToArray(),
            collections.Where(p => p.GhiNhanLuc >= previous && p.GhiNhanLuc < start).Sum(p => p.SoTien),
            ticketFacts.Count(v => v.When < start),
            bookingCounts.Sum(x => x.Count),
            bookingCounts.Where(x => x.Status is TrangThaiDatVe.DaHuy or TrangThaiDatVe.HetHan).Sum(x => x.Count),
            returns.Select(r => r.Id).Distinct().Count());
    }

    private static IEnumerable<KhoanBaoCao> Allocate(int id, DatVe booking, DateTimeOffset at, decimal amount) =>
        JourneyAmounts.Allocate(booking, amount).Select(part => new KhoanBaoCao(id, at, part.Amount, Flight(part.Ticket?.ChuyenBay)));

    private static ChuyenBaoCao Flight(ChuyenBay? flight) =>
        flight is null
            ? new(null, null, null, "Khác", "Khác")
            : new(flight.Id, flight.SoHieu, flight.GioKhoiHanh, TenTuyen(flight.TuyenBay), flight.HangHangKhong?.TenHang ?? "Khác");

    public static string TenTuyen(TuyenBay route) => $"{route.SanBayDi.MaSanBay} ↔ {route.SanBayDen.MaSanBay}";
>>>>>>> Stashed changes
}
