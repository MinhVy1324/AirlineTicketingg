using AirlineTicketing.Domain;
using AirlineTicketing.DTO;

namespace AirlineTicketing.BUS;

public interface ITaiKhoanBUS
{
    Task<TaiKhoanDTO> DangKyAsync(DangKyDTO dto, CancellationToken cancellationToken = default);
    Task<TaiKhoanDTO?> DangNhapAsync(string tenDangNhap, string matKhau, CancellationToken cancellationToken = default);
    Task<TaiKhoanDTO?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TaiKhoanDTO?> GetByTenDangNhapAsync(string tenDangNhap, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaiKhoanDTO>> GetAllAsync(string? vaiTro = null, string? keyword = null, CancellationToken cancellationToken = default);
    Task<TaiKhoanDTO> TaoTaiKhoanNoiBoAsync(TaoNhanVienDTO dto, CancellationToken cancellationToken = default);
    Task<bool> CapNhatHoSoAsync(int taiKhoanId, CapNhatHoSoDTO dto, CancellationToken cancellationToken = default);
    Task<bool> DoiMatKhauAsync(int taiKhoanId, DoiMatKhauDTO dto, CancellationToken cancellationToken = default);
    Task<bool> DatLaiMatKhauAsync(int taiKhoanId, string matKhauMoi, CancellationToken cancellationToken = default);
    Task<bool> CapNhatTrangThaiAsync(int taiKhoanId, TrangThaiTaiKhoan trangThai, CancellationToken cancellationToken = default);
    Task<bool> CapNhatVaiTroAsync(int taiKhoanId, VaiTroTaiKhoan vaiTro, CancellationToken cancellationToken = default);
}
