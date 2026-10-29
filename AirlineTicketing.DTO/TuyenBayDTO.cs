namespace AirlineTicketing.DTO;

public sealed record TuyenBayDTO
{
    public int Id { get; init; }
    public int SanBayDiId { get; init; }
    public SanBayDTO SanBayDi { get; init; } = new();
    public int SanBayDenId { get; init; }
    public SanBayDTO SanBayDen { get; init; } = new();
    public int? ThoiLuongDuKienPhut { get; init; }
    public bool DangKhaiThac { get; init; }
}
