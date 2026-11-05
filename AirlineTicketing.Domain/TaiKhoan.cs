namespace AirlineTicketing.Domain;

public class TaiKhoan
{
    public int Id { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhauHash { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SoDienThoai { get; set; } = string.Empty;
    public VaiTroTaiKhoan VaiTro { get; set; } = VaiTroTaiKhoan.KhachHang;
    public TrangThaiTaiKhoan TrangThai { get; set; } = TrangThaiTaiKhoan.HoatDong;
    public DateTimeOffset NgayTao { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LanDangNhapCuoi { get; set; }

    public int? KhachHangId { get; set; }
    public KhachHang? KhachHang { get; set; }
}
