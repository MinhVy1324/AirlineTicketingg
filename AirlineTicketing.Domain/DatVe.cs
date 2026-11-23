namespace AirlineTicketing.Domain;

public class DatVe
{
    public int Id { get; set; }
    public string MaDatVe { get; set; } = string.Empty;
    public int KhachHangId { get; set; }
    public DateTimeOffset NgayDat { get; set; }
    public TrangThaiDatVe TrangThai { get; set; } = TrangThaiDatVe.ChoThanhToan;
    public decimal TongTien { get; set; }
    public string DonViTienTe { get; set; } = "VND";
<<<<<<< Updated upstream
=======
    public decimal ThuePhi { get; set; }
    public decimal PhiDichVu { get; set; }
    public decimal PhiHanhLy { get; set; }
    public decimal PhiDoiDaThu { get; set; }
    public string? MaKhuyenMai { get; set; }
    public decimal GiamGia { get; set; }
    public string? EmailLienHe { get; set; }
    public string? TenLienHe { get; set; }
    public string? SoDienThoaiLienHe { get; set; }
    public string? GhiChu { get; set; }
>>>>>>> Stashed changes

    public KhachHang KhachHang { get; set; } = null!;
    public ICollection<Ve> Ves { get; set; } = new List<Ve>();
    public ICollection<ThanhToan> ThanhToans { get; set; } = new List<ThanhToan>();
<<<<<<< Updated upstream
=======
    public ICollection<LichSuDatVe> LichSus { get; set; } = new List<LichSuDatVe>();
    public ICollection<YeuCauDoiHoan> YeuCaus { get; set; } = new List<YeuCauDoiHoan>();
    public ICollection<BienDongDatVe> BienDongs { get; set; } = new List<BienDongDatVe>();
>>>>>>> Stashed changes
}
