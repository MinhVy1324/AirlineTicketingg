using System.ComponentModel.DataAnnotations;

namespace AirlineTicketing.DTO;

/// <summary>Dữ liệu đầu vào khi nhân viên khai thác tạo một chuyến bay mới.</summary>
public sealed record TaoChuyenBayDTO
{
    [Required, StringLength(20)]
    public string SoHieu { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int TuyenBayId { get; init; }

    [Range(1, int.MaxValue)]
    public int MayBayId { get; init; }

    public DateTimeOffset GioKhoiHanh { get; init; }
    public DateTimeOffset GioHaCanh { get; init; }

    [Range(typeof(decimal), "0", "9999999999.99")]
    public decimal GiaCoBan { get; init; }

    [Required, StringLength(20)]
    public string TrangThai { get; init; } = "DuKien";
}

/// <summary>Dữ liệu đầu vào khi cập nhật lịch khai thác của một chuyến bay.</summary>
public sealed record CapNhatChuyenBayDTO
{
    [Range(1, int.MaxValue)]
    public int Id { get; init; }

    [Required, StringLength(20)]
    public string SoHieu { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int TuyenBayId { get; init; }

    [Range(1, int.MaxValue)]
    public int MayBayId { get; init; }

    public DateTimeOffset GioKhoiHanh { get; init; }
    public DateTimeOffset GioHaCanh { get; init; }

    [Range(typeof(decimal), "0", "9999999999.99")]
    public decimal GiaCoBan { get; init; }

    [Required, StringLength(20)]
    public string TrangThai { get; init; } = "DuKien";
}

public sealed record GheDTO
{
    public string SoGhe { get; init; } = string.Empty;

    /// <summary>Trong, DangGiu hoặc DaBan.</summary>
    public string TrangThai { get; init; } = "Trong";
}

public sealed record SoDoGheDTO
{
    public int ChuyenBayId { get; init; }
    public string SoHieu { get; init; } = string.Empty;
    public int TongSoGhe { get; init; }
    public IReadOnlyList<GheDTO> Ghes { get; init; } = Array.Empty<GheDTO>();
}
