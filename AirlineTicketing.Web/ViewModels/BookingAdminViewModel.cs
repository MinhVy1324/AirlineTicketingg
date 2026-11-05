using AirlineTicketing.DTO;

namespace AirlineTicketing.Web.ViewModels;

public sealed class BookingAdminViewModel
{
    public string? TuKhoa { get; init; }
    public IReadOnlyList<DatVeDTO> DatVes { get; init; } = Array.Empty<DatVeDTO>();
}
