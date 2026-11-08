namespace AirlineTicketing.Domain;

public class YeuCauDoiHoan
{
    public int Id { get; set; }
    public string MaYeuCau { get; set; } = string.Empty;
    public int DatVeId { get; set; }
    public LoaiYeuCau LoaiYeuCau { get; set; }
    public string LyDo { get; set; } = string.Empty;
    public string? ThongTinBoSung { get; set; }
    public TrangThaiYeuCau TrangThai { get; set; } = TrangThaiYeuCau.ChoDuyet;
    public decimal PhiDoi { get; set; }
    public decimal PhiHoan { get; set; }
    public decimal PhiDichVuKhongHoan { get; set; }
    public decimal PhiHanhLyKhongHoan { get; set; }
    public decimal SoTienHoan { get; set; }
    public string? PhuongThucHoan { get; set; }
    public int? ChuyenBayMoiId { get; set; }
    public DateTimeOffset NgayTao { get; set; }
    public DateTimeOffset? NgayXuLy { get; set; }
    public int? NguoiXuLyId { get; set; }
    public string? GhiChuXuLy { get; set; }

    public DatVe DatVe { get; set; } = null!;
    public ChuyenBay? ChuyenBayMoi { get; set; }
    public TaiKhoan? NguoiXuLy { get; set; }
}
