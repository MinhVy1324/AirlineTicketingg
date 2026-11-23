using System.Globalization;

namespace AirlineTicketing.Web.Infrastructure;

public static class UiLanguage
{
    public const string Vietnamese = "vi";
    public const string English = "en";
    public const string CookieName = "aeroviet.language";

    private static readonly IReadOnlyDictionary<string, string> EnglishLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Phổ thông"] = "Economy",
        ["Phổ thông Tiêu chuẩn"] = "Standard Economy",
        ["Thương gia"] = "Business",
        ["Nam"] = "Male",
        ["Nữ"] = "Female",
        ["Khác"] = "Other",
        ["Việt Nam"] = "Vietnam",
        ["Người lớn"] = "Adult",
        ["Trẻ em"] = "Child",
        ["Em bé"] = "Infant",
        ["Chưa chọn"] = "Not selected",
        ["Đang giữ chỗ"] = "On hold",
        ["Chờ thanh toán"] = "Awaiting payment",
        ["Chờ xác nhận"] = "Awaiting confirmation",
        ["Chờ xử lý"] = "Pending",
        ["Chờ đối soát"] = "Awaiting reconciliation",
        ["Chờ duyệt"] = "Awaiting approval",
        ["Chờ hoàn tiền"] = "Awaiting refund",
        ["Đã duyệt"] = "Approved",
        ["Đã duyệt · Chờ hoàn tiền"] = "Approved · Awaiting refund",
        ["Từ chối"] = "Declined",
        ["Đã từ chối"] = "Declined",
        ["Đã xác nhận"] = "Confirmed",
        ["Đã phát hành"] = "Issued",
        ["Đã xuất vé"] = "Ticket issued",
        ["Đã sử dụng"] = "Used",
        ["Đã thanh toán"] = "Paid",
        ["Đã hủy"] = "Cancelled",
        ["Bị hủy"] = "Cancelled",
        ["Đổi giờ"] = "Rescheduled",
        ["Hết hạn"] = "Expired",
        ["Hoàn tiền"] = "Refund",
        ["Đã hoàn"] = "Refunded",
        ["Đã hoàn tiền"] = "Refunded",
        ["Thành công"] = "Successful",
        ["Thất bại"] = "Failed",
        ["Dự kiến"] = "Scheduled",
        ["Đang bay"] = "In flight",
        ["Đã hạ cánh"] = "Landed",
        ["Hoạt động"] = "Active",
        ["Đã khóa"] = "Locked",
        ["Đã kết nối"] = "Connected",
        ["Khách hàng"] = "Customer",
        ["Quản lý"] = "Manager",
        ["Quản trị viên"] = "Administrator",
        ["Tiêu chuẩn"] = "Standard",
        ["Cửa sổ"] = "Window",
        ["Lối đi"] = "Aisle",
        ["Ghế giữa"] = "Middle seat",
        ["Xem chi tiết"] = "View details",
        ["DangGiuCho"] = "On hold",
        ["ChoThanhToan"] = "Awaiting payment",
        ["DaXacNhan"] = "Confirmed",
        ["DaPhatHanh"] = "Issued",
        ["ChuaThanhToan"] = "Unpaid",
        ["DaThanhToan"] = "Paid",
        ["DaHoan"] = "Refunded",
        ["DaHuy"] = "Cancelled",
        ["HetHan"] = "Expired"
    };

    public static bool IsEnglish => CultureInfo.CurrentUICulture.Name == English;
    public static string Code => IsEnglish ? English : Vietnamese;

    public static string T(string vietnamese, string english) => IsEnglish ? english : vietnamese;

    public static string Label(string? vietnamese) => vietnamese is null ? string.Empty
        : IsEnglish && EnglishLabels.TryGetValue(vietnamese, out var english) ? english : vietnamese;
}
