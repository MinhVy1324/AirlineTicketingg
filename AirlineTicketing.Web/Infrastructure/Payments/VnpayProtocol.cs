using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AirlineTicketing.BUS;
using AirlineTicketing.Domain;

namespace AirlineTicketing.Web.Infrastructure.Payments;

public sealed record VnpayNotice(string Reference, long Amount, string TransactionNo, string ResponseCode, string Status);

public static class VnpayProtocol
{
    public const string PayEndpoint = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public const string ApiEndpoint = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
    public const string ReturnPath = "/dat-ve/vnpay/ket-qua";
    private const string Version = "2.1.0";
    private const string HashKey = "vnp_SecureHash";

    public static string NewReference() => Guid.NewGuid().ToString("N");
    public static string Date(DateTimeOffset value) => value.ToOffset(ChuyenBayBUS.VietnamOffset)
        .ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    public static bool TryAmount(decimal amount, out long minorUnits)
    {
        var scaled = amount * 100;
        var valid = scaled is > 0 and <= 999_999_999_999m && scaled == decimal.Truncate(scaled);
        minorUnits = valid ? (long)scaled : 0;
        return valid;
    }

    public static string CreatePayUrl(VnpayOptions options, ThanhToan payment, DateTimeOffset expiresAt, string ip)
    {
        if (!TryAmount(payment.SoTien, out var amount)) throw new ArgumentOutOfRangeException(nameof(payment));
        var fields = new Dictionary<string, string>
        {
            ["vnp_Version"] = Version, ["vnp_Command"] = "pay", ["vnp_TmnCode"] = options.MerchantCode,
            ["vnp_Amount"] = amount.ToString(CultureInfo.InvariantCulture), ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = payment.MaGiaoDich!, ["vnp_OrderInfo"] = $"AeroViet {payment.MaGiaoDich}",
            ["vnp_OrderType"] = "other", ["vnp_Locale"] = "vn", ["vnp_IpAddr"] = ip,
            ["vnp_ReturnUrl"] = new Uri(new Uri(options.PublicBaseUrl), ReturnPath).AbsoluteUri,
            ["vnp_CreateDate"] = Date(payment.NgayTao), ["vnp_ExpireDate"] = Date(expiresAt)
        };
        var data = CanonicalQuery(fields);
        return $"{PayEndpoint}?{data}&{HashKey}={Sign(options.HashSecret, data)}";
    }

    public static VnpayNotice? ReadNotice(VnpayOptions options, IQueryCollection query)
    {
        if (!options.Ready || query.Count > 30) return null;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, values) in query)
        {
            if (!key.StartsWith("vnp_", StringComparison.Ordinal)) continue;
            if (values.Count != 1 || values[0] is not { Length: <= 1024 } value) return null;
            fields.Add(key, value);
        }
        if (!Verify(options.HashSecret, CanonicalQuery(fields), fields.GetValueOrDefault(HashKey))
            || fields.GetValueOrDefault("vnp_TmnCode") != options.MerchantCode)
            return null;
        return ParseNotice(fields);
    }

    public static VnpayNotice? ParseNotice(IReadOnlyDictionary<string, string> fields)
    {
        var reference = fields.GetValueOrDefault("vnp_TxnRef") ?? "";
        var transaction = fields.GetValueOrDefault("vnp_TransactionNo") ?? "";
        var response = fields.GetValueOrDefault("vnp_ResponseCode") ?? "";
        var status = fields.GetValueOrDefault("vnp_TransactionStatus") ?? "";
        if (reference.Length is < 1 or > 100 || !reference.All(char.IsAsciiLetterOrDigit)
            || response.Length != 2 || !response.All(char.IsAsciiDigit)
            || status.Length != 2 || !status.All(char.IsAsciiDigit)
            || !long.TryParse(fields.GetValueOrDefault("vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
            return null;
        if (response == "00" && status == "00"
            && (transaction.Length is < 1 or > 15 || !transaction.All(char.IsAsciiDigit) || transaction.All(c => c == '0')))
            return null;
        return new(reference, amount, transaction, response, status);
    }

    public static string CanonicalQuery(IEnumerable<KeyValuePair<string, string>> fields) =>
        string.Join("&", fields
            .Where(field => field.Key is not ("vnp_SecureHash" or "vnp_SecureHashType") && field.Value.Length > 0)
            .OrderBy(field => field.Key, StringComparer.Ordinal)
            .Select(field => $"{WebUtility.UrlEncode(field.Key)}={WebUtility.UrlEncode(field.Value)}"));

    public static string PipeData(IReadOnlyDictionary<string, string> fields, IEnumerable<string> keys) =>
        string.Join("|", keys.Select(key => fields.GetValueOrDefault(key) ?? ""));

    public static string Sign(string secret, string data) => Convert.ToHexStringLower(
        HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data)));

    public static bool Verify(string secret, string data, string? signature)
    {
        if (signature?.Length != 128 || !signature.All(Uri.IsHexDigit)) return false;
        var expected = HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data));
        return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature));
    }
}
