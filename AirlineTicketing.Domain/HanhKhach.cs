namespace AirlineTicketing.Domain;

public class HanhKhach
{
    public int Id { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public DateOnly? NgaySinh { get; set; }
    public string? QuocTich { get; set; }
    public string? SoHoChieu { get; set; }

    public ICollection<Ve> Ves { get; set; } = new List<Ve>();
}
