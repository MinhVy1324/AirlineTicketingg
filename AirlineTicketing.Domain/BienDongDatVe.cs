namespace AirlineTicketing.Domain;

public sealed class BienDongDatVe
{
    public int Id { get; set; }
    public int DatVeId { get; set; }
    public int ChuyenBayId { get; set; }
    public string SoHieuCu { get; set; } = string.Empty;
    public string? SoHieuMoi { get; set; }
    public DateTimeOffset KhoiHanhCu { get; set; }
    public DateTimeOffset HaCanhCu { get; set; }
    public DateTimeOffset? KhoiHanhMoi { get; set; }
    public DateTimeOffset? HaCanhMoi { get; set; }
    public DateTimeOffset NgayTao { get; set; }
    public DateTimeOffset? NgayXuLy { get; set; }
    public string? KetQua { get; set; }

    public DatVe DatVe { get; set; } = null!;
    public ChuyenBay ChuyenBay { get; set; } = null!;

    public bool BiHuy => KhoiHanhMoi == null;
}
