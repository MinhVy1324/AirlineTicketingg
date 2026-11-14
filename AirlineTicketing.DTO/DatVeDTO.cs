using System.ComponentModel.DataAnnotations;

namespace AirlineTicketing.DTO;

public sealed record DatVeDTO
{
    public int Id { get; init; }
    public string MaDatVe { get; init; } = string.Empty;
    public KhachHangDTO KhachHang { get; init; } = new();
    public DateTimeOffset NgayDat { get; init; }
    public DateTimeOffset HetHanGiuCho { get; init; }
    public string TrangThai { get; init; } = string.Empty;
    public decimal TongTien { get; init; }
    public string DonViTienTe { get; init; } = "VND";
    public IReadOnlyList<VeDTO> Ves { get; init; } = Array.Empty<VeDTO>();
    public IReadOnlyList<ThanhToanDTO> ThanhToans { get; init; } = Array.Empty<ThanhToanDTO>();
}

public sealed record TaoDatVeDTO
{
    [Required]
    public KhachHangDTO NguoiLienHe { get; init; } = new();

    [Required, MinLength(1), MaxLength(9)]
    public IReadOnlyList<TaoVeDTO> Ves { get; init; } = Array.Empty<TaoVeDTO>();
}

public sealed record XuLyThanhToanDTO
{
    [Required, StringLength(30)]
    public string MaDatVe { get; init; } = string.Empty;

    [Required, StringLength(50)]
    public string NhaCungCap { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string MaGiaoDich { get; init; } = string.Empty;

    public bool ThanhCong { get; init; }
}

public sealed record KetQuaThanhToanDTO
{
    public bool DaXacNhan { get; init; }
    public bool DaXuLyTruocDo { get; init; }
    public DatVeDTO DatVe { get; init; } = new();
}

public sealed record TaoVeDTO
{
    [Range(1, int.MaxValue)]
    public int ChuyenBayId { get; init; }

    [Required, StringLength(5)]
    public string SoGhe { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string HangVe { get; init; } = "PhoThong";

    [Required]
    public HanhKhachDTO HanhKhach { get; init; } = new();
}
