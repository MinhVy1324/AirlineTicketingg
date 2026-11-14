using AirlineTicketing.DAL;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.BUS;

/// <summary>
/// Đọc cấu hình nghiệp vụ trong bảng <c>CauHinhHeThongs</c> để các luồng đặt vé,
/// đổi/hoàn vé và liên hệ dùng chung một nguồn thay vì hardcode. Mọi getter đều có
/// giá trị dự phòng nên trang không hỏng khi bảng cấu hình trống.
/// </summary>
public sealed class CauHinhHeThongBUS(AppDbContext dbContext)
{
    private static readonly string[] PolicyKeys =
    [
        "booking.hold_minutes",
        "booking.service_fee",
        "booking.tax_default",
        "policy.change_fee",
        "policy.refund_fee",
        "contact.hotline",
        "contact.email"
    ];

    public async Task<string?> LayAsync(string khoa, CancellationToken cancellationToken = default) =>
        await dbContext.CauHinhHeThongs.AsNoTracking()
            .Where(x => x.Khoa == khoa)
            .Select(x => x.GiaTri)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<decimal> LaySoAsync(string khoa, decimal macDinh,
        CancellationToken cancellationToken = default)
    {
        var raw = await LayAsync(khoa, cancellationToken);
        return TryParseSo(raw, out var value) ? Math.Max(0m, value) : macDinh;
    }

    public async Task<int> LaySoNguyenAsync(string khoa, int macDinh,
        CancellationToken cancellationToken = default)
    {
        var value = await LaySoAsync(khoa, macDinh, cancellationToken);
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded > 0 ? rounded : macDinh;
    }

    /// <summary>Đọc một lần toàn bộ tham số dùng cho luồng đặt vé / vé của tôi.</summary>
    public async Task<ChinhSachDatVe> LayChinhSachAsync(CancellationToken cancellationToken = default)
    {
        var values = await dbContext.CauHinhHeThongs.AsNoTracking()
            .Where(x => PolicyKeys.Contains(x.Khoa))
            .ToDictionaryAsync(x => x.Khoa, x => x.GiaTri, cancellationToken);

        var macDinh = new ChinhSachDatVe();
        string? Doc(string khoa) => values.GetValueOrDefault(khoa);

        return new ChinhSachDatVe
        {
            HoldMinutes = ToInt(Doc("booking.hold_minutes"), macDinh.HoldMinutes),
            ServiceFee = ToMoney(Doc("booking.service_fee"), macDinh.ServiceFee),
            Tax = ToMoney(Doc("booking.tax_default"), macDinh.Tax),
            ChangeFee = ToMoney(Doc("policy.change_fee"), macDinh.ChangeFee),
            RefundFee = ToMoney(Doc("policy.refund_fee"), macDinh.RefundFee),
            Hotline = ToText(Doc("contact.hotline"), macDinh.Hotline),
            SupportEmail = ToText(Doc("contact.email"), macDinh.SupportEmail)
        };
    }

    private static int ToInt(string? raw, int macDinh) =>
        TryParseSo(raw, out var value) && value > 0
            ? (int)Math.Round(value, MidpointRounding.AwayFromZero)
            : macDinh;

    private static decimal ToMoney(string? raw, decimal macDinh) =>
        TryParseSo(raw, out var value) ? Math.Max(0m, value) : macDinh;

    private static string ToText(string? raw, string macDinh) =>
        string.IsNullOrWhiteSpace(raw) ? macDinh : raw.Trim();

    /// <summary>
    /// Cấu hình tiền tệ được nhập dạng VNĐ nguyên (ví dụ <c>70000</c>). Chỉ lấy chữ số
    /// để bỏ qua dấu phân cách nghìn/đơn vị mà người dùng có thể gõ trong /admin/cau-hinh.
    /// </summary>
    private static bool TryParseSo(string? raw, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length > 0
            && decimal.TryParse(digits, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
