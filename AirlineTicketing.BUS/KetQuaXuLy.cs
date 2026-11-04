namespace AirlineTicketing.BUS;

/// <summary>Kết quả xử lý nghiệp vụ thống nhất để controller hiển thị lỗi cho người dùng.</summary>
public sealed record KetQuaXuLy(bool ThanhCong, string? Loi = null)
{
    public static KetQuaXuLy Ok() => new(true);

    public static KetQuaXuLy ThatBai(string loi) => new(false, loi);
}
