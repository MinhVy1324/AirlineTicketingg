namespace AirlineTicketing.Domain;

public class ChuyenBay
{
    public int Id { get; set; }
    public string SoHieu { get; set; } = string.Empty;
    public int TuyenBayId { get; set; }
    public int MayBayId { get; set; }
    public DateTimeOffset GioKhoiHanh { get; set; }
    public DateTimeOffset GioHaCanh { get; set; }
    public decimal GiaCoBan { get; set; }
    public TrangThaiChuyenBay TrangThai { get; set; } = TrangThaiChuyenBay.DuKien;

    public TuyenBay TuyenBay { get; set; } = null!;
    public MayBay MayBay { get; set; } = null!;
    public ICollection<Ve> Ves { get; set; } = new List<Ve>();
}
