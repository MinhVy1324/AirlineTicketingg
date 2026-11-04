using AirlineTicketing.Domain;

namespace AirlineTicketing.DAL.Repositories;

public interface ISanBayDAL
{
    Task<IReadOnlyList<SanBay>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SanBay>> SearchAsync(string keyword, CancellationToken cancellationToken = default);
    Task<SanBay?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<SanBay?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> ExistsCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(SanBay entity, CancellationToken cancellationToken = default);
    void Update(SanBay entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IMayBayDAL
{
    Task<IReadOnlyList<MayBay>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<MayBay?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(MayBay entity, CancellationToken cancellationToken = default);
    void Update(MayBay entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ITuyenBayDAL
{
    Task<IReadOnlyList<TuyenBay>> GetAllWithAirportsAsync(CancellationToken cancellationToken = default);
    Task<TuyenBay?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsPairAsync(int sanBayDiId, int sanBayDenId, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(TuyenBay entity, CancellationToken cancellationToken = default);
    void Update(TuyenBay entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IChuyenBayDAL
{
    Task<IReadOnlyList<ChuyenBay>> SearchAsync(
        int sanBayDiId,
        int sanBayDenId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChuyenBay>> GetAllWithDetailsAsync(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int? tuyenBayId = null,
        CancellationToken cancellationToken = default);

    Task<ChuyenBay?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ChuyenBay?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsSoHieuAsync(string soHieu, DateTimeOffset gioKhoiHanh, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> HasAircraftConflictAsync(int mayBayId, DateTimeOffset gioKhoiHanh, DateTimeOffset gioHaCanh, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, (int DaBan, int DangGiu)>> GetSeatUsageAsync(IEnumerable<int> chuyenBayIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetSeatStatesAsync(int chuyenBayId, CancellationToken cancellationToken = default);
    Task<int> GetMaxSeatUsageForAircraftAsync(int mayBayId, CancellationToken cancellationToken = default);
    Task AddAsync(ChuyenBay entity, CancellationToken cancellationToken = default);
    void Update(ChuyenBay entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
