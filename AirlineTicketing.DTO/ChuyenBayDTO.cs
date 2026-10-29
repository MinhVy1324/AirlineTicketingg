namespace AirlineTicketing.DTO;

public sealed record ChuyenBayDTO
{
    public int Id { get; init; }
    public string SoHieu { get; init; } = string.Empty;
    public int TuyenBayId { get; init; }
    public string MaSanBayDi { get; init; } = string.Empty;
    public string TenSanBayDi { get; init; } = string.Empty;
    public string MaSanBayDen { get; init; } = string.Empty;
    public string TenSanBayDen { get; init; } = string.Empty;
    public string MaMayBay { get; init; } = string.Empty;
    public DateTimeOffset GioKhoiHanh { get; init; }
    public DateTimeOffset GioHaCanh { get; init; }
    public decimal GiaCoBan { get; init; }
    public int SoGheTrong { get; init; }
    public string TrangThai { get; init; } = string.Empty;
}

public sealed record TimChuyenBayDTO
{
    public int SanBayDiId { get; init; }
    public int SanBayDenId { get; init; }
    public DateOnly NgayDi { get; init; }
    public int SoHanhKhach { get; init; } = 1;
    public int Trang { get; init; } = 1;
    public int KichThuocTrang { get; init; } = 20;
}
