namespace AirlineTicketing.Domain;

public class ChuyenBay
{
    public int Id { get; set; }
    public string SoHieu { get; set; } = string.Empty;
    public int TuyenBayId { get; set; }
    public int MayBayId { get; set; }
    public int HangHangKhongId { get; set; }
    public DateTimeOffset GioKhoiHanh { get; set; }
    public DateTimeOffset GioHaCanh { get; set; }
    public decimal GiaCoBan { get; set; }
    public TrangThaiChuyenBay TrangThai { get; set; } = TrangThaiChuyenBay.DuKien;

    public TuyenBay TuyenBay { get; set; } = null!;
    public MayBay MayBay { get; set; } = null!;
    public HangHangKhong HangHangKhong { get; set; } = null!;
    public ICollection<Ve> Ves { get; set; } = new List<Ve>();
    public ICollection<HangVe> HangVes { get; set; } = new List<HangVe>();
    public ICollection<YeuCauDoiHoan> YeuCausDoiSang { get; set; } = new List<YeuCauDoiHoan>();
}
