namespace AirlineTicketing.Domain;

public class Ve
{
    public int Id { get; set; }
    public string MaVe { get; set; } = string.Empty;
    public int ChuyenBayId { get; set; }
    public int DatVeId { get; set; }
    public int HanhKhachId { get; set; }
    public string SoGhe { get; set; } = string.Empty;
    public string HangVe { get; set; } = string.Empty;
    public decimal GiaVe { get; set; }
    public TrangThaiVe TrangThai { get; set; } = TrangThaiVe.TamGiu;

    public ChuyenBay ChuyenBay { get; set; } = null!;
    public DatVe DatVe { get; set; } = null!;
    public HanhKhach HanhKhach { get; set; } = null!;
}
