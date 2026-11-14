namespace AirlineTicketing.BUS;

/// <summary>
/// Ảnh chụp các tham số nghiệp vụ đọc từ bảng <c>CauHinhHeThongs</c>. Giá trị mặc định
/// trùng với dữ liệu seed để hệ thống vẫn chạy được khi thiếu cấu hình.
/// </summary>
public sealed record ChinhSachDatVe
{
    public const int DefaultHoldMinutes = 15;
    public const decimal DefaultServiceFee = 70_000m;
    public const decimal DefaultTax = 380_000m;
    public const decimal DefaultChangeFee = 360_000m;
    public const decimal DefaultRefundFee = 500_000m;
    public const string DefaultHotline = "1900 636 063";
    public const string DefaultSupportEmail = "support@aeroviet.vn";

    public int HoldMinutes { get; init; } = DefaultHoldMinutes;
    public decimal ServiceFee { get; init; } = DefaultServiceFee;
    public decimal Tax { get; init; } = DefaultTax;
    public decimal ChangeFee { get; init; } = DefaultChangeFee;
    public decimal RefundFee { get; init; } = DefaultRefundFee;
    public string Hotline { get; init; } = DefaultHotline;
    public string SupportEmail { get; init; } = DefaultSupportEmail;

    public TimeSpan HoldDuration => TimeSpan.FromMinutes(HoldMinutes);
}
