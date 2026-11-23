using System.Text;
using Microsoft.Extensions.Options;

namespace AirlineTicketing.Web.Infrastructure.Email;

/// <summary>
/// Kênh gửi dùng khi chưa cấu hình SMTP: ghi email ra tệp <c>.eml</c> trong thư mục
/// <c>Email:OutboxPath</c> và ghi log. Nhờ vậy luồng "gửi vé điện tử" vẫn kiểm thử
/// được end-to-end mà không cần tài khoản thư thật.
/// </summary>
public sealed class FileEmailSender(
    IOptions<EmailOptions> options,
    IWebHostEnvironment environment,
    ILogger<FileEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public string TenKenh => "Hộp thư demo";

    public async Task GuiAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var folder = Path.IsPathRooted(_options.OutboxPath)
            ? _options.OutboxPath
            : Path.Combine(environment.ContentRootPath, _options.OutboxPath);
        Directory.CreateDirectory(folder);

        var safeRecipient = new string(message.To
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' or '@' ? ch : '_')
            .ToArray());
        var name = message.DeliveryId is Guid id
            ? $"{safeRecipient}-{id:N}.eml"
            : $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{safeRecipient}-{Guid.NewGuid().ToString("N")[..6]}.eml";
        var path = Path.Combine(folder, name);

        var content = BuildMime(message,
            string.IsNullOrWhiteSpace(message.FromAddress) ? _options.FromAddress : message.FromAddress!,
            string.IsNullOrWhiteSpace(message.FromName) ? _options.FromName : message.FromName!);

        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }

        logger.LogInformation(
            "Email demo '{Subject}' cho {To} đã ghi vào {Path} ({Channel}).",
            message.Subject, message.To, path, TenKenh);
    }

    private static string BuildMime(EmailMessage message, string fromAddress, string fromName)
    {
        var builder = new StringBuilder();
        var boundary = $"aeroviet-{Guid.NewGuid():N}";

        builder.Append("From: ").Append(EncodeHeader(fromName))
            .Append(" <").Append(fromAddress).Append(">\r\n");
        builder.Append("To: ");
        if (!string.IsNullOrWhiteSpace(message.ToName))
        {
            builder.Append(EncodeHeader(message.ToName!)).Append(' ');
        }
        builder.Append('<').Append(message.To).Append(">\r\n");
        builder.Append("Subject: ").Append(EncodeHeader(message.Subject)).Append("\r\n");
        builder.Append("Date: ").Append(DateTimeOffset.UtcNow.ToString("r")).Append("\r\n");
        if (message.DeliveryId is Guid deliveryId)
            builder.Append("Message-ID: <").Append(deliveryId.ToString("N")).Append("@aeroviet.vn>\r\n");
        builder.Append("MIME-Version: 1.0\r\n");
        builder.Append("Content-Type: multipart/mixed; boundary=\"").Append(boundary).Append("\"\r\n\r\n");

        builder.Append("--").Append(boundary).Append("\r\n");
        builder.Append("Content-Type: text/html; charset=utf-8\r\n");
        builder.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        builder.Append(WrapBase64(Convert.ToBase64String(Encoding.UTF8.GetBytes(message.HtmlBody)))).Append("\r\n\r\n");

        if (message.Attachment is { } attachment)
        {
            builder.Append("--").Append(boundary).Append("\r\n");
            builder.Append("Content-Type: ").Append(attachment.ContentType)
                .Append("; name=\"").Append(attachment.FileName).Append("\"\r\n");
            builder.Append("Content-Disposition: attachment; filename=\"").Append(attachment.FileName).Append("\"\r\n");
            builder.Append("Content-Transfer-Encoding: base64\r\n\r\n");
            builder.Append(WrapBase64(Convert.ToBase64String(attachment.Content))).Append("\r\n\r\n");
        }

        builder.Append("--").Append(boundary).Append("--\r\n");
        return builder.ToString();
    }

    private static string EncodeHeader(string value) =>
        $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}?=";

    private static string WrapBase64(string value)
    {
        const int lineLength = 76;
        var builder = new StringBuilder(value.Length + value.Length / lineLength * 2);
        for (var index = 0; index < value.Length; index += lineLength)
        {
            builder.Append(value, index, Math.Min(lineLength, value.Length - index)).Append("\r\n");
        }
        return builder.ToString().TrimEnd('\r', '\n');
    }
}
