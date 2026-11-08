namespace AirlineTicketing.Web.ViewModels;

public sealed record AirportInfo(string Code, string City, string Name)
{
    public string Display => $"{City} ({Code})";
}

/// <summary>Static demo flight used by the customer flow (mirrors the Figma sample data).</summary>
public sealed record FlightOffer(
    string Id,
    string AirlineName,
    string FlightNumber,
    string Aircraft,
    string FareName,
    AirportInfo From,
    AirportInfo To,
    DateTimeOffset Departure,
    DateTimeOffset Arrival,
    decimal PricePerPassenger,
    string Stops,
    string CabinBaggage,
    string CheckedBaggage,
    decimal ChangeFee,
    decimal RefundFee)
{
    public TimeSpan Duration => Arrival - Departure;
}

public sealed record FlightSearchCriteria
{
    public string OriginCity { get; init; } = "TP. Hồ Chí Minh";
    public string OriginCode { get; init; } = "SGN";
    public string DestinationCity { get; init; } = "Hà Nội";
    public string DestinationCode { get; init; } = "HAN";
    public string DepartDateText { get; init; } = "20/11/2026";
    public string TripTypeText { get; init; } = "Một chiều";
    public int Passengers { get; init; } = 1;
    public string Cabin { get; init; } = "Phổ thông";
    public string PriceRangeText { get; init; } = "1.500.000 ₫ – 4.000.000 ₫";

    public string RouteText => $"{OriginCity} ({OriginCode}) → {DestinationCity} ({DestinationCode})";
    public string ShortRouteText => $"{OriginCity} → {DestinationCity}";
    public string SummaryText => $"{TripTypeText} · {DepartDateText} · {Passengers} người lớn · {Cabin}";
}

public sealed class SearchFormModel
{
    public string From { get; set; } = "TP. Hồ Chí Minh (SGN)";
    public string To { get; set; } = "Hà Nội (HAN)";
    public string DepartDate { get; set; } = "20/11/2026";
    public string ReturnDate { get; set; } = "";
    public string Passengers { get; set; } = "1 người lớn";
    public string Cabin { get; set; } = "Phổ thông";
    public string Trip { get; set; } = "oneway";
}

public sealed record DestinationCard(string Name, string Code, decimal FromPrice, string ImageFile);

public sealed class HomeViewModel
{
    public string CustomerName { get; init; } = "Khách hàng";
    public string CustomerInitials { get; init; } = "KH";
    public SearchFormModel Search { get; init; } = new();
    public IReadOnlyList<DestinationCard> Destinations { get; init; } = Array.Empty<DestinationCard>();
}

public sealed class FlightResultsViewModel
{
    public FlightSearchCriteria Criteria { get; init; } = new();
    public IReadOnlyList<FlightCardModel> Flights { get; init; } = Array.Empty<FlightCardModel>();
    public IReadOnlyList<DayPriceChip> DayPrices { get; init; } = Array.Empty<DayPriceChip>();
    public bool IsEmpty { get; init; }
    public string EmptyTitle { get; init; } = "Không có chuyến phù hợp bộ lọc";
    public string EmptyText { get; init; } =
        "Chưa tìm thấy chuyến Vietnam Airlines bay buổi sáng trong khoảng giá đã chọn. " +
        "Bạn có thể mở rộng khoảng giá, bỏ bộ lọc hoặc đổi ngày bay.";

    public int ResultCount => Flights.Count;

    /// <summary>Total flights available on the route (the demo only materialises three cards).</summary>
    public int TotalCount { get; init; }
}

public sealed record DayPriceChip(string DateText, decimal Price, bool IsActive);

public sealed class PassengerFormModel
{
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string Gender { get; set; } = "Nữ";
    public string DateOfBirth { get; set; } = string.Empty;
    public string Nationality { get; set; } = "Việt Nam";
    public string IdentityNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public string FullName => $"{LastName} {FirstName}".Trim();
}

public sealed class PriceSummary
{
    public string Title { get; init; } = "Chi tiết thanh toán";
    public string BaseFareLabel { get; init; } = "Giá vé cơ bản";
    public decimal BaseFare { get; init; }
    public decimal Taxes { get; init; }
    public decimal ServiceFee { get; init; }
    public decimal BaggageFee { get; init; }
    public string BaggageLabel { get; init; } = "Hành lý thêm 10 kg";
    public string TotalLabel { get; init; } = "Tổng thanh toán";
    public string? Note { get; init; } = "Giá đã bao gồm thuế, phí · Thanh toán bằng VND";

    public decimal Total => BaseFare + Taxes + ServiceFee + BaggageFee;
}

public sealed record FlightTimelineModel(
    FlightOffer Flight,
    bool ShowAirportName = false,
    bool Compact = false,
    string? MidFooter = null);

public sealed record FlightCardModel(FlightOffer Flight, bool Expanded = false, bool Highlighted = false);

public sealed class BookingViewModel
{
    public FlightOffer Flight { get; init; } = null!;
    public FlightSearchCriteria Criteria { get; init; } = new();
    public PriceSummary Price { get; init; } = new();
    public PassengerFormModel Passenger { get; init; } = new();
    public string SeatCode { get; init; } = "18A";
    public string SeatPosition { get; init; } = "Cạnh cửa sổ";
    public IReadOnlyList<SeatRow> SeatRows { get; init; } = Array.Empty<SeatRow>();
    public string ExtraBaggage { get; init; } = "+10 kg";
    public string PaymentMethod { get; init; } = "Thẻ ngân hàng";
    public string CardBrand { get; init; } = "Visa";
    public string CardLast4 { get; init; } = "4242";
    public string Pnr { get; init; } = "AV8K2M";
    public string TicketNumber { get; init; } = "738 2400012865";
    public string TransactionCode { get; init; } = "TX0111260942";
    public string HoldUntilText { get; init; } = "09:45 ngày 01/11/2026";
    public string HoldRemainingText { get; init; } = "Còn 12 phút";
    public string CreatedAtText { get; init; } = "09:30 ngày 01/11/2026";
    public string IssuedAtText { get; init; } = "01/11/2026 · 09:43";
    public string PaidAtText { get; init; } = "09:42 ngày 01/11/2026";
    public string Email { get; init; } = "minhanh@example.com";
    public string Phone { get; init; } = "+84 912 345 678";
    public string TotalText { get; init; } = "2.200.000 ₫";

    /// <summary>Frame 21: the passenger step rendered with validation errors.</summary>
    public bool ShowValidationErrors { get; init; }
}

public sealed record SeatCell(string Label, SeatState State);

public enum SeatState
{
    Free,
    Selected,
    Taken
}

public sealed record SeatRow(string Number, IReadOnlyList<SeatCell> Seats);

public sealed class LoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; } = true;
    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }
}
