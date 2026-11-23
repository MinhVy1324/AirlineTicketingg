namespace AirlineTicketing.Web.Infrastructure;

/// <summary>
/// Tên các chính sách giới hạn tần suất dùng chung giữa Program.cs và controller.
/// </summary>
public static class AppRateLimits
{
    /// <summary>Chặn dò mật khẩu bằng cách giới hạn số lần POST /dang-nhap theo IP.</summary>
    public const string Login = "login";

    /// <summary>Giới hạn số lần gửi lại vé điện tử qua email theo IP.</summary>
    public const string ResendTicket = "resend-ticket";
    public const string AccountLinks = "account-links";
}
