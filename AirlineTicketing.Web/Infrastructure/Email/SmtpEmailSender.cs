using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace AirlineTicketing.Web.Infrastructure.Email;

/// <summary>
/// Gửi email qua máy chủ SMTP khi <c>Email:Enabled</c> bật và có <c>Email:Host</c>.
/// Không dùng cho production quy mô lớn (SmtpClient không có hàng đợi/retry),
/// nhưng đủ cho đồ án và có thể thay bằng dịch vụ giao vận khi cần.
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public string TenKenh => $"SMTP {_options.Host}";

    public async Task GuiAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var fromAddress = string.IsNullOrWhiteSpace(message.FromAddress)
            ? _options.FromAddress
            : message.FromAddress!;
        var fromName = string.IsNullOrWhiteSpace(message.FromName) ? _options.FromName : message.FromName!;

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 20_000
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName, Encoding.UTF8),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.HtmlBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };

        mail.To.Add(new MailAddress(message.To, message.ToName, Encoding.UTF8));

        if (message.Attachment is { } attachment)
        {
            mail.Attachments.Add(new Attachment(
                new MemoryStream(attachment.Content), attachment.FileName, attachment.ContentType));
        }

        await client.SendMailAsync(mail, cancellationToken);
        logger.LogInformation("Đã gửi email '{Subject}' tới {To} qua {Host}.",
            message.Subject, message.To, _options.Host);
    }
}
