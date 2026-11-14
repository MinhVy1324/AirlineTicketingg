namespace AirlineTicketing.Domain;

public class TaiKhoan
{
    public int Id { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhauHash { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }
    public VaiTro VaiTro { get; set; } = VaiTro.KhachHang;
    public TrangThaiTaiKhoan TrangThai { get; set; } = TrangThaiTaiKhoan.HoatDong;
    public string? MaNhanSu { get; set; }
    public DateTimeOffset NgayTao { get; set; }
    public DateTimeOffset? LanDangNhapCuoi { get; set; }
    public string? GhiChu { get; set; }

    public int? KhachHangId { get; set; }
    public KhachHang? KhachHang { get; set; }

    public ICollection<NhatKyHoatDong> NhatKys { get; set; } = new List<NhatKyHoatDong>();
    public ICollection<YeuCauDoiHoan> YeuCausXuLy { get; set; } = new List<YeuCauDoiHoan>();
}
