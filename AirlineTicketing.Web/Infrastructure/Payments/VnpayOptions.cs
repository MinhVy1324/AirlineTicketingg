namespace AirlineTicketing.Web.Infrastructure.Payments;

public sealed class VnpayOptions
{
    public const string Provider = "VNPAY Sandbox";
    public bool Enabled { get; set; }
    public string MerchantCode { get; set; } = "";
    public string HashSecret { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public string ServerIp { get; set; } = "127.0.0.1";

    public bool Ready => Enabled && MerchantCode is { Length: 8 } && MerchantCode.All(char.IsAsciiLetterOrDigit)
        && !string.IsNullOrWhiteSpace(HashSecret) && System.Net.IPAddress.TryParse(ServerIp, out _)
        && Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var origin)
        && origin.Scheme == Uri.UriSchemeHttps && origin.UserInfo.Length == 0
        && origin.AbsolutePath == "/" && origin.Query.Length == 0 && origin.Fragment.Length == 0;
}
