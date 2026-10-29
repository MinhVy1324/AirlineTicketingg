using System.ComponentModel.DataAnnotations;

namespace AirlineTicketing.DTO;

public sealed record HuyVeDTO
{
    [Required, StringLength(30)]
    public string MaVe { get; init; } = string.Empty;

    [StringLength(300)]
    public string? LyDo { get; init; }
}
