namespace AirlineTicketing.Web.Infrastructure.Email;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>Một email giao dịch (vé điện tử, thông báo) cần gửi cho khách.</summary>
public sealed record EmailMessage
{
    public required string To { get; init; }
    public string? ToName { get; init; }
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public string? TextBody { get; init; }
    public EmailAttachment? Attachment { get; init; }

    /// <summary>Địa chỉ người gửi riêng cho email này (ví dụ notification.sender_email).</summary>
    public string? FromAddress { get; init; }
    public string? FromName { get; init; }
}
