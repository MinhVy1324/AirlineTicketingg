using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.BUS;

public sealed class ThongKeBUS(AppDbContext dbContext) : IThongKeBUS
{
    public async Task<ThongKeDoanhThuDTO> GetDoanhThuTongQuanAsync(
        DateOnly? tuNgay = null,
        DateOnly? denNgay = null,
        string donViTienTe = "VND",
        CancellationToken cancellationToken = default)
    {
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
            .Include(v => v.ChuyenBay)
                .ThenInclude(cb => cb.TuyenBay)
                    .ThenInclude(tb => tb.SanBayDi)
            .Include(v => v.ChuyenBay)
                .ThenInclude(cb => cb.TuyenBay)
                    .ThenInclude(tb => tb.SanBayDen)
            .Where(v => v.TrangThai == TrangThaiVe.DaPhatHanh);

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
                    SoVeDaBan = g.Count(),
                    DoanhThu = g.Sum(x => x.GiaVe)
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
}
