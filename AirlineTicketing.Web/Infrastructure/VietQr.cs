using System.Globalization;
using System.Text;

namespace AirlineTicketing.Web.Infrastructure;

/// <summary>
/// Tạo nội dung QR chuyển khoản theo chuẩn VietQR (EMVCo). Thông tin ngân hàng là
/// dữ liệu minh họa của AeroViet, không phải tài khoản thật.
/// </summary>
public static class VietQr
{
    public const string BankBin = "970436"; // Vietcombank
    public const string BankName = "Vietcombank";
    public const string AccountNumber = "0123456789";
    public const string AccountName = "CONG TY AEROVIET";

    public static string BuildPayload(decimal amount, string memo, string accountNumber = AccountNumber)
    {
        var transferInfo = Tlv("00", BankBin) + Tlv("01", accountNumber);
        var consumer = Tlv("00", "A000000727")
            + Tlv("01", transferInfo)
            + Tlv("02", "QRIBFTTA");

        var payload = new StringBuilder()
            .Append(Tlv("00", "01"))
            .Append(Tlv("01", "12"))
            .Append(Tlv("38", consumer))
            .Append(Tlv("53", "704"))
            .Append(Tlv("54", decimal.Round(amount, 0, MidpointRounding.AwayFromZero)
                .ToString("0", CultureInfo.InvariantCulture)))
            .Append(Tlv("58", "VN"))
            .Append(Tlv("59", "AEROVIET"))
            .Append(Tlv("60", "HO CHI MINH"))
            .Append(Tlv("62", Tlv("08", NormalizeMemo(memo))))
            .ToString();

        return payload + "6304" + Crc16(payload + "6304");
    }

    private static string NormalizeMemo(string memo)
    {
        var value = (memo ?? string.Empty).Trim().ToUpperInvariant();
        return value.Length <= 25 ? value : value[..25];
    }

    private static string Tlv(string tag, string value) =>
        tag + value.Length.ToString("D2", CultureInfo.InvariantCulture) + value;

    /// <summary>CRC-16/CCITT-FALSE, poly 0x1021, init 0xFFFF.</summary>
    private static string Crc16(string value)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (ushort)((crc & 0x8000) != 0
                    ? (crc << 1) ^ 0x1021
                    : crc << 1);
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }
}
