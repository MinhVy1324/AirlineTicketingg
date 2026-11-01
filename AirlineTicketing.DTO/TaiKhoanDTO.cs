using System.ComponentModel.DataAnnotations;

namespace AirlineTicketing.DTO;

public sealed record DangKyDTO
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 50 ký tự")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Tên đăng nhập chỉ chứa chữ cái, chữ số và dấu gạch dưới")]
    public string TenDangNhap { get; init; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string MatKhau { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận lại mật khẩu")]
    [Compare(nameof(MatKhau), ErrorMessage = "Mật khẩu xác nhận không khớp")]
    public string XacNhanMatKhau { get; init; } = string.Empty;

    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [StringLength(150, ErrorMessage = "Họ và tên tối đa 150 ký tự")]
    public string HoTen { get; init; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [StringLength(254, ErrorMessage = "Email tối đa 254 ký tự")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại không được để trống")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
    public string SoDienThoai { get; init; } = string.Empty;
}

public sealed record DangNhapDTO
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email")]
    public string TenDangNhap { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    public string MatKhau { get; init; } = string.Empty;

    public bool GhiNho { get; init; } = false;

    public string? ReturnUrl { get; init; }
}

public sealed record TaiKhoanDTO
{
    public int Id { get; init; }
    public string TenDangNhap { get; init; } = string.Empty;
    public string HoTen { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string SoDienThoai { get; init; } = string.Empty;
    public string VaiTro { get; init; } = string.Empty;
    public string TrangThai { get; init; } = string.Empty;
    public DateTimeOffset NgayTao { get; init; }
    public DateTimeOffset? LanDangNhapCuoi { get; init; }
    public int? KhachHangId { get; init; }
}

public sealed record TaoNhanVienDTO
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 50 ký tự")]
    public string TenDangNhap { get; init; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu khởi tạo không được để trống")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    public string MatKhau { get; init; } = string.Empty;

    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [StringLength(150, ErrorMessage = "Họ và tên tối đa 150 ký tự")]
    public string HoTen { get; init; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại không được để trống")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string SoDienThoai { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vai trò")]
    public string VaiTro { get; init; } = "NhanVien";
}

public sealed record CapNhatHoSoDTO
{
    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [StringLength(150, ErrorMessage = "Họ và tên tối đa 150 ký tự")]
    public string HoTen { get; init; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại không được để trống")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string SoDienThoai { get; init; } = string.Empty;
}

public sealed record DoiMatKhauDTO
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
    public string MatKhauHienTai { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới tối thiểu 6 ký tự")]
    public string MatKhauMoi { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
    [Compare(nameof(MatKhauMoi), ErrorMessage = "Mật khẩu xác nhận không khớp")]
    public string XacNhanMatKhauMoi { get; init; } = string.Empty;
}

public sealed record CapNhatTaiKhoanAdminDTO
{
    public int Id { get; init; }

    [Required(ErrorMessage = "Họ tên không được để trống")]
    public string HoTen { get; init; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    public string SoDienThoai { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vai trò không được để trống")]
    public string VaiTro { get; init; } = string.Empty;

    [Required(ErrorMessage = "Trạng thái không được để trống")]
    public string TrangThai { get; init; } = string.Empty;
}
