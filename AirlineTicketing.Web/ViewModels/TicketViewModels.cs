namespace AirlineTicketing.Web.ViewModels;

// --------------------------------------------------------------------- 07 · Vé của tôi

public sealed record TicketTab(string Label, int Count, bool IsActive);

public sealed record TicketListItem(
    string Pnr,
    string StatusText,
    string StatusTone,
    string RouteText,
    string ScheduleText,
    string PassengerText,
    string PriceText,
    string NoteText,
    string NoteTone,
    bool IsSelected = false,
    string? ActionText = null,
    string? ActionHref = null);

public sealed class TicketsViewModel
{
    public IReadOnlyList<TicketTab> Tabs { get; init; } = Array.Empty<TicketTab>();
    public IReadOnlyList<TicketListItem> Tickets { get; init; } = Array.Empty<TicketListItem>();
    public TicketDetailViewModel Detail { get; init; } = null!;
}

public sealed class TicketDetailViewModel
{
    public FlightOffer Flight { get; init; } = null!;
    public string Pnr { get; init; } = "AV8K2M";
    public string PassengerName { get; init; } = "NGUYEN MINH ANH";
    public string SeatCode { get; init; } = "18A";
    public string TicketNumber { get; init; } = "738 2400012865";
    public string BaggageText { get; init; } = "12 kg xách tay · 33 kg ký gửi";
    public string PaymentText { get; init; } = "Đã thanh toán · 2.200.000 ₫";
    public string BookedAtText { get; init; } = "01/11/2026 · 09:30 (UTC+7)";
    public decimal ChangeFee { get; init; } = 360_000m;
    public decimal RefundFee { get; init; } = 500_000m;
    public decimal ServiceFee { get; init; } = 70_000m;
    public decimal ExtraBaggageFee { get; init; } = 250_000m;
    public bool CanManage { get; init; } = true;
}

// --------------------------------------------------- 20 · Chuyến vừa hết chỗ

public sealed class SoldOutViewModel
{
    public FlightSearchCriteria Criteria { get; init; } = new();
    public FlightOffer SelectedFlight { get; init; } = null!;
    public decimal SelectedPrice { get; init; }
    public IReadOnlyList<FlightOffer> Alternatives { get; init; } = Array.Empty<FlightOffer>();
}

// ------------------------------------------- 22–24 · Các nhánh sau thanh toán

public sealed record BookingStatusItem(string Label, string Value);

public sealed record OutcomeEvent(string Time, string Title, string Text);

public sealed class BookingOutcomeViewModel
{
    public BookingViewModel Booking { get; init; } = null!;
    public string UpdatedAtText { get; init; } = "";
    public IReadOnlyList<BookingStatusItem> Statuses { get; init; } = Array.Empty<BookingStatusItem>();
    public string EventsTitle { get; init; } = "Diễn tiến đặt vé";
    public string? EventsSubtitle { get; init; }
    public IReadOnlyList<OutcomeEvent> Events { get; init; } = Array.Empty<OutcomeEvent>();
}

// ------------------------------------------- 25 · Chuyến bay đổi giờ

public sealed class ScheduleChangedViewModel
{
    public BookingViewModel Booking { get; init; } = null!;
    public string OldScheduleLabel { get; init; } = "Lịch cũ · 20/11/2026";
    public string OldScheduleText { get; init; } = "08:00 → 10:10";
    public string NewScheduleLabel { get; init; } = "Lịch mới · 20/11/2026";
    public string NewScheduleText { get; init; } = "09:30 → 11:40";
    public string DelayText { get; init; } = "Muộn hơn 1 giờ 30 phút";
    public string NoticeReason { get; init; } = "Lý do: điều chỉnh kế hoạch khai thác.";
    public string NoticeAppliedText { get; init; } =
        "Áp dụng cho VN210 ngày 20/11/2026 trên chặng TP. Hồ Chí Minh → Hà Nội.";
}

// ------------------------------------------- 26 · Chuyến bay bị hủy

public sealed class CancelledFlightViewModel
{
    public BookingViewModel Booking { get; init; } = null!;
    public string TicketNumber { get; init; } = "738 2400013018";
    public string PaidText { get; init; } = "2.450.000 ₫";
    public string PaymentText { get; init; } = "Visa ••••4242";
}

// ------------------------------------------- 27 · Tra cứu đặt chỗ

public sealed class LookupFormModel
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(30)]
    public string Pnr { get; set; } = "AV8K2M";
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = "minhanh@example.com";
}

public sealed class LookupPageViewModel
{
    public LookupFormModel Form { get; init; } = new();
    public bool HasResult { get; init; }
    public string? ErrorMessage { get; init; }
    public LookupResultViewModel? Result { get; init; }
}

public sealed class LookupResultViewModel
{
    public string Pnr { get; init; } = "AV8K2M";
    public string BookingStatus { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public string TicketStatus { get; init; } = string.Empty;
    public FlightOffer Flight { get; init; } = null!;
    public string PassengerMasked { get; init; } = "NGUYEN M*** A** · Người lớn";
    public string IdentityMasked { get; init; } = "••••••••1234";
    public string PhoneMasked { get; init; } = "+84 ••• ••• 678";
    public string EmailMasked { get; init; } = "m••••••@example.com";
    public string SeatCode { get; init; } = "18A";
    public string BaggageText { get; init; } = "12 kg xách tay + 33 kg ký gửi";
    public string TicketNumber { get; init; } = "738 2400012865";
    public string BookedAtText { get; init; } = "01/11/2026 · 09:30 (UTC+7)";
    public decimal Total { get; init; } = 2_200_000m;
}

// ------------------------------------------- 28 · Gửi yêu cầu đổi vé

public sealed class ChangeRequestViewModel
{
    public BookingViewModel Booking { get; init; } = null!;
    public FlightOffer NewFlight { get; init; } = null!;
    public decimal NewBaseFare { get; init; }
    public string SeatBaggageText { get; init; } = "18A · 12 kg xách tay + 23 kg ký gửi + thêm 10 kg";
    public string AvailabilityText { get; init; } = "Có chỗ tại thời điểm kiểm tra · 01/11/2026 10:15";
    public string AvailabilityNote { get; init; } =
        "Giá và chỗ là dự kiến, chưa giữ chỗ trên chuyến mới. Bộ phận vận hành sẽ xác nhận lại còn chỗ và mức phí trước khi thu tiền.";
    public string Reason { get; init; } = "Thay đổi kế hoạch cá nhân";

    public decimal NewTotal =>
        NewBaseFare + Booking.Price.Taxes + Booking.Price.ServiceFee + Booking.Price.BaggageFee;

    public decimal PriceDifference => NewTotal - Booking.Price.Total;
    public decimal ChangeFee => Booking.Flight.ChangeFee;
    public decimal TotalExtra => PriceDifference + ChangeFee;
}

// ------------------------------------------- 29 · Gửi yêu cầu hoàn vé

public sealed class RefundRequestViewModel
{
    public BookingViewModel Booking { get; init; } = null!;
    public string SeatBaggageText { get; init; } = "18A · 12 kg xách tay + 23 kg ký gửi + thêm 10 kg";
    public string RefundToText { get; init; } = "Visa ••••4242";
    public string DeadlineText { get; init; } =
        "Gửi trước giờ bay ít nhất 3 tiếng, muộn nhất 05:00 ngày 20/11/2026 (UTC+7). Vé phải chưa sử dụng. Không hoàn khi không có mặt.";
    public string Reason { get; init; } = "Thay đổi kế hoạch cá nhân";
    public string AdditionalInfo { get; init; } = "Tôi không còn nhu cầu thực hiện hành trình ngày 20/11.";

    public decimal RefundFee => Booking.Flight.RefundFee;
    public decimal ServiceFee => Booking.Price.ServiceFee;
    public decimal BaggageFee => Booking.Price.BaggageFee;
    public decimal RefundTotal => Booking.Price.Total - RefundFee - ServiceFee - BaggageFee;
}

// -------------------------------------- 30 · Theo dõi yêu cầu đổi / hoàn

public sealed record RequestRow(
    string Code,
    string Pnr,
    string TypeText,
    string RouteText,
    string StatusText,
    string StatusTone,
    bool IsSelected = false);

public sealed class RequestTrackingViewModel
{
    public IReadOnlyList<TicketTab> Tabs { get; init; } = Array.Empty<TicketTab>();
    public IReadOnlyList<RequestRow> Requests { get; init; } = Array.Empty<RequestRow>();
    public string UpdatedAtText { get; init; } = "02/11/2026";
    public string DetailCode { get; init; } = "RF-1031";
    public string DetailPnr { get; init; } = "AV8K2M";
    public string DetailPassenger { get; init; } = "NGUYEN MINH ANH";
    public string DetailTicketNumber { get; init; } = "738 2400012865";
    public string DetailStatusText { get; init; } = "Đã duyệt · Chờ hoàn tiền";
    public string DetailStatusTone { get; init; } = "warning";
    public string FlightText { get; init; } = "VN210 · SGN → HAN · 20/11/2026 08:00";
    public string ReasonText { get; init; } = "Thay đổi kế hoạch cá nhân";
    public IReadOnlyList<OutcomeEvent> Events { get; init; } = Array.Empty<OutcomeEvent>();
    public decimal PaidTotal { get; init; }
    public decimal RefundFee { get; init; }
    public decimal ServiceFee { get; init; }
    public decimal BaggageFee { get; init; }
    public string RefundToText { get; init; } = "Visa ••••4242";
    public string OtherCode { get; init; } = "CH-1032";
    public string OtherText { get; init; } =
        "AV3D9P · Vé 738 2400013124 · SGN → DAD. Yêu cầu đổi sang ngày 07/12/2026 đang chờ bạn xác nhận mức phí.";
    public string OtherStatusText { get; init; } = "Chờ xác nhận phí";

    public decimal RefundTotal => PaidTotal - RefundFee - ServiceFee - BaggageFee;
}
