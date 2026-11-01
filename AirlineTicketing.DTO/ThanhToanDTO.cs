using System.ComponentModel.DataAnnotations;

namespace AirlineTicketing.DTO;

public sealed record ThanhToanDTO
{
    public int Id { get; init; }
    public int DatVeId { get; init; }
    public string? MaDatVe { get; init; }
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
    [Range(typeof(decimal), "0.01", "9999999999.99")]
    public decimal SoTien { get; init; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string DonViTienTe { get; init; } = "VND";

    [Required, StringLength(20)]
    public string PhuongThuc { get; init; } = string.Empty;

    [Required, StringLength(50)]
    public string NhaCungCap { get; init; } = "NoiBo";
}

public sealed record KetQuaThanhToanDTO
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ThanhToanId { get; init; }
    public string? MaGiaoDich { get; init; }
    public string? MaDatVe { get; init; }
    public string TrangThai { get; init; } = string.Empty;
    public decimal SoTien { get; init; }
    public string DonViTienTe { get; init; } = "VND";
    public DateTimeOffset? NgayHoanTat { get; init; }
}

public sealed record TaoHoanTienDTO
{
    [Required]
    public int ThanhToanId { get; init; }

    [Range(typeof(decimal), "0.01", "9999999999.99", ErrorMessage = "Số tiền hoàn phải lớn hơn 0")]
    public decimal SoTien { get; init; }

    [StringLength(300, ErrorMessage = "Lý do hoàn tối đa 300 ký tự")]
    public string? LyDo { get; init; }
}
