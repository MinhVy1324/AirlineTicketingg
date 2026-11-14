using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.DAL.Repositories;

public sealed class ChuyenBayDAL(AppDbContext dbContext) : IChuyenBayDAL
{
    public async Task<IReadOnlyList<ChuyenBay>> SearchAsync(
        int sanBayDiId,
        int sanBayDenId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default) =>
        await BaseQuery()
            .Where(x => x.TuyenBay.SanBayDiId == sanBayDiId
                && x.TuyenBay.SanBayDenId == sanBayDenId
                && x.GioKhoiHanh >= fromUtc
                && x.GioKhoiHanh < toUtc
                && x.TrangThai == TrangThaiChuyenBay.DuKien
                && x.TuyenBay.DangKhaiThac
                && x.MayBay.DangHoatDong
                && x.TuyenBay.SanBayDi.DangHoatDong
                && x.TuyenBay.SanBayDen.DangHoatDong)
            .OrderBy(x => x.GioKhoiHanh)
            .ThenBy(x => x.GiaCoBan)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ChuyenBay>> GetAllWithDetailsAsync(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int? tuyenBayId = null,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery();

        if (fromUtc is not null)
        {
            query = query.Where(x => x.GioKhoiHanh >= fromUtc);
        }

        if (toUtc is not null)
        {
            query = query.Where(x => x.GioKhoiHanh < toUtc);
        }

        if (tuyenBayId is not null)
        {
            query = query.Where(x => x.TuyenBayId == tuyenBayId);
        }

        return await query
            .OrderBy(x => x.GioKhoiHanh)
            .ThenBy(x => x.SoHieu)
            .ToListAsync(cancellationToken);
    }

    public Task<ChuyenBay?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.ChuyenBays.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<ChuyenBay?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default) =>
        BaseQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> ExistsSoHieuAsync(
        string soHieu,
        DateTimeOffset gioKhoiHanh,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var code = soHieu.Trim().ToUpperInvariant();
        return dbContext.ChuyenBays.AnyAsync(
            x => x.SoHieu == code
                && x.GioKhoiHanh == gioKhoiHanh
                && (excludeId == null || x.Id != excludeId),
            cancellationToken);
    }

    public Task<bool> HasAircraftConflictAsync(
        int mayBayId,
        DateTimeOffset gioKhoiHanh,
        DateTimeOffset gioHaCanh,
        int? excludeId = null,
        CancellationToken cancellationToken = default) =>
        dbContext.ChuyenBays.AnyAsync(
            x => x.MayBayId == mayBayId
                && x.TrangThai != TrangThaiChuyenBay.BiHuy
                && x.TrangThai != TrangThaiChuyenBay.HoanTat
                && x.GioKhoiHanh < gioHaCanh
                && x.GioHaCanh > gioKhoiHanh
                && (excludeId == null || x.Id != excludeId),
            cancellationToken);

    public async Task<IReadOnlyDictionary<int, (int DaBan, int DangGiu)>> GetSeatUsageAsync(
        IEnumerable<int> chuyenBayIds,
        CancellationToken cancellationToken = default)
    {
        var ids = chuyenBayIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, (int DaBan, int DangGiu)>();
        }

        var rows = await dbContext.Ves.AsNoTracking()
            .Where(x => ids.Contains(x.ChuyenBayId)
                && (x.TrangThai == TrangThaiVe.DaPhatHanh || x.TrangThai == TrangThaiVe.TamGiu))
            .GroupBy(x => new { x.ChuyenBayId, x.TrangThai })
            .Select(group => new
            {
                group.Key.ChuyenBayId,
                group.Key.TrangThai,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.ChuyenBayId)
            .ToDictionary(
                group => group.Key,
                group => (
                    DaBan: group.Where(x => x.TrangThai == TrangThaiVe.DaPhatHanh).Sum(x => x.Count),
                    DangGiu: group.Where(x => x.TrangThai == TrangThaiVe.TamGiu).Sum(x => x.Count)));
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSeatStatesAsync(
        int chuyenBayId,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Ves.AsNoTracking()
            .Where(x => x.ChuyenBayId == chuyenBayId
                && (x.TrangThai == TrangThaiVe.DaPhatHanh || x.TrangThai == TrangThaiVe.TamGiu))
            .Select(x => new { x.SoGhe, x.TrangThai })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var state = row.TrangThai == TrangThaiVe.DaPhatHanh ? "DaBan" : "DangGiu";
            result[row.SoGhe] = state;
        }

        return result;
    }

    public async Task<int> GetMaxSeatUsageForAircraftAsync(int mayBayId, CancellationToken cancellationToken = default)
    {
        var usage = await dbContext.Ves.AsNoTracking()
            .Where(x => x.ChuyenBay.MayBayId == mayBayId
                && (x.TrangThai == TrangThaiVe.DaPhatHanh || x.TrangThai == TrangThaiVe.TamGiu))
            .GroupBy(x => x.ChuyenBayId)
            .Select(group => group.Count())
            .ToListAsync(cancellationToken);

        return usage.Count == 0 ? 0 : usage.Max();
    }

    public async Task AddAsync(ChuyenBay entity, CancellationToken cancellationToken = default) =>
        await dbContext.ChuyenBays.AddAsync(entity, cancellationToken);

    public void Update(ChuyenBay entity) => dbContext.ChuyenBays.Update(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<ChuyenBay> BaseQuery() =>
        dbContext.ChuyenBays.AsNoTracking()
            .Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .Include(x => x.MayBay);
}
