namespace AirlineTicketing.Domain;

public class HangHangKhong
{
    public int Id { get; set; }
    public string MaHang { get; set; } = string.Empty;
    public string TenHang { get; set; } = string.Empty;
    public string QuocGia { get; set; } = string.Empty;
    public string? MaIcao { get; set; }
    public bool DangHoatDong { get; set; } = true;

    public ICollection<ChuyenBay> ChuyenBays { get; set; } = new List<ChuyenBay>();
}
