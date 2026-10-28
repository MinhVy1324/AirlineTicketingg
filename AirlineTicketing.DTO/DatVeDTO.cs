namespace AirlineTicketing.DTO;

public sealed class DatVeDTO
{
    public int Id { get; init; }
    public string MaDatVe { get; init; } = string.Empty;
    public KhachHangDTO KhachHang { get; init; } = new();
    public DateTime NgayDat { get; init; }
    public string TrangThai { get; init; } = string.Empty;
    public IReadOnlyList<VeDTO> Ve { get; init; } = Array.Empty<VeDTO>();
}
