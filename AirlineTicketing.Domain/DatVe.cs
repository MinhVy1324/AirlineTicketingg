namespace AirlineTicketing.Domain;

public class DatVe
{
    public int Id { get; set; }
    public string MaDatVe { get; set; } = string.Empty;
    public int KhachHangId { get; set; }
    public DateTimeOffset NgayDat { get; set; }
    public TrangThaiDatVe TrangThai { get; set; } = TrangThaiDatVe.ChoThanhToan;
    public decimal TongTien { get; set; }
    public string DonViTienTe { get; set; } = "VND";

    public KhachHang KhachHang { get; set; } = null!;
    public ICollection<Ve> Ves { get; set; } = new List<Ve>();
    public ICollection<ThanhToan> ThanhToans { get; set; } = new List<ThanhToan>();
}
