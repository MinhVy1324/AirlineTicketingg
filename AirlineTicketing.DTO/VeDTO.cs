namespace AirlineTicketing.DTO;

public sealed record VeDTO
{
    public int Id { get; init; }
    public string MaVe { get; init; } = string.Empty;
    public string MaDatVe { get; init; } = string.Empty;
    public string SoHieuChuyenBay { get; init; } = string.Empty;
    public DateTimeOffset GioKhoiHanh { get; init; }
    public string SoGhe { get; init; } = string.Empty;
    public string HangVe { get; init; } = string.Empty;
    public decimal GiaVe { get; init; }
    public string TrangThai { get; init; } = string.Empty;
    public HanhKhachDTO HanhKhach { get; init; } = new();
}
