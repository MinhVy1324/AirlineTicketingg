using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using AirlineTicketing.Domain;
using Microsoft.Extensions.Options;

namespace AirlineTicketing.Web.Infrastructure.Payments;

public sealed class VnpayApiException(string message, string? field = null) : InvalidOperationException(message)
{
    public string? Field { get; } = field;
}

public sealed class VnpayClient(HttpClient http, IOptions<VnpayOptions> configuration, TimeProvider clock)
{
    private static readonly string[] QueryRequest = Keys("RequestId Version Command TmnCode TxnRef TransactionDate CreateDate IpAddr OrderInfo");
    private static readonly string[] RefundRequest = Keys("RequestId Version Command TmnCode TransactionType TxnRef Amount TransactionNo TransactionDate CreateBy CreateDate IpAddr OrderInfo");
    private static readonly string[] CommonResponse = Keys("ResponseId Command ResponseCode Message TmnCode TxnRef Amount BankCode PayDate TransactionNo TransactionType TransactionStatus OrderInfo");
    private static readonly string[] QueryResponse = [.. CommonResponse, "vnp_PromotionCode", "vnp_PromotionAmount"];
    private readonly VnpayOptions options = configuration.Value;

    public async Task<VnpayNotice> QueryAsync(ThanhToan payment, CancellationToken ct)
    {
        var fields = RequestFields(payment, "querydr", VnpayProtocol.NewReference(), "AeroViet truy van");
        var response = await SendAsync(fields, QueryRequest, QueryResponse, ct);
        if (response.GetValueOrDefault("vnp_ResponseCode") != "00")
            throw new VnpayApiException($"VNPAY chưa xác nhận truy vấn (mã {SafeCode(response, "vnp_ResponseCode")}).");
        if (response.GetValueOrDefault("vnp_TransactionType") != "01" || VnpayProtocol.ParseNotice(response) is not { } notice)
            throw new VnpayApiException("Kết quả truy vấn không phải giao dịch thanh toán hợp lệ.");
        return notice;
    }

    public async Task<(string Code, string Status, string? Number)> RefundAsync(HoanTien refund, string creator, CancellationToken ct)
    {
        var payment = refund.ThanhToan;
        var fields = RequestFields(payment, "refund", refund.MaGiaoDich!, $"AeroViet hoan {refund.MaGiaoDich}");
        if (!VnpayProtocol.TryAmount(refund.SoTien, out var amount))
            throw new VnpayApiException("Số tiền hoàn ngoài giới hạn VNPAY.");
        fields["vnp_TransactionType"] = refund.SoTien == payment.SoTien ? "02" : "03";
        fields["vnp_Amount"] = amount.ToString(CultureInfo.InvariantCulture);
        fields["vnp_TransactionNo"] = payment.MaGiaoDichNhaCungCap!;
        fields["vnp_CreateBy"] = creator;
        var response = await SendAsync(fields, RefundRequest, CommonResponse, ct);
        var code = SafeCode(response, "vnp_ResponseCode");
        var status = SafeCode(response, "vnp_TransactionStatus");
        if (code != "00") return (code, status, null);
        if (VnpayProtocol.ParseNotice(response) is not { } result || result.Amount != amount
            || response.GetValueOrDefault("vnp_TransactionType") != fields["vnp_TransactionType"]
            || response.GetValueOrDefault("vnp_OrderInfo") != fields["vnp_OrderInfo"])
            throw new VnpayApiException("Phản hồi hoàn tiền không khớp yêu cầu.");
        return (code, status, status == "00" ? result.TransactionNo : null);
    }

    private Dictionary<string, string> RequestFields(ThanhToan payment, string command, string requestId, string description)
    {
        if (!options.Ready || payment.MaDiemThanhToan != options.MerchantCode)
            throw new VnpayApiException("VNPAY Sandbox chưa được cấu hình cho điểm thanh toán này.");
        return new()
        {
            ["vnp_RequestId"] = requestId, ["vnp_Version"] = "2.1.0", ["vnp_Command"] = command,
            ["vnp_TmnCode"] = options.MerchantCode, ["vnp_TxnRef"] = payment.MaGiaoDich!,
            ["vnp_TransactionDate"] = VnpayProtocol.Date(payment.NgayTao),
            ["vnp_CreateDate"] = VnpayProtocol.Date(clock.GetUtcNow()), ["vnp_IpAddr"] = options.ServerIp,
            ["vnp_OrderInfo"] = description
        };
    }

    private async Task<Dictionary<string, string>> SendAsync(Dictionary<string, string> fields,
        string[] requestKeys, string[] responseKeys, CancellationToken ct)
    {
        fields["vnp_SecureHash"] = VnpayProtocol.Sign(options.HashSecret, VnpayProtocol.PipeData(fields, requestKeys));
        try
        {
            using var response = await http.PostAsJsonAsync(VnpayProtocol.ApiEndpoint, fields, ct);
            if (!response.IsSuccessStatusCode)
                throw new VnpayApiException("VNPAY chưa trả về kết quả có thể xác minh.");
            using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
            if (json is null || json.RootElement.ValueKind != JsonValueKind.Object)
                throw new VnpayApiException("Phản hồi VNPAY không đúng định dạng.");
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in json.RootElement.EnumerateObject())
            {
                if (!item.Name.StartsWith("vnp_", StringComparison.Ordinal)) continue;
                var value = item.Value.ValueKind switch
                {
                    JsonValueKind.String => item.Value.GetString() ?? "",
                    JsonValueKind.Number => item.Value.GetRawText(),
                    JsonValueKind.Null => "",
                    _ => throw new VnpayApiException("Phản hồi VNPAY không đúng định dạng.")
                };
                if (!result.TryAdd(item.Name, value))
                    throw new VnpayApiException("Phản hồi VNPAY có trường bị lặp.");
            }
            if (!VnpayProtocol.Verify(options.HashSecret, VnpayProtocol.PipeData(result, responseKeys),
                    result.GetValueOrDefault("vnp_SecureHash"))
                || result.GetValueOrDefault("vnp_Command") != fields["vnp_Command"]
                || result.GetValueOrDefault("vnp_TxnRef") != fields["vnp_TxnRef"]
                || result.GetValueOrDefault("vnp_TmnCode") != options.MerchantCode)
                throw new VnpayApiException("Không xác minh được chữ ký hoặc mã giao dịch VNPAY.");
            return result;
        }
        catch (Exception error) when (error is JsonException or HttpRequestException
            || error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new VnpayApiException("Chưa nhận được phản hồi hợp lệ từ VNPAY.");
        }
    }

    private static string SafeCode(IReadOnlyDictionary<string, string> values, string key) =>
        values.GetValueOrDefault(key) is { Length: 2 } code && code.All(char.IsAsciiDigit) ? code : "??";
    private static string[] Keys(string names) => names.Split(' ').Select(name => "vnp_" + name).ToArray();
}
