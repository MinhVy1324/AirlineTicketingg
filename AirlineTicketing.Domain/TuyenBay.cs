namespace AirlineTicketing.Domain;

public class TuyenBay
{
    public int Id { get; set; }
    public int SanBayDiId { get; set; }
    public int SanBayDenId { get; set; }
    public int? ThoiLuongDuKienPhut { get; set; }
    public bool DangKhaiThac { get; set; } = true;

    public SanBay SanBayDi { get; set; } = null!;
    public SanBay SanBayDen { get; set; } = null!;
    public ICollection<ChuyenBay> ChuyenBays { get; set; } = new List<ChuyenBay>();
}
