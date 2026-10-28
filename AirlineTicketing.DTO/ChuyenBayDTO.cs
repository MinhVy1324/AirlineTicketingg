namespace AirlineTicketing.DTO;

public sealed class ChuyenBayDTO
{
    public int Id { get; init; }
    public string SoHieu { get; init; } = string.Empty;
    public int SanBayDiId { get; init; }
    public string TenSanBayDi { get; init; } = string.Empty;
    public int SanBayDenId { get; init; }
    public string TenSanBayDen { get; init; } = string.Empty;
    public DateTime GioKhoiHanh { get; init; }
    public DateTime GioHaCanh { get; init; }
    public decimal GiaCoBan { get; init; }
    public int SoGheTrong { get; init; }
}
