using System.ComponentModel.DataAnnotations;

namespace AirlineTicketing.DTO;

public sealed record HanhKhachDTO
{
    public int Id { get; init; }

    [Required, StringLength(150)]
    public string HoTen { get; init; } = string.Empty;

    public DateOnly? NgaySinh { get; init; }

    [StringLength(100)]
    public string? QuocTich { get; init; }

    [StringLength(30)]
    public string? SoHoChieu { get; init; }
}
