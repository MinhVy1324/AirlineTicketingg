namespace AirlineTicketing.Domain;

public class KhachHang
{
    public int Id { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SoDienThoai { get; set; } = string.Empty;
    public DateTimeOffset NgayTao { get; set; }

    public ICollection<DatVe> DatVes { get; set; } = new List<DatVe>();
}
