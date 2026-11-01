using AirlineTicketing.Web.ViewModels;

namespace AirlineTicketing.Web.Demo;

/// <summary>
/// Static demo data copied from the Figma sample screens. Swap these factories for BUS/DAL
/// calls once the business layer is implemented; the views only depend on the view models.
/// </summary>
public static partial class DemoCatalog
{
    public const string CustomerName = "Minh Anh";
    public const string CustomerInitials = "MA";
    public const string CustomerEmail = "minhanh@example.com";
    public const string CustomerPhone = "+84 912 345 678";
    public const string Pnr = "AV8K2M";
    public const string TicketNumber = "738 2400012865";
    public const string TransactionCode = "TX0111260942";

    public const decimal BaseFare = 1_500_000m;
    public const decimal TaxesAndFees = 380_000m;
    public const decimal ServiceFee = 70_000m;
    public const decimal ExtraBaggageFee = 250_000m;
    public const decimal TotalAmount = BaseFare + TaxesAndFees + ServiceFee + ExtraBaggageFee;

    public static readonly AirportInfo HoChiMinh = new("SGN", "TP. Hồ Chí Minh", "Tân Sơn Nhất");
    public static readonly AirportInfo HaNoi = new("HAN", "Hà Nội", "Nội Bài");

    public static readonly FlightOffer PrimaryFlight = new(
        Id: "vn210",
        AirlineName: "Vietnam Airlines",
        FlightNumber: "VN210",
        Aircraft: "Airbus A321",
        FareName: "Phổ thông Tiêu chuẩn",
        From: HoChiMinh,
        To: HaNoi,
        Departure: new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.FromHours(7)),
        Arrival: new DateTimeOffset(2026, 11, 20, 10, 10, 0, TimeSpan.FromHours(7)),
        PricePerPassenger: 1_950_000m,
        Stops: "Bay thẳng",
        CabinBaggage: "Xách tay 12 kg",
        CheckedBaggage: "Ký gửi 23 kg",
        ChangeFee: 360_000m,
        RefundFee: 500_000m);

    public static readonly FlightOffer AfternoonFlight = PrimaryFlight with
    {
        Id = "vn216",
        FlightNumber = "VN216",
        Departure = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.FromHours(7)),
        Arrival = new DateTimeOffset(2026, 11, 20, 14, 10, 0, TimeSpan.FromHours(7)),
        PricePerPassenger = 2_150_000m
    };

    public static readonly FlightOffer EveningFlight = PrimaryFlight with
    {
        Id = "vn220",
        FlightNumber = "VN220",
        Departure = new DateTimeOffset(2026, 11, 20, 16, 0, 0, TimeSpan.FromHours(7)),
        Arrival = new DateTimeOffset(2026, 11, 20, 18, 10, 0, TimeSpan.FromHours(7)),
        PricePerPassenger = 2_450_000m
    };

    public static readonly IReadOnlyList<FlightOffer> Offers =
        new[] { PrimaryFlight, AfternoonFlight, EveningFlight };

    public static readonly IReadOnlyList<DayPriceChip> DayPrices = new[]
    {
        new DayPriceChip("19/11", 1_890_000m, false),
        new DayPriceChip("20/11", 1_950_000m, true),
        new DayPriceChip("21/11", 2_050_000m, false)
    };

    public static readonly FlightSearchCriteria DefaultCriteria = new();

    public static HomeViewModel Home() => new()
    {
        CustomerName = CustomerName,
        CustomerInitials = CustomerInitials,
        Search = new SearchFormModel(),
        Destinations = new[]
        {
            new DestinationCard("Hà Nội", "HAN", 1_950_000m, "dest-ha-noi.png"),
            new DestinationCard("Đà Nẵng", "DAD", 1_290_000m, "dest-da-nang.png"),
            new DestinationCard("Phú Quốc", "PQC", 1_150_000m, "dest-phu-quoc.png")
        }
    };

    public static FlightResultsViewModel Flights() => new()
    {
        Criteria = DefaultCriteria,
        Flights = new[]
        {
            new FlightCardModel(PrimaryFlight, Expanded: true, Highlighted: true),
            new FlightCardModel(AfternoonFlight),
            new FlightCardModel(EveningFlight)
        },
        DayPrices = DayPrices,
        TotalCount = 6
    };

    public static FlightResultsViewModel EmptyFlights() => new()
    {
        Criteria = DefaultCriteria with
        {
            PriceRangeText = "1.500.000 ₫ – 1.800.000 ₫"
        },
        Flights = Array.Empty<FlightCardModel>(),
        DayPrices = DayPrices,
        IsEmpty = true
    };

    public static PriceSummary Price(string title = "Chi tiết thanh toán") => new()
    {
        Title = title,
        BaseFare = BaseFare,
        Taxes = TaxesAndFees,
        ServiceFee = ServiceFee,
        BaggageFee = ExtraBaggageFee
    };

    public static BookingViewModel Passengers() => new()
    {
        Flight = PrimaryFlight,
        Criteria = DefaultCriteria,
        Price = Price(),
        Passenger = new PassengerFormModel(),
        SeatRows = BuildSeatRows()
    };

    public static BookingViewModel Checkout() => new()
    {
        Flight = PrimaryFlight,
        Criteria = DefaultCriteria,
        Price = Price("Tổng tiền thanh toán"),
        Passenger = new PassengerFormModel(),
        SeatRows = BuildSeatRows()
    };

    public static BookingViewModel Success() => new()
    {
        Flight = PrimaryFlight,
        Criteria = DefaultCriteria,
        Price = Price("Tổng tiền thanh toán"),
        Passenger = new PassengerFormModel(),
        SeatRows = BuildSeatRows()
    };

    // ---------------------------------------------------------------- P2 · vé & tra cứu

    public static TicketsViewModel Tickets(string? tab = null)
    {
        var active = tab?.Trim().ToLowerInvariant() switch
        {
            "done" => 1,
            "cancelled" => 2,
            _ => 0
        };

        var tabs = new[]
        {
            new TicketTab("Sắp khởi hành", 2, IsActive: active == 0),
            new TicketTab("Đã hoàn tất", 3, IsActive: active == 1),
            new TicketTab("Đã hủy / hoàn", 1, IsActive: active == 2)
        };

        var upcoming = new[]
        {
            new TicketListItem(
                Pnr: Pnr,
                StatusText: "Đã xuất vé",
                StatusTone: "success",
                RouteText: "TP. Hồ Chí Minh → Hà Nội",
                ScheduleText: "20/11/2026 · 08:00 · VN210",
                PassengerText: "1 người lớn",
                PriceText: "2.200.000 ₫",
                NoteText: "Đã thanh toán · Xem chi tiết →",
                NoteTone: "success",
                IsSelected: active == 0),
            new TicketListItem(
                Pnr: "AV9P4D",
                StatusText: "Chờ thanh toán",
                StatusTone: "warning",
                RouteText: "TP. Hồ Chí Minh → Đà Nẵng",
                ScheduleText: "05/12/2026 · 09:30 · VJ620",
                PassengerText: "1 người lớn",
                PriceText: "1.290.000 ₫",
                NoteText: "Giữ chỗ đến 10:00 · 01/11/2026",
                NoteTone: "warning",
                ActionText: "Tiếp tục thanh toán",
                ActionHref: "/dat-ve/thanh-toan")
        };

        var completed = new[]
        {
            new TicketListItem(
                Pnr: "AV4T9N",
                StatusText: "Đã hoàn tất",
                StatusTone: "success",
                RouteText: "TP. Hồ Chí Minh → Hà Nội",
                ScheduleText: "12/10/2026 · 09:00 · VN214",
                PassengerText: "1 người lớn",
                PriceText: "1.850.000 ₫",
                NoteText: "Chuyến đã hoàn tất · Xem chi tiết →",
                NoteTone: "success",
                IsSelected: active == 1),
            new TicketListItem(
                Pnr: "AV2B7K",
                StatusText: "Đã hoàn tất",
                StatusTone: "success",
                RouteText: "TP. Hồ Chí Minh → Đà Nẵng",
                ScheduleText: "28/09/2026 · 14:20 · VJ620",
                PassengerText: "1 người lớn",
                PriceText: "1.290.000 ₫",
                NoteText: "Chuyến đã hoàn tất · Xem chi tiết →",
                NoteTone: "success"),
            new TicketListItem(
                Pnr: "AV1D5R",
                StatusText: "Đã hoàn tất",
                StatusTone: "success",
                RouteText: "Hà Nội → TP. Hồ Chí Minh",
                ScheduleText: "03/08/2026 · 18:40 · VN245",
                PassengerText: "1 người lớn",
                PriceText: "1.720.000 ₫",
                NoteText: "Chuyến đã hoàn tất · Xem chi tiết →",
                NoteTone: "success")
        };

        var cancelled = new[]
        {
            new TicketListItem(
                Pnr: "AV3C6P",
                StatusText: "Đã hủy",
                StatusTone: "danger",
                RouteText: "TP. Hồ Chí Minh → Đà Nẵng",
                ScheduleText: "05/11/2026 · 07:30 · VN218",
                PassengerText: "1 người lớn",
                PriceText: "1.650.000 ₫",
                NoteText: "Đã hoàn tiền · Xem chi tiết →",
                NoteTone: "danger",
                IsSelected: active == 2)
        };

        var upcomingFlight = PrimaryFlight;
        var completedFlight = PrimaryFlight with
        {
            FlightNumber = "VN214",
            Departure = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.FromHours(7)),
            Arrival = new DateTimeOffset(2026, 10, 12, 11, 10, 0, TimeSpan.FromHours(7)),
            PricePerPassenger = 1_850_000m
        };
        var cancelledFlight = PrimaryFlight with
        {
            FlightNumber = "VN218",
            Departure = new DateTimeOffset(2026, 11, 5, 7, 30, 0, TimeSpan.FromHours(7)),
            Arrival = new DateTimeOffset(2026, 11, 5, 9, 40, 0, TimeSpan.FromHours(7)),
            PricePerPassenger = 1_650_000m
        };

        var detail = active switch
        {
            1 => new TicketDetailViewModel
            {
                Flight = completedFlight,
                Pnr = "AV4T9N",
                PassengerName = "NGUYEN MINH ANH",
                SeatCode = "16F",
                TicketNumber = "738 2400009147",
                BaggageText = "12 kg xách tay · 23 kg ký gửi",
                PaymentText = "Đã hoàn tất · 1.850.000 ₫",
                BookedAtText = "10/09/2026 · 14:20 (UTC+7)",
                CanManage = false
            },
            2 => new TicketDetailViewModel
            {
                Flight = cancelledFlight,
                Pnr = "AV3C6P",
                PassengerName = "NGUYEN MINH ANH",
                SeatCode = "12A",
                TicketNumber = "738 2400010094",
                BaggageText = "12 kg xách tay · 23 kg ký gửi",
                PaymentText = "Đã hoàn · 1.650.000 ₫",
                BookedAtText = "20/10/2026 · 10:05 (UTC+7)",
                CanManage = false
            },
            _ => new TicketDetailViewModel
            {
                Flight = upcomingFlight,
                Pnr = Pnr,
                PassengerName = "NGUYEN MINH ANH",
                SeatCode = "18A",
                TicketNumber = TicketNumber,
                BaggageText = "12 kg xách tay · 33 kg ký gửi",
                PaymentText = "Đã thanh toán · 2.200.000 ₫",
                BookedAtText = "01/11/2026 · 09:30 (UTC+7)"
            }
        };

        return new TicketsViewModel
        {
            Tabs = tabs,
            Tickets = active switch { 1 => completed, 2 => cancelled, _ => upcoming },
            Detail = detail
        };
    }

    public static SoldOutViewModel SoldOut() => new()
    {
        Criteria = DefaultCriteria,
        SelectedFlight = PrimaryFlight,
        SelectedPrice = PrimaryFlight.PricePerPassenger,
        Alternatives = new[] { AfternoonFlight, EveningFlight }
    };

    public static BookingOutcomeViewModel PaymentFailed() => new()
    {
        Booking = new BookingViewModel
        {
            Flight = PrimaryFlight,
            Criteria = DefaultCriteria,
            Price = Price(),
            Passenger = new PassengerFormModel(),
            HoldUntilText = "09:45 ngày 01/11/2026",
            HoldRemainingText = "Còn 5 phút"
        },
        UpdatedAtText = "09:40 ngày 01/11/2026",
        Statuses = new[]
        {
            new BookingStatusItem("Đặt chỗ", "Đang giữ chỗ"),
            new BookingStatusItem("Thanh toán", "Thất bại · Chưa thu tiền"),
            new BookingStatusItem("Vé", "Chưa xuất vé")
        }
    };

    public static BookingOutcomeViewModel Expired() => new()
    {
        Booking = new BookingViewModel
        {
            Flight = PrimaryFlight,
            Criteria = DefaultCriteria,
            Price = Price(),
            Passenger = new PassengerFormModel(),
            HoldUntilText = "09:45 ngày 01/11/2026",
            HoldRemainingText = "Đã hết hạn"
        },
        UpdatedAtText = "09:46 ngày 01/11/2026",
        Statuses = new[]
        {
            new BookingStatusItem("Đặt chỗ", "Hết hạn · Đã giải phóng"),
            new BookingStatusItem("Thanh toán", "Chưa thực hiện · 0 ₫"),
            new BookingStatusItem("Vé", "Chưa xuất vé")
        },
        EventsTitle = "Diễn tiến giữ chỗ",
        Events = new[]
        {
            new OutcomeEvent("09:30", "Tạo giữ chỗ", "01/11/2026 · Có hiệu lực đến 09:45."),
            new OutcomeEvent("09:45", "Hết hạn giữ chỗ", "Không nhận được thanh toán. Chỗ đã được giải phóng."),
            new OutcomeEvent("09:46", "Chưa có vé", "Không phát hành số vé cho đặt chỗ này.")
        }
    };

    public static BookingOutcomeViewModel Processing() => new()
    {
        Booking = new BookingViewModel
        {
            Flight = PrimaryFlight,
            Criteria = DefaultCriteria,
            Price = Price("Tổng đã thanh toán"),
            Passenger = new PassengerFormModel(),
            PaidAtText = "09:42 ngày 01/11/2026",
            HoldUntilText = "09:45 ngày 01/11/2026",
            HoldRemainingText = "Đã thanh toán"
        },
        UpdatedAtText = "09:42 ngày 01/11/2026",
        Statuses = new[]
        {
            new BookingStatusItem("Đặt chỗ", "Đã xác nhận"),
            new BookingStatusItem("Thanh toán", "Đã thanh toán"),
            new BookingStatusItem("Vé", "Đang xuất · Chưa có số vé")
        },
        EventsTitle = "Diễn tiến đặt vé",
        EventsSubtitle = "01/11/2026 · Giờ Việt Nam (UTC+7)",
        Events = new[]
        {
            new OutcomeEvent("09:30", "Đã giữ chỗ", "Giữ chỗ AV8K2M đến 09:45."),
            new OutcomeEvent("09:30", "Chờ thanh toán", "Số tiền cần thanh toán: 2.200.000 ₫."),
            new OutcomeEvent("09:42", "Đã thanh toán", "TX0111260942 · Visa ••••4242."),
            new OutcomeEvent("09:42", "Đang xuất vé", "Chờ hãng xác nhận phát hành. Chưa có số vé."),
            new OutcomeEvent("Chưa có", "Đã xuất vé", "Chưa hoàn tất. Chỉ nhận vé khi có số vé hợp lệ.")
        }
    };

    public static ScheduleChangedViewModel ScheduleChanged() => new()
    {
        Booking = new BookingViewModel
        {
            Flight = PrimaryFlight,
            Criteria = DefaultCriteria,
            Price = Price(),
            Passenger = new PassengerFormModel()
        },
        OldScheduleLabel = "Lịch cũ · 20/11/2026",
        OldScheduleText = "08:00 → 10:10",
        NewScheduleLabel = "Lịch mới · 20/11/2026",
        NewScheduleText = "09:30 → 11:40",
        DelayText = "Muộn hơn 1 giờ 30 phút",
        NoticeReason = "Lý do: điều chỉnh kế hoạch khai thác.",
        NoticeAppliedText = "Áp dụng cho VN210 ngày 20/11/2026 trên chặng TP. Hồ Chí Minh → Hà Nội."
    };

    public static CancelledFlightViewModel Cancelled() => new()
    {
        Booking = new BookingViewModel
        {
            Flight = PrimaryFlight with
            {
                FlightNumber = "VN220",
                Departure = new DateTimeOffset(2026, 11, 24, 16, 0, 0, TimeSpan.FromHours(7)),
                Arrival = new DateTimeOffset(2026, 11, 24, 18, 10, 0, TimeSpan.FromHours(7)),
                PricePerPassenger = 1_750_000m
            },
            Criteria = DefaultCriteria,
            Price = new PriceSummary
            {
                BaseFare = 1_750_000m,
                Taxes = TaxesAndFees,
                ServiceFee = ServiceFee,
                BaggageFee = ExtraBaggageFee
            },
            Passenger = new PassengerFormModel(),
            Pnr = "AV6H3T",
            TicketNumber = "738 2400013018",
            TotalText = "2.450.000 ₫"
        },
        TicketNumber = "738 2400013018",
        PaidText = "2.450.000 ₫",
        PaymentText = "Visa ••••4242"
    };

    public static LookupPageViewModel LookupFound() => LookupFound(new LookupFormModel());

    public static LookupPageViewModel Lookup(LookupFormModel form)
    {
        if (string.IsNullOrWhiteSpace(form.Pnr) || string.IsNullOrWhiteSpace(form.Email))
        {
            return new LookupPageViewModel
            {
                Form = form,
                ErrorMessage = "Vui lòng nhập mã đặt chỗ (PNR) và email liên hệ."
            };
        }

        var matches = string.Equals(form.Pnr.Trim(), Pnr, StringComparison.OrdinalIgnoreCase)
                      && string.Equals(form.Email.Trim(), CustomerEmail, StringComparison.OrdinalIgnoreCase);

        if (!matches)
        {
            return new LookupPageViewModel
            {
                Form = form,
                ErrorMessage = "Không tìm thấy đặt chỗ khớp mã PNR và email liên hệ. Vui lòng kiểm tra lại cả hai trường."
            };
        }

        return LookupFound(form);
    }

    private static LookupPageViewModel LookupFound(LookupFormModel form) => new()
    {
        Form = form,
        HasResult = true,
        Result = new LookupResultViewModel
        {
            Pnr = Pnr,
            Flight = PrimaryFlight,
            PassengerMasked = "NGUYEN M*** A** · Người lớn",
            IdentityMasked = "••••••••1234",
            PhoneMasked = "+84 ••• ••• 678",
            EmailMasked = "m•••••••••@example.com",
            SeatCode = "18A",
            BaggageText = "12 kg xách tay + 33 kg ký gửi",
            TicketNumber = TicketNumber,
            BookedAtText = "01/11/2026 · 09:30 (UTC+7)",
            Total = TotalAmount
        }
    };

    // ---------------------------------------------------------------- P3 · đổi / hoàn vé

    public static ChangeRequestViewModel ChangeRequest() => new()
    {
        Booking = new BookingViewModel
        {
            Flight = PrimaryFlight,
            Criteria = DefaultCriteria,
            Price = Price(),
            Passenger = new PassengerFormModel()
        },
        NewFlight = PrimaryFlight with
        {
            Id = "vn216-2211",
            FlightNumber = "VN216",
            Departure = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.FromHours(7)),
            Arrival = new DateTimeOffset(2026, 11, 22, 14, 10, 0, TimeSpan.FromHours(7))
        },
        NewBaseFare = 1_700_000m
    };

    public static RefundRequestViewModel RefundRequest() => new()
    {
        Booking = new BookingViewModel
        {
            Flight = PrimaryFlight,
            Criteria = DefaultCriteria,
            Price = Price(),
            Passenger = new PassengerFormModel()
        }
    };

    public static RequestTrackingViewModel Requests() => new()
    {
        Tabs = new[]
        {
            new TicketTab("Tất cả", 2, IsActive: true),
            new TicketTab("Đang xử lý", 2, IsActive: false),
            new TicketTab("Đã hoàn tất", 0, IsActive: false)
        },
        Requests = new[]
        {
            new RequestRow(
                Code: "RF-1031",
                Pnr: Pnr,
                TypeText: "Hoàn vé · 01/11/2026",
                RouteText: "VN210 · SGN → HAN / 20/11/2026 · 08:00",
                StatusText: "Đã duyệt · Chờ hoàn tiền",
                StatusTone: "warning",
                IsSelected: true),
            new RequestRow(
                Code: "CH-1032",
                Pnr: "AV3D9P",
                TypeText: "Đổi vé · 02/11/2026",
                RouteText: "VN118 · SGN → DAD / 06/12/2026 · 09:00",
                StatusText: "Chờ xác nhận phí",
                StatusTone: "warning")
        },
        UpdatedAtText = "02/11/2026",
        DetailStatusTone = "warning",
        Events = new[]
        {
            new OutcomeEvent("01/11 / 10:20", "Đã tiếp nhận", "Ghi nhận yêu cầu hoàn RF-1031."),
            new OutcomeEvent("01/11 / 14:00", "Đã kiểm tra điều kiện",
                "Vé chưa sử dụng, gửi trước giờ bay hơn 3 tiếng. Phí hoàn 500.000 ₫."),
            new OutcomeEvent("02/11 / 09:10", "Đã duyệt · Chờ hoàn tiền",
                "Đã tạo lệnh hoàn 1.380.000 ₫ về Visa ••••4242."),
            new OutcomeEvent("Chưa có", "Hoàn tiền chưa hoàn tất",
                "Chờ cổng thanh toán xác nhận. Dự kiến 7–15 ngày làm việc sau khi duyệt.")
        },
        PaidTotal = TotalAmount,
        RefundFee = PrimaryFlight.RefundFee,
        ServiceFee = ServiceFee,
        BaggageFee = ExtraBaggageFee,
        RefundToText = "Visa ••••4242"
    };

    public static BookingViewModel ReviewPassengers() => new()
    {
        Flight = PrimaryFlight,
        Criteria = DefaultCriteria,
        Price = Price(),
        Passenger = new PassengerFormModel
        {
            Email = "minhanh@example",
            IdentityNumber = ""
        },
        SeatRows = BuildSeatRows(),
        ShowValidationErrors = true
    };

    private static IReadOnlyList<SeatRow> BuildSeatRows()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal) { "17A", "17B", "17C", "19B", "19E" };
        const string selected = "18A";
        var letters = new[] { "A", "B", "C", "D", "E", "F" };
        var rows = new List<SeatRow>(4);

        for (var row = 17; row <= 20; row++)
        {
            var cells = new List<SeatCell>(letters.Length);
            foreach (var letter in letters)
            {
                var code = $"{row}{letter}";
                var state = code == selected
                    ? SeatState.Selected
                    : taken.Contains(code)
                        ? SeatState.Taken
                        : SeatState.Free;

                cells.Add(new SeatCell(letter, state));
            }

            rows.Add(new SeatRow(row.ToString(), cells));
        }

        return rows;
    }
}
