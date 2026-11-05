namespace AirlineTicketing.Domain;

/// <summary>
/// Nhật ký bất biến cho các thay đổi quan trọng của đặt chỗ và vé.
/// Không dùng bảng này để suy ra trạng thái hiện tại; trạng thái hiện tại vẫn nằm trên DatVe/Ve.
/// </summary>
public sealed class LichSuDatVe
{
    public long Id { get; set; }
    public int DatVeId { get; set; }
    public int? VeId { get; set; }
    public string HanhDong { get; set; } = string.Empty;
    public string? TrangThaiTruoc { get; set; }
    public string TrangThaiSau { get; set; } = string.Empty;
    public string? LyDo { get; set; }
    public string? NguoiThucHien { get; set; }
    public string? DuLieuBoSung { get; set; }
    public DateTimeOffset ThoiDiem { get; set; }

    public DatVe DatVe { get; set; } = null!;
    public Ve? Ve { get; set; }
}
