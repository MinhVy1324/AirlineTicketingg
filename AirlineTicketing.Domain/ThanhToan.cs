namespace AirlineTicketing.Domain;

public class ThanhToan
{
    public int Id { get; set; }
    public int DatVeId { get; set; }
    public decimal SoTien { get; set; }
    public string DonViTienTe { get; set; } = "VND";
    public PhuongThucThanhToan PhuongThuc { get; set; }
    public string NhaCungCap { get; set; } = "NoiBo";
    public TrangThaiThanhToan TrangThai { get; set; } = TrangThaiThanhToan.ChoXuLy;
    public string? MaGiaoDich { get; set; }
    public DateTimeOffset NgayTao { get; set; }
    public DateTimeOffset? NgayHoanTat { get; set; }

    public DatVe DatVe { get; set; } = null!;
    public ICollection<HoanTien> HoanTiens { get; set; } = new List<HoanTien>();
}
