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
}

public sealed record DoanhThuTheoNgayDTO
{
    public DateOnly Ngay { get; init; }
    public decimal TienDaThu { get; init; }
    public decimal TienDaHoan { get; init; }
    public decimal DoanhThuThuan { get; init; }
}
