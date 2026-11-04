using System.Globalization;
using AirlineTicketing.BUS;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.ViewModels;

namespace AirlineTicketing.Web.Infrastructure;

/// <summary>Chuyển DTO nghiệp vụ sang view model của tầng Web.</summary>
public static class FlightMapping
{
    public const decimal TaxesPerPassenger = 380_000m;
    public const decimal ServiceFeePerPassenger = 70_000m;
    public const decimal ChangeFee = 360_000m;
    public const decimal RefundFee = 500_000m;
    public const string CabinBaggage = "Xách tay 12 kg";
    public const string CheckedBaggage = "Ký gửi 23 kg";

    public static DateTimeOffset ToVietnamTime(DateTimeOffset value) =>
        value.ToOffset(ChuyenBayBUS.VietnamOffset);

    public static AirportInfo ToAirport(string code, string city, string name) => new(code, city, name);

    public static FlightOffer ToOffer(ChuyenBayDTO dto, string fareName = "Phổ thông Tiêu chuẩn") => new(
        Id: dto.Id.ToString(CultureInfo.InvariantCulture),
        AirlineName: AirlineNameFor(dto.SoHieu),
        FlightNumber: dto.SoHieu,
        Aircraft: dto.TenMayBay,
        FareName: fareName,
        From: ToAirport(dto.MaSanBayDi, dto.ThanhPhoDi, dto.TenSanBayDi),
        To: ToAirport(dto.MaSanBayDen, dto.ThanhPhoDen, dto.TenSanBayDen),
        Departure: ToVietnamTime(dto.GioKhoiHanh),
        Arrival: ToVietnamTime(dto.GioHaCanh),
        PricePerPassenger: dto.GiaCoBan,
        Stops: "Bay thẳng",
        CabinBaggage: CabinBaggage,
        CheckedBaggage: CheckedBaggage,
        ChangeFee: ChangeFee,
        RefundFee: RefundFee);

    public static FlightCardModel ToCard(ChuyenBayDTO dto, bool highlighted = false) =>
        new(ToOffer(dto), Expanded: highlighted, Highlighted: highlighted);

    public static string AirlineNameFor(string flightNumber)
    {
        var prefix = new string(flightNumber
            .Trim()
            .TakeWhile(char.IsLetterOrDigit)
            .Take(2)
            .ToArray())
            .ToUpperInvariant();

        return prefix switch
        {
            "VN" => "Vietnam Airlines",
            "VJ" => "Vietjet Air",
            "QH" => "Bamboo Airways",
            "BL" => "Pacific Airlines",
            "VU" => "Vietravel Airlines",
            "0V" => "VASCO",
            _ => "AeroViet"
        };
    }

    public static bool TryParseDate(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy" };
        return DateOnly.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    public static int ParsePassengers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 1;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? Math.Clamp(count, 1, 9)
            : 1;
    }

    public static string ParseCabin(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Phổ thông" : value.Trim();

    public static string ParseTrip(string? value) =>
        string.Equals(value, "roundtrip", StringComparison.OrdinalIgnoreCase) ? "Khứ hồi" : "Một chiều";

    public static string DisplayDate(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string DisplayDate(DateTimeOffset value) => Money.Date(ToVietnamTime(value));

    public static string DisplayTime(DateTimeOffset value) => Money.Time(ToVietnamTime(value));

    public static string SeatStateText(string state) => state switch
    {
        "DaBan" => "Đã bán",
        "DangGiu" => "Đang giữ",
        _ => "Trống"
    };
}
