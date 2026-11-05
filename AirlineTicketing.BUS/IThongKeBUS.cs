using AirlineTicketing.DTO;

namespace AirlineTicketing.BUS;

public interface IThongKeBUS
{
    Task<ThongKeDoanhThuDTO> GetDoanhThuTongQuanAsync(
        DateOnly? tuNgay = null,
        DateOnly? denNgay = null,
        string donViTienTe = "VND",
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DoanhThuTheoNgayDTO>> GetDoanhThuTheoNgayAsync(
        DateOnly tuNgay,
        DateOnly denNgay,
        string donViTienTe = "VND",
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DoanhThuTheoThangDTO>> GetDoanhThuTheoThangAsync(
        int nam,
        string donViTienTe = "VND",
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DoanhThuTheoChuyenBayDTO>> GetDoanhThuTheoChuyenBayAsync(
        DateOnly? tuNgay = null,
        DateOnly? denNgay = null,
        CancellationToken cancellationToken = default);

    Task<DashboardStatsDTO> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
}
