namespace AirlineTicketing.DTO;

public sealed record HoanTienDTO
{
    public int Id { get; init; }
    public int ThanhToanId { get; init; }
    public decimal SoTien { get; init; }
    public string DonViTienTe { get; init; } = "VND";
    public string TrangThai { get; init; } = string.Empty;
    public string? MaGiaoDich { get; init; }
    public string? LyDo { get; init; }
    public DateTimeOffset NgayTao { get; init; }
    public DateTimeOffset? NgayHoanTat { get; init; }
}
