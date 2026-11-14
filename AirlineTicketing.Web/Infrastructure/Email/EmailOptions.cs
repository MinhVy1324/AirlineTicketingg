namespace AirlineTicketing.Web.Infrastructure.Email;

/// <summary>
/// Cấu hình gửi email, đọc từ section <c>Email</c> trong appsettings.json hoặc
/// biến môi trường <c>Email__*</c>. Khi chưa cấu hình SMTP, hệ thống ghi email
/// ra hộp thư demo trên đĩa để vẫn kiểm thử được luồng gửi vé.
/// </summary>
public sealed class EmailOptions
{
    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseStartTls { get; init; } = true;
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromAddress { get; init; } = "no-reply@aeroviet.vn";
    public string FromName { get; init; } = "AeroViet";

    /// <summary>Thư mục lưu email demo, tương đối so với thư mục chạy ứng dụng.</summary>
    public string OutboxPath { get; init; } = "App_Data/outbox";

    public bool CoCauHinhSmtp =>
        Enabled
        && !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(FromAddress);
}
