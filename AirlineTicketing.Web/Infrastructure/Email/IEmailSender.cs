namespace AirlineTicketing.Web.Infrastructure.Email;

/// <summary>
/// Cổng gửi email của ứng dụng. Có hai cài đặt: SMTP thật khi cấu hình đầy đủ,
/// và hộp thư demo ghi ra đĩa khi chạy nội bộ.
/// </summary>
public interface IEmailSender
{
    /// <summary>Tên kênh gửi, hiển thị trong lịch sử đặt vé để dễ đối chiếu.</summary>
    string TenKenh { get; }

    Task GuiAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
