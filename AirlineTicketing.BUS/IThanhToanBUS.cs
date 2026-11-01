using AirlineTicketing.Domain;
using AirlineTicketing.DTO;

namespace AirlineTicketing.BUS;

public interface IThanhToanBUS
{
    Task<ThanhToanDTO?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ThanhToanDTO>> GetByDatVeIdAsync(int datVeId, CancellationToken cancellationToken = default);
    Task<ThanhToanDTO> KhoiTaoThanhToanAsync(int datVeId, PhuongThucThanhToan phuongThuc, string nhaCungCap = "NoiBo", CancellationToken cancellationToken = default);
    Task<KetQuaThanhToanDTO> XuLyThanhToanGiaLapAsync(int thanhToanId, bool thanhCong, string? maGiaoDichNgoai = null, CancellationToken cancellationToken = default);
    Task<HoanTienDTO> TaoHoanTienAsync(TaoHoanTienDTO dto, string? nguoiThucHien = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ThanhToanDTO>> GetAllGiaoDichAsync(string? keyword = null, TrangThaiThanhToan? trangThai = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HoanTienDTO>> GetAllHoanTienAsync(CancellationToken cancellationToken = default);
}
