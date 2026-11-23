namespace AirlineTicketing.DTO;

public sealed record ChuyenBaoCao(int? Id, string? SoHieu, DateTimeOffset? KhoiHanh, string Tuyen, string Hang);
public sealed record KhoanBaoCao(int Id, DateTimeOffset GhiNhanLuc, decimal SoTien, ChuyenBaoCao Chuyen);
public sealed record DoanhThuTheoChuyen(ChuyenBaoCao Chuyen, int VeDaXuat, decimal DaThu, decimal DaHoan)
{
    public decimal ThuRong => DaThu - DaHoan;
}

public sealed record BaoCaoDoanhThuDTO(
    IReadOnlyList<DoanhThuTheoChuyen> TheoChuyen,
    IReadOnlyList<KhoanBaoCao> XuHuongThu,
    decimal DaThuKyTruoc,
    int VeDaXuatKyTruoc,
    int SoDatChoTrongKy,
    int SoDatChoDaHuy,
    int SoGiaoDichHoan)
{
    public decimal DaThu => TheoChuyen.Sum(x => x.DaThu);
    public decimal DaHoan => TheoChuyen.Sum(x => x.DaHoan);
    public decimal ThuRong => DaThu - DaHoan;
    public int VeDaXuat => TheoChuyen.Sum(x => x.VeDaXuat);
}
