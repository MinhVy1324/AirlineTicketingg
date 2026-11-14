using System.ComponentModel.DataAnnotations;
using AirlineTicketing.DTO;

namespace AirlineTicketing.Web.ViewModels;

public sealed class CreateReservationViewModel
{
    [Range(1, int.MaxValue)] public int ChuyenBayId { get; set; }
    [Required, StringLength(5)] public string SoGhe { get; set; } = string.Empty;
    [Required, StringLength(20)] public string HangVe { get; set; } = "PhoThong";
    [Required, StringLength(150)] public string HoTenLienHe { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = string.Empty;
    [Required, Phone, StringLength(20)] public string SoDienThoai { get; set; } = string.Empty;
    [Required, StringLength(150)] public string HoTenHanhKhach { get; set; } = string.Empty;
    [DataType(DataType.Date)] public DateOnly? NgaySinh { get; set; }
    [StringLength(100)] public string? QuocTich { get; set; }
    [StringLength(30)] public string? SoHoChieu { get; set; }
}

public sealed class ReservationConfirmationViewModel
{
    public DatVeDTO DatVe { get; init; } = new();
}

public sealed class ManageReservationViewModel
{
    [Required, StringLength(30)] public string MaDatVe { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    public DatVeDTO? KetQua { get; set; }
    public string? Loi { get; set; }
}
