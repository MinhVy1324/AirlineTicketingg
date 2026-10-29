namespace AirlineTicketing.DTO;

public sealed record SanBayDTO
{
    public int Id { get; init; }
    public string MaSanBay { get; init; } = string.Empty;
    public string TenSanBay { get; init; } = string.Empty;
    public string ThanhPho { get; init; } = string.Empty;
    public string QuocGia { get; init; } = string.Empty;
    public bool DangHoatDong { get; init; }
}
