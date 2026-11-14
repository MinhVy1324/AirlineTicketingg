namespace AirlineTicketing.Domain;

public class NhatKyHoatDong
{
    public int Id { get; set; }
    public string MaNhatKy { get; set; } = string.Empty;
    public int? TaiKhoanId { get; set; }
    public string TenNguoiDung { get; set; } = string.Empty;
    public string VaiTro { get; set; } = string.Empty;
    public string HanhDong { get; set; } = string.Empty;
    public string DoiTuong { get; set; } = string.Empty;
    public KetQuaNhatKy KetQua { get; set; } = KetQuaNhatKy.ThanhCong;
    public string? ChiTiet { get; set; }
    public string? DiaChiIp { get; set; }
    public DateTimeOffset ThoiGian { get; set; }

    public TaiKhoan? TaiKhoan { get; set; }
}
