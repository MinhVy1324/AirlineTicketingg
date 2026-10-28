namespace AirlineTicketing.Domain;

public class SanBay
{
    public int Id { get; set; }
    public string MaSanBay { get; set; } = string.Empty;
    public string TenSanBay { get; set; } = string.Empty;
    public string ThanhPho { get; set; } = string.Empty;
    public string QuocGia { get; set; } = string.Empty;

    public ICollection<ChuyenBay> ChuyenBayDi { get; set; } = new List<ChuyenBay>();
    public ICollection<ChuyenBay> ChuyenBayDen { get; set; } = new List<ChuyenBay>();
}
