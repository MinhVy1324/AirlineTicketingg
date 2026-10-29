namespace AirlineTicketing.Domain;

public class MayBay
{
    public int Id { get; set; }
    public string MaMayBay { get; set; } = string.Empty;
    public string TenMayBay { get; set; } = string.Empty;
    public string HangSanXuat { get; set; } = string.Empty;
    public int SoGhe { get; set; }
    public bool DangHoatDong { get; set; } = true;

    public ICollection<ChuyenBay> ChuyenBays { get; set; } = new List<ChuyenBay>();
}
