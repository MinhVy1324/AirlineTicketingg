using System.ComponentModel.DataAnnotations;

namespace AirlineTicketing.DTO;

public sealed record KhachHangDTO
{
    public int Id { get; init; }

    [Required, StringLength(150)]
    public string HoTen { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, Phone, StringLength(20)]
    public string SoDienThoai { get; init; } = string.Empty;
}
