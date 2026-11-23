namespace AirlineTicketing.Domain;

public class HoanTien
{
    public int Id { get; set; }
    public int ThanhToanId { get; set; }
    public decimal SoTien { get; set; }
    public string DonViTienTe { get; set; } = "VND";
    public string NhaCungCap { get; set; } = "NoiBo";
    public string? MaGiaoDich { get; set; }
    public string? LyDo { get; set; }
    public TrangThaiHoanTien TrangThai { get; set; } = TrangThaiHoanTien.ChoXuLy;
    public DateTimeOffset NgayTao { get; set; }
    public DateTimeOffset? NgayHoanTat { get; set; }
    public DateTimeOffset? NgayGuiCongThanhToan { get; set; }
    public string? MaGiaoDichNhaCungCap { get; set; }
    public string? KetQuaNhaCungCap { get; set; }
    public int? YeuCauDoiHoanId { get; set; }
    public YeuCauDoiHoan? YeuCauDoiHoan { get; set; }

    public ThanhToan ThanhToan { get; set; } = null!;
}
