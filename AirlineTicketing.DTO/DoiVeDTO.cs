using System.ComponentModel.DataAnnotations;

namespace AirlineTicketing.DTO;

public sealed record DoiVeDTO
{
    [Range(1, int.MaxValue)]
    public int VeId { get; init; }

    [Range(1, int.MaxValue)]
    public int ChuyenBayMoiId { get; init; }

    [Required, StringLength(5)]
    public string SoGheMoi { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string HangVeMoi { get; init; } = string.Empty;
}
