namespace AirlineTicketing.DTO;

public sealed record HangHangKhongDTO
{
    public int Id { get; init; }
    public string MaHang { get; init; } = string.Empty;
    public string TenHang { get; init; } = string.Empty;
    public string QuocGia { get; init; } = string.Empty;
    public string? MaIcao { get; init; }
    public bool DangHoatDong { get; init; } = true;
}
