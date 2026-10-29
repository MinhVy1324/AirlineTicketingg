namespace AirlineTicketing.Domain;

public class SanBay
{
    public int Id { get; set; }
    public string MaSanBay { get; set; } = string.Empty;
    public string TenSanBay { get; set; } = string.Empty;
    public string ThanhPho { get; set; } = string.Empty;
    public string QuocGia { get; set; } = string.Empty;
    public bool DangHoatDong { get; set; } = true;

    public ICollection<TuyenBay> TuyenBayDi { get; set; } = new List<TuyenBay>();
    public ICollection<TuyenBay> TuyenBayDen { get; set; } = new List<TuyenBay>();
}
