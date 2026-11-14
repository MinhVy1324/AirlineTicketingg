using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.DAL.Repositories;

public sealed class SanBayDAL(AppDbContext dbContext) : ISanBayDAL
{
    public async Task<IReadOnlyList<SanBay>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SanBays.AsNoTracking()
            .OrderBy(x => x.MaSanBay)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SanBay>> SearchAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var value = keyword.Trim();
        if (value.Length == 0)
        {
            return Array.Empty<SanBay>();
        }

        var upper = value.ToUpperInvariant();
        return await dbContext.SanBays.AsNoTracking()
            .Where(x => x.MaSanBay.ToUpper() == upper
                || x.TenSanBay.ToUpper().Contains(upper)
                || x.ThanhPho.ToUpper().Contains(upper))
            .OrderBy(x => x.MaSanBay)
            .ToListAsync(cancellationToken);
    }

    public Task<SanBay?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.SanBays.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<SanBay?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var upper = code.Trim().ToUpperInvariant();
        return dbContext.SanBays.AsNoTracking().FirstOrDefaultAsync(x => x.MaSanBay == upper, cancellationToken);
    }

    public Task<bool> ExistsCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var upper = code.Trim().ToUpperInvariant();
        return dbContext.SanBays.AnyAsync(
            x => x.MaSanBay == upper && (excludeId == null || x.Id != excludeId),
            cancellationToken);
    }

    public Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.TuyenBays.AnyAsync(x => x.SanBayDiId == id || x.SanBayDenId == id, cancellationToken);

    public async Task AddAsync(SanBay entity, CancellationToken cancellationToken = default) =>
        await dbContext.SanBays.AddAsync(entity, cancellationToken);

    public void Update(SanBay entity) => dbContext.SanBays.Update(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class MayBayDAL(AppDbContext dbContext) : IMayBayDAL
{
    public async Task<IReadOnlyList<MayBay>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.MayBays.AsNoTracking()
            .OrderBy(x => x.MaMayBay)
            .ToListAsync(cancellationToken);

    public Task<MayBay?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.MayBays.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> ExistsCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var upper = code.Trim().ToUpperInvariant();
        return dbContext.MayBays.AnyAsync(
            x => x.MaMayBay == upper && (excludeId == null || x.Id != excludeId),
            cancellationToken);
    }

    public Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.ChuyenBays.AnyAsync(x => x.MayBayId == id, cancellationToken);

    public async Task AddAsync(MayBay entity, CancellationToken cancellationToken = default) =>
        await dbContext.MayBays.AddAsync(entity, cancellationToken);

    public void Update(MayBay entity) => dbContext.MayBays.Update(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class HangHangKhongDAL(AppDbContext dbContext) : IHangHangKhongDAL
{
    public async Task<IReadOnlyList<HangHangKhong>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.HangHangKhongS.AsNoTracking()
            .OrderBy(x => x.MaHang)
            .ToListAsync(cancellationToken);

    public Task<HangHangKhong?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.HangHangKhongS.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<HangHangKhong?> FindByCodeAsync(string maHang, CancellationToken cancellationToken = default)
    {
        var upper = maHang.Trim().ToUpperInvariant();
        return dbContext.HangHangKhongS.AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaHang == upper, cancellationToken);
    }
}

public sealed class TuyenBayDAL(AppDbContext dbContext) : ITuyenBayDAL
{
    public async Task<IReadOnlyList<TuyenBay>> GetAllWithAirportsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.TuyenBays.AsNoTracking()
            .Include(x => x.SanBayDi)
            .Include(x => x.SanBayDen)
            .OrderBy(x => x.SanBayDi.MaSanBay)
            .ThenBy(x => x.SanBayDen.MaSanBay)
            .ToListAsync(cancellationToken);

    public Task<TuyenBay?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.TuyenBays
            .Include(x => x.SanBayDi)
            .Include(x => x.SanBayDen)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> ExistsPairAsync(int sanBayDiId, int sanBayDenId, int? excludeId = null, CancellationToken cancellationToken = default) =>
        dbContext.TuyenBays.AnyAsync(
            x => x.SanBayDiId == sanBayDiId
                && x.SanBayDenId == sanBayDenId
                && (excludeId == null || x.Id != excludeId),
            cancellationToken);

    public Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.ChuyenBays.AnyAsync(x => x.TuyenBayId == id, cancellationToken);

    public async Task AddAsync(TuyenBay entity, CancellationToken cancellationToken = default) =>
        await dbContext.TuyenBays.AddAsync(entity, cancellationToken);

    public void Update(TuyenBay entity) => dbContext.TuyenBays.Update(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
