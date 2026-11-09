namespace AirlineTicketing.Domain;

public class CauHinhHeThong
{
    public int Id { get; set; }
    public string Khoa { get; set; } = string.Empty;
    public string GiaTri { get; set; } = string.Empty;
    public string Nhom { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public string? KieuDuLieu { get; set; }
    public DateTimeOffset NgayCapNhat { get; set; }
    public string? NguoiCapNhat { get; set; }
}
