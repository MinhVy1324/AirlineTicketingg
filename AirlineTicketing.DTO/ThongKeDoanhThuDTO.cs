namespace AirlineTicketing.DTO;

public sealed record ThongKeDoanhThuDTO
{
    public DateOnly TuNgay { get; init; }
    public DateOnly DenNgay { get; init; }
    public string DonViTienTe { get; init; } = "VND";
    public decimal TongTienDaThu { get; init; }
    public decimal TongTienDaHoan { get; init; }
    public decimal DoanhThuThuan { get; init; }
    public int SoGiaoDichThanhCong { get; init; }
    public int SoGiaoDichHoanTien { get; init; }
}

public sealed record DoanhThuTheoNgayDTO
{
    public DateOnly Ngay { get; init; }
    public decimal TienDaThu { get; init; }
    public decimal TienDaHoan { get; init; }
    public decimal DoanhThuThuan { get; init; }
    public int SoGiaoDich { get; init; }
}

public sealed record DoanhThuTheoThangDTO
{
    public int Nam { get; init; }
    public int Thang { get; init; }
    public decimal TienDaThu { get; init; }
    public decimal TienDaHoan { get; init; }
    public decimal DoanhThuThuan { get; init; }
    public int SoGiaoDich { get; init; }
}

public sealed record DoanhThuTheoChuyenBayDTO
{
    public int ChuyenBayId { get; init; }
    public string SoHieu { get; init; } = string.Empty;
    public string TuyenBay { get; init; } = string.Empty;
    public DateTimeOffset GioKhoiHanh { get; init; }
    public int SoVeDaBan { get; init; }
    public decimal DoanhThu { get; init; }
}

public sealed record DashboardStatsDTO
{
    public decimal TongDoanhThuThuan { get; init; }
    public decimal TongTienDaThu { get; init; }
    public decimal TongTienDaHoan { get; init; }
    public int TongDatVe { get; init; }
    public int TongVeDaBan { get; init; }
    public int TongKhachHang { get; init; }
    public int TongChuyenBay { get; init; }
    public decimal DoanhThuHomNay { get; init; }
    public decimal DoanhThuThangNay { get; init; }
}
