namespace AirlineTicketing.Domain;

public class ChuyenBay
{
    public int Id { get; set; }
    public string SoHieu { get; set; } = string.Empty;
    public int SanBayDiId { get; set; }
    public int SanBayDenId { get; set; }
    public int MayBayId { get; set; }
    public DateTime GioKhoiHanh { get; set; }
    public DateTime GioHaCanh { get; set; }
    public decimal GiaCoBan { get; set; }

    public SanBay SanBayDi { get; set; } = null!;
    public SanBay SanBayDen { get; set; } = null!;
    public MayBay MayBay { get; set; } = null!;
    public ICollection<Ve> Ves { get; set; } = new List<Ve>();
}
