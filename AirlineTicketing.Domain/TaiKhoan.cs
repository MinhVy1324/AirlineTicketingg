namespace AirlineTicketing.Domain;

public class TaiKhoan
{
    public const string InternalEmailSuffix = "@accounts.aeroviet.invalid";
    public int Id { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhauHash { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
<<<<<<< Updated upstream
    public string SoDienThoai { get; set; } = string.Empty;
    public VaiTroTaiKhoan VaiTro { get; set; } = VaiTroTaiKhoan.KhachHang;
=======
    public string? GmailLienHe { get; set; }
    public string? SoDienThoai { get; set; }
    public VaiTro VaiTro { get; set; } = VaiTro.KhachHang;
>>>>>>> Stashed changes
    public TrangThaiTaiKhoan TrangThai { get; set; } = TrangThaiTaiKhoan.HoatDong;
    public DateTimeOffset NgayTao { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LanDangNhapCuoi { get; set; }

    public int? KhachHangId { get; set; }
    public KhachHang? KhachHang { get; set; }
<<<<<<< Updated upstream
=======

    public ICollection<NhatKyHoatDong> NhatKys { get; set; } = new List<NhatKyHoatDong>();
    public ICollection<YeuCauDoiHoan> YeuCausXuLy { get; set; } = new List<YeuCauDoiHoan>();

    public bool UsesUsernameRegistration() => Email.EndsWith(InternalEmailSuffix, StringComparison.OrdinalIgnoreCase);
    public bool NeedsProfile() => UsesUsernameRegistration() && KhachHangId is null;
>>>>>>> Stashed changes
}
