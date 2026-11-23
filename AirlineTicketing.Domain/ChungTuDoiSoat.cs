namespace AirlineTicketing.Domain;

public sealed class ChungTuDoiSoat
{
    public int Id { get; set; }
    public int ThanhToanId { get; set; }
    public int? HoanTienId { get; set; }
    public string NhaCungCap { get; set; } = string.Empty;
    public string MaLenh { get; set; } = string.Empty;
    public string MaChungTu { get; set; } = string.Empty;
    public string KetQua { get; set; } = string.Empty;
    public decimal SoTien { get; set; }
    public string NguoiDoiSoat { get; set; } = string.Empty;
    public DateTimeOffset ThoiDiem { get; set; }
    public ThanhToan ThanhToan { get; set; } = null!;
    public HoanTien? HoanTien { get; set; }
}
