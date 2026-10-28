namespace AirlineTicketing.Domain;

public class DatVe
{
    public int Id { get; set; }
    public string MaDatVe { get; set; } = string.Empty;
    public string HoTenKhachHang { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SoDienThoai { get; set; } = string.Empty;
    public DateTime NgayDat { get; set; }
    public string TrangThai { get; set; } = string.Empty;

    public ICollection<Ve> Ves { get; set; } = new List<Ve>();
}
