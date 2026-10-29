namespace AirlineTicketing.DTO;

public sealed record MayBayDTO
{
    public int Id { get; init; }
    public string MaMayBay { get; init; } = string.Empty;
    public string TenMayBay { get; init; } = string.Empty;
    public string HangSanXuat { get; init; } = string.Empty;
    public int SoGhe { get; init; }
    public bool DangHoatDong { get; init; }
}
