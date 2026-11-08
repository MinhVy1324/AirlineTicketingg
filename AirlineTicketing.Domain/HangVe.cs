namespace AirlineTicketing.Domain;

public class HangVe
{
    public int Id { get; set; }
    public int ChuyenBayId { get; set; }
    public string MaHang { get; set; } = string.Empty;
    public string TenHang { get; set; } = string.Empty;
    public string NhomHang { get; set; } = string.Empty;
    public decimal GiaCoBan { get; set; }
    public int SoGhe { get; set; }
    public int SoGheDaBan { get; set; }
    public int HanhLyXachTayKg { get; set; }
    public int HanhLyKyGuiKg { get; set; }
    public decimal PhiDoi { get; set; }
    public decimal PhiHoan { get; set; }
    public bool DangMoBan { get; set; } = true;

    public ChuyenBay ChuyenBay { get; set; } = null!;
}
