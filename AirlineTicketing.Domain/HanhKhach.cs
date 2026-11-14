namespace AirlineTicketing.Domain;

public class HanhKhach
{
    public int Id { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public DateOnly? NgaySinh { get; set; }
    public GioiTinh GioiTinh { get; set; } = GioiTinh.Khac;
    public LoaiHanhKhach LoaiHanhKhach { get; set; } = LoaiHanhKhach.NguoiLon;
    public string? QuocTich { get; set; }
    public string? SoHoChieu { get; set; }
    public string? Email { get; set; }
    public string? SoDienThoai { get; set; }

    public ICollection<Ve> Ves { get; set; } = new List<Ve>();
}
