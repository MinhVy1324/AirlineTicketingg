using System.ComponentModel.DataAnnotations;

namespace AirlineTicketing.DTO;

public sealed record ThanhToanDTO
{
    public int Id { get; init; }
    public decimal SoTien { get; init; }
    public string DonViTienTe { get; init; } = "VND";
    public string PhuongThuc { get; init; } = string.Empty;
    public string NhaCungCap { get; init; } = string.Empty;
    public string TrangThai { get; init; } = string.Empty;
    public string? MaGiaoDich { get; init; }
    public DateTimeOffset NgayTao { get; init; }
    public DateTimeOffset? NgayHoanTat { get; init; }
}

public sealed record TaoThanhToanDTO
{
    [Required, StringLength(20)]
    public string PhuongThuc { get; init; } = string.Empty;

    [Required, StringLength(50)]
    public string NhaCungCap { get; init; } = "NoiBo";
}
