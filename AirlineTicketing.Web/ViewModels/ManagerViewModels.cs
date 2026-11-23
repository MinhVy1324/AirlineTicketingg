using AirlineTicketing.Web.Infrastructure;

namespace AirlineTicketing.Web.ViewModels;

// ----------------------------------------------------------- manager chrome

public sealed record ManagerNavItem(string Key, string Label, string EnglishLabel, string Icon, string Href)
{
    public string DisplayLabel => UiLanguage.T(Label, EnglishLabel);
}

public static class ManagerNav
{
    public const string WorkspaceName = "Điều hành khai thác";
    public const string ScopeTitle = "Quyền vận hành được cấp";
    public const string ScopeText = "Không cấp quyền hoặc thay đổi cấu hình bảo mật.";

    public static readonly IReadOnlyList<ManagerNavItem> Items = new[]
    {
        new ManagerNavItem("overview", "Tổng quan vận hành", "Operations overview", "layout-dashboard", "/quan-ly"),
        new ManagerNavItem("schedules", "Lịch bay & tồn chỗ", "Flight schedules & availability", "calendar", "/quan-ly/lich-bay"),
        new ManagerNavItem("disruptions", "Biến động chuyến bay", "Flight disruptions", "alert", "/quan-ly/bien-dong-chuyen-bay"),
        new ManagerNavItem("bookings", "Quản lý đặt vé", "Booking management", "ticket", "/quan-ly/dat-ve"),
        new ManagerNavItem("changes", "Xử lý đổi / hoàn", "Ticket changes & refunds", "refresh", "/quan-ly/doi-hoan"),
        new ManagerNavItem("vnpay", "Đối soát thanh toán", "Payment reconciliation", "credit-card", "/quan-ly/thanh-toan-vnpay"),
        new ManagerNavItem("reports", "Báo cáo kinh doanh", "Business reports", "chart", "/quan-ly/bao-cao")
    };

    public static string LabelFor(string? key) =>
        (Items.FirstOrDefault(item => item.Key == key) ?? Items[0]).DisplayLabel;
}

public sealed record ManagerKpi(string Label, string Value, string Meta, string Tone, string Icon);

// ------------------------------------------------------- 14 · tổng quan

public sealed record ManagerFlightRow(
    string Flight,
    string Time,
    string Seats,
    string Condition,
    string Tone = "neutral");

public sealed record ManagerHoldRow(string Pnr, string Customer, string Total, string Expires);

public sealed record ManagerWorkloadRow(string Label, string BadgeText, int Percent, string Tone);

public sealed record ManagerChecklistItem(string Text, bool IsDone);

public sealed class ManagerOverviewViewModel
{
    public string Subtitle { get; init; } =
        "Chủ nhật, 01/11/2026 · Theo dõi chuyến bay, đặt chỗ và yêu cầu trong ngày.";
    public string ShiftText { get; init; } = UiLanguage.T("Ca sáng · 06:00–14:00", "Morning shift · 06:00–14:00");
    public string UpdatedText { get; init; } = "Cập nhật lúc 10:00 · Giờ Việt Nam (UTC+7)";

    public IReadOnlyList<ManagerKpi> Kpis { get; init; } = Array.Empty<ManagerKpi>();

    public string AttentionTitle { get; init; } = "2 chuyến cần chú ý trong ca trực";
    public string AttentionText { get; init; } =
        "VN216: thay đổi giờ khởi hành +30 phút. VJ640: còn 4 ghế. " +
        "Kiểm tra đặt chỗ liên quan trước khi cập nhật.";

    public string FlightsTitle { get; init; } = UiLanguage.T("Chuyến cần chú ý", "Flights needing attention");
    public string FlightsSubtitle { get; init; } = "Lịch khai thác ngày 01/11/2026";
    public string FlightsLinkText { get; init; } = UiLanguage.T("Xem lịch bay", "View schedules");
    public string FlightsLinkHref { get; init; } = "/quan-ly/lich-bay";
    public IReadOnlyList<ManagerFlightRow> Flights { get; init; } = Array.Empty<ManagerFlightRow>();
    public string FlightsNote { get; init; } =
        UiLanguage.T("Giờ hiển thị là lịch mới nhất. Thay đổi lịch phải gửi thông báo cho hành khách.",
            "Times reflect the latest schedule. Notify passengers about schedule changes.");

    public string WorkloadTitle { get; init; } = UiLanguage.T("Khối lượng xử lý", "Workload");
    public IReadOnlyList<ManagerWorkloadRow> Workload { get; init; } = Array.Empty<ManagerWorkloadRow>();
    public string WorkloadDetailLabel { get; init; } = UiLanguage.T("Chờ đối soát thanh toán", "Payments awaiting reconciliation");
    public string WorkloadDetailValue { get; init; } = "3 giao dịch";
    public string WorkloadButtonText { get; init; } = UiLanguage.T("Mở hàng đợi yêu cầu", "Open request queue");
    public string WorkloadButtonHref { get; init; } = "/quan-ly/doi-hoan";

    public string HoldsTitle { get; init; } = UiLanguage.T("Đặt chỗ chờ thanh toán", "Bookings awaiting payment");
    public string HoldsLinkText { get; init; } = UiLanguage.T("Quản lý đặt vé", "Booking management");
    public string HoldsLinkHref { get; init; } = "/quan-ly/dat-ve";
    public IReadOnlyList<ManagerHoldRow> Holds { get; init; } = Array.Empty<ManagerHoldRow>();

    public string ChecklistTitle { get; init; } = UiLanguage.T("Checklist ca trực", "Shift checklist");
    public IReadOnlyList<ManagerChecklistItem> Checklist { get; init; } = Array.Empty<ManagerChecklistItem>();
    public string SafetyTitle { get; init; } = UiLanguage.T("Xuất vé an toàn", "Ticket issuance checks");
    public string SafetyText { get; init; } = UiLanguage.T("Chỉ xuất vé sau khi thanh toán đã được xác nhận.", "Issue tickets after payment is confirmed.");
}

// -------------------------------------------------- 15 · lịch bay

public sealed record ManagerScheduleRow(
    string Flight,
    string Route,
    string Time,
    string StatusText,
    string StatusTone,
    string Seats,
    string Price,
    bool IsSelected = false);

public sealed record ManagerFareRow(string Cabin, string Capacity, string Sold, string BasePrice);

public sealed class ManagerSchedulesViewModel
{
    public static string StatusLabel(string value) => value switch
    {
        "Tất cả" => UiLanguage.T(value, "All"),
        "Mở bán" or "Đang mở bán" => UiLanguage.T(value, "On sale"),
        "Hoàn tất" => UiLanguage.T(value, "Completed"),
        "Tạm ngừng mở bán" => UiLanguage.T(value, "Sales paused"),
        _ => UiLanguage.Label(value)
    };

    public string Subtitle { get; init; } =
        "Quản lý lịch khai thác và giá theo hạng. Không thay đổi dữ liệu danh mục nền.";
    public string AddText { get; init; } = UiLanguage.T("Thêm lịch khai thác", "Add flight schedule");

    public string DateLabel { get; init; } = UiLanguage.T("Ngày khởi hành", "Departure date");
    public string DateValue { get; init; } = "20/11/2026";
    public string RouteLabel { get; init; } = UiLanguage.T("Tuyến bay", "Route");
    public string RouteValue { get; init; } = "SGN → HAN";
    public string AirlineStatusLabel { get; init; } = UiLanguage.T("Hãng / Trạng thái", "Airline / Status");
    public string AirlineStatusValue { get; init; } = "Tất cả";
    public string SearchText { get; init; } = UiLanguage.T("Tìm chuyến", "Find flights");

    public string ListTitle { get; init; } = "Lịch khai thác · 20/11/2026";
    public string ListSubtitle { get; init; } =
        UiLanguage.T("Giờ Việt Nam (UTC+7) · Tồn chỗ = sức chứa − đã bán − đang giữ", "Vietnam time (UTC+7) · Available seats = capacity − sold − held");
    public IReadOnlyList<ManagerScheduleRow> Rows { get; init; } = Array.Empty<ManagerScheduleRow>();
    public string ResultText { get; init; } = "Hiển thị 1–4 trong 18 chuyến cùng tuyến";

    public string EditorTitle { get; init; } = "Chỉnh sửa chuyến · VN210";
    public string EditorSubtitle { get; init; } = "20/11/2026 · Vietnam Airlines · Đã có 147 ghế bán";
    public string EditorDateLabel { get; init; } = UiLanguage.T("Ngày bay", "Flight date");
    public string EditorDateValue { get; init; } = "20/11/2026";
    public string EditorTimeLabel { get; init; } = UiLanguage.T("Giờ đi / đến", "Departure / arrival time");
    public string EditorTimeValue { get; init; } = "08:00 / 10:10";
    public string EditorStatusLabel { get; init; } = UiLanguage.T("Trạng thái", "Status");
    public string EditorStatusValue { get; init; } = "Mở bán";
    public IReadOnlyList<ManagerFareRow> Fares { get; init; } = Array.Empty<ManagerFareRow>();
    public string EconomyPriceLabel { get; init; } = UiLanguage.T("Giá cơ bản · Phổ thông", "Base fare · Economy");
    public string EconomyPriceValue { get; init; } = "1.500.000 VND";
    public string BusinessPriceLabel { get; init; } = UiLanguage.T("Giá cơ bản · Thương gia", "Base fare · Business");
    public string BusinessPriceValue { get; init; } = "4.500.000 VND";
    public string EditorNote { get; init; } =
        "Giá cơ bản chưa gồm thuế/phí. Giá bán Phổ thông Tiêu chuẩn: 1.950.000 ₫. " +
        "Số ghế đang giữ: 0 · Còn trống: 33.";
    public string CancelEditText { get; init; } = UiLanguage.T("Hủy chỉnh sửa", "Cancel editing");
    public string SaveText { get; init; } = UiLanguage.T("Lưu lịch & giá", "Save schedule and fare");

    public string RulesTitle { get; init; } = UiLanguage.T("Quy tắc cập nhật", "Update rules");
    public string ChangeTitle { get; init; } = UiLanguage.T("Đổi lịch phải thông báo", "Notify passengers of schedule changes");
    public string ChangeText { get; init; } =
        UiLanguage.T("Nếu đổi ngày hoặc giờ, bắt buộc gửi email đến toàn bộ hành khách có đặt chỗ.",
            "Send an email to every passenger with a booking when the date or time changes.");
    public bool EmailChecked { get; init; } = true;
    public string ReasonLabel { get; init; } = UiLanguage.T("Lý do thay đổi", "Reason for change");
    public string ReasonPlaceholder { get; init; } = UiLanguage.T("Nhập lý do nếu đổi lịch", "Enter a reason when rescheduling");
    public string RulesNote { get; init; } =
        UiLanguage.T("Không giảm sức chứa dưới số ghế đã bán và đang giữ. Giá mới chỉ áp dụng cho đặt chỗ mới.",
            "Capacity must cover sold and held seats. New fares apply to new bookings.");
    public string ThresholdTitle { get; init; } = UiLanguage.T("Ngưỡng giá được cấp", "Allowed fare range");
    public string ThresholdText { get; init; } =
        UiLanguage.T("Phổ thông: 900.000–3.500.000 ₫. Ngoài ngưỡng cần Admin phê duyệt.",
            "Economy: 900.000–3.500.000 ₫. Fares outside this range require administrator approval.");

    public bool ShowCancelDialog { get; init; }
    public ManagerCancelFlightDialogViewModel CancelDialog { get; init; } = new();

    public bool ShowPriceDialog { get; init; }
    public ManagerPriceDialogViewModel PriceDialog { get; init; } = new();
}

public sealed class ManagerCancelFlightDialogViewModel
{
    public string Title { get; init; } = "Xác nhận hủy chuyến VN210?";
    public string Subtitle { get; init; } = "Lịch bay & tồn chỗ / VN210 · Hiện đang mở bán";
    public string FlightLabel { get; init; } = UiLanguage.T("Chuyến bay", "Flight");
    public string FlightValue { get; init; } = "VN210 · Vietnam Airlines · SGN → HAN";
    public string DepartureLabel { get; init; } = UiLanguage.T("Khởi hành", "Departure");
    public string DepartureValue { get; init; } = "20/11/2026 · 08:00 (UTC+7)";
    public string SeatsLabel { get; init; } = UiLanguage.T("Ghế đã bán / Đang giữ", "Seats sold / held");
    public string SeatsValue { get; init; } = "147 / 0";
    public string ImpactTitle { get; init; } = "Ảnh hưởng đến hành khách trên 147 ghế đã bán";
    public string ImpactText { get; init; } =
        "Sau khi xác nhận, dừng mở bán và đánh dấu chuyến bị hủy. Cần liên hệ hành khách để kiểm tra " +
        "phương án chuyển chuyến hoặc hoàn theo chính sách hãng.";
    public string ReasonLabel { get; init; } = UiLanguage.T("Lý do hủy chuyến *", "Cancellation reason *");
    public string ReasonValue { get; init; } =
        "Điều chỉnh kế hoạch khai thác theo thông báo hãng.";
    public string MessageLabel { get; init; } = UiLanguage.T("Nội dung thông báo cho hành khách *", "Message to passengers *");
    public string MessageValue { get; init; } =
        "Vietnam Airlines thông báo hủy VN210 ngày 20/11/2026. AeroViet sẽ liên hệ để hỗ trợ chuyến " +
        "thay thế hoặc tiếp nhận yêu cầu hoàn theo chính sách hãng.";
    public string NoteTitle { get; init; } = UiLanguage.T("Xử lý đặt chỗ bị ảnh hưởng", "Manage affected bookings");
    public string NoteText { get; init; } =
        "Hệ thống giải phóng ghế đang giữ và xếp thông báo cho từng đặt chỗ có email hợp lệ. " +
        "Vé đã thanh toán được xử lý trong mục Biến động chuyến bay.";
    public string Footnote { get; init; } =
        "Chưa hủy chuyến. Chọn “Hủy thao tác” để giữ nguyên lịch bay và trạng thái mở bán hiện tại.";
    public string CancelHref { get; init; } = "/quan-ly/lich-bay";
    public string CancelText { get; init; } = UiLanguage.T("Hủy thao tác", "Cancel action");
    public string SubmitText { get; init; } = UiLanguage.T("Xác nhận hủy chuyến", "Confirm cancellation");
}

public sealed class ManagerPriceDialogViewModel
{
    public string Title { get; init; } = UiLanguage.T("Xác nhận thay đổi giá Phổ thông", "Confirm economy fare change");
    public string Subtitle { get; init; } = "VN210 · SGN → HAN · 20/11/2026 · 08:00 (UTC+7)";
    public string CurrentLabel { get; init; } = UiLanguage.T("Giá cơ bản hiện tại / khách", "Current base fare / passenger");
    public string CurrentValue { get; init; } = "1.500.000 VND";
    public string NewLabel { get; init; } = UiLanguage.T("Giá cơ bản mới / khách", "New base fare / passenger");
    public string NewValue { get; init; } = "1.650.000 VND";
<<<<<<< Updated upstream
    public string DeltaLabel { get; init; } = "Mức tăng";
=======
    public decimal NewAmount { get; init; } = 1_650_000m;
    public string DeltaLabel { get; init; } = UiLanguage.T("Mức tăng", "Increase");
>>>>>>> Stashed changes
    public string DeltaValue { get; init; } = "+150.000 VND / khách (+10%)";
    public string ThresholdLabel { get; init; } = UiLanguage.T("Ngưỡng giá được cấp", "Allowed fare range");
    public string ThresholdValue { get; init; } = "900.000–3.500.000 VND";
    public string WithinText { get; init; } = UiLanguage.T("Trong ngưỡng giá được cấp", "Within allowed fare range");
    public string NoteTitle { get; init; } = UiLanguage.T("Chỉ áp dụng cho đặt chỗ mới", "Applies to new bookings only");
    public string NoteText { get; init; } =
        "Vé đã thanh toán và đặt chỗ đang giữ còn hiệu lực giữ nguyên giá đã chốt. Không điều chỉnh số " +
        "tiền của các vé đang có hoặc thu thêm từ hành khách đã đặt.";
    public string ReasonLabel { get; init; } = UiLanguage.T("Lý do thay đổi giá *", "Fare change reason *");
    public string ReasonValue { get; init; } =
        "Điều chỉnh giá mở bán theo kế hoạch kinh doanh tháng 11.";
    public string Footnote { get; init; } =
        "Giá cơ bản chưa gồm thuế, phí. Thay đổi được lưu trong nhật ký.";
    public string ConfirmCheckText { get; init; } =
        "Tôi xác nhận chỉ cập nhật giá cho đặt chỗ mới sau thời điểm áp dụng.";
    public string CancelHref { get; init; } = "/quan-ly/lich-bay";
    public string CancelText { get; init; } = UiLanguage.T("Hủy thao tác", "Cancel action");
    public string SubmitText { get; init; } = UiLanguage.T("Áp dụng giá mới", "Apply new fare");
}

// -------------------------------------------------- 16 · đặt vé

public sealed record ManagerBookingRow(
    string Pnr,
    string Customer,
    string FlightDate,
    string HoldText,
    string HoldTone,
    string PaymentText,
    string PaymentTone,
    string TicketText,
    string TicketTone,
    string Total,
    bool IsSelected = false);

public sealed record ManagerTimelineRow(string Time, string Title, string Text);

public sealed record ManagerStatusBadge(string Text, string Tone);

public sealed class ManagerBookingsViewModel
{
    public string Subtitle { get; init; } =
        UiLanguage.T("Kiểm tra giữ chỗ, đối soát thanh toán và tình trạng xuất vé.",
            "Review booking holds, payment reconciliation and ticket issuance.");

    public string SearchLabel { get; init; } = UiLanguage.T("PNR / Tên khách hàng", "PNR / Customer name");
    public string SearchPlaceholder { get; init; } = UiLanguage.T("AV8K2M hoặc Nguyễn Minh Anh", "AV8K2M or Nguyễn Minh Anh");
    public string DateLabel { get; init; } = UiLanguage.T("Ngày đặt", "Booking date");
    public string DateValue { get; init; } = "01/11/2026";
    public string StatusLabel { get; init; } = UiLanguage.T("Trạng thái", "Status");
    public string StatusValue { get; init; } = "Tất cả trạng thái";
    public string SearchText { get; init; } = UiLanguage.T("Tìm đặt chỗ", "Find bookings");

    public string ListTitle { get; init; } = UiLanguage.T("Đặt chỗ gần đây", "Recent bookings");
    public string ListMeta { get; init; } = "32 đơn chờ thanh toán";
    public IReadOnlyList<ManagerBookingRow> Rows { get; init; } = Array.Empty<ManagerBookingRow>();
    public string ResultText { get; init; } = "Hiển thị 1–4 trong 126 đặt chỗ ngày 01/11/2026";

    public string DetailTitle { get; init; } = "Đặt chỗ · AV8K2M";
    public string DetailSubtitle { get; init; } =
        "Tạo lúc 09:30 ngày 01/11/2026 · Khách hàng tự đặt";
    public IReadOnlyList<ManagerStatusBadge> Statuses { get; init; } = Array.Empty<ManagerStatusBadge>();
    public IReadOnlyList<AdminDetailField> Fields { get; init; } = Array.Empty<AdminDetailField>();
    public string TotalLabel { get; init; } = UiLanguage.T("Tổng đã thanh toán", "Total paid");
    public string TotalValue { get; init; } = "2.200.000 ₫";
    public string PrimaryActionText { get; init; } = UiLanguage.T("Xem vé điện tử", "View e-ticket");
    public string SecondaryActionText { get; init; } = UiLanguage.T("Gửi lại email vé", "Resend ticket email");

    public string SafeTitle { get; init; } = UiLanguage.T("Không xuất vé trùng", "Duplicate ticket prevention");
    public string SafeText { get; init; } =
        "Đơn này đã có số vé hợp lệ. Chức năng xuất vé được khóa để tránh phát hành hai lần.";

    public string TimelineTitle { get; init; } = UiLanguage.T("Lịch sử giao dịch", "Transaction history");
    public IReadOnlyList<ManagerTimelineRow> Timeline { get; init; } = Array.Empty<ManagerTimelineRow>();
    public string TransactionCode { get; init; } = "Mã giao dịch: TX0111260942";
    public string TransactionConfirmed { get; init; } = UiLanguage.T("Cổng thanh toán đã xác nhận", "Confirmed by payment gateway");

    public string ConditionsTitle { get; init; } = UiLanguage.T("Điều kiện trước khi xuất vé", "Ticket issuance conditions");
    public IReadOnlyList<ManagerChecklistItem> Conditions { get; init; } = Array.Empty<ManagerChecklistItem>();
    public string IssuedText { get; init; } = UiLanguage.T("Đã xuất vé", "Ticket issued");
    public string ConditionsNote { get; init; } =
        "Đơn chưa thanh toán không được xuất vé; chuyển sang nhắc thanh toán hoặc chờ đối soát.";
}

// ------------------------------------------- 17 · xử lý đổi / hoàn

public sealed record ManagerQueueTab(string Label, string Href, bool IsActive);

public sealed record ManagerRequestRow(
    string Request,
    string Pnr,
    string Customer,
    string Type,
    string Submitted,
    string StatusText,
    string StatusTone,
<<<<<<< Updated upstream
    bool IsSelected = false);
=======
    bool IsSelected = false,
    int Id = 0,
    string? Href = null);
>>>>>>> Stashed changes

public sealed record ManagerRefundRow(string Label, string Value, bool IsTotal = false);

public sealed class ManagerChangeRefundsViewModel
{
    public string Subtitle { get; init; } =
        UiLanguage.T("Kiểm tra điều kiện, tính phí và phê duyệt yêu cầu. Tách biệt phê duyệt với hoàn tiền.",
            "Check eligibility, calculate fees and approve requests. Refund payment follows approval.");

    public IReadOnlyList<ManagerQueueTab> Tabs { get; init; } = Array.Empty<ManagerQueueTab>();
    public string QueueTitle { get; init; } = UiLanguage.T("Hàng đợi yêu cầu", "Request queue");
    public string QueueSubtitle { get; init; } = "Chờ duyệt: 5 đổi vé · 7 hoàn vé";
    public string SearchLabel { get; init; } = UiLanguage.T("Tìm theo PNR / Mã yêu cầu", "Search by PNR / Request ID");
    public string SearchPlaceholder { get; init; } = UiLanguage.T("Nhập mã yêu cầu", "Enter request ID");
    public string? SearchValue { get; init; }
    public string TypeLabel { get; init; } = UiLanguage.T("Loại yêu cầu", "Request type");
    public string TypeValue { get; init; } = "Tất cả";
    public string DateLabel { get; init; } = UiLanguage.T("Ngày gửi", "Submission date");
    public string DateValue { get; init; } = "01/11/2026";
    public string FilterText { get; init; } = UiLanguage.T("Lọc", "Filter");
    public IReadOnlyList<ManagerRequestRow> Rows { get; init; } = Array.Empty<ManagerRequestRow>();
    public string ResultText { get; init; } = "Hiển thị 1–4 trong 18 yêu cầu";

    public string DetailTitle { get; init; } = "Chi tiết yêu cầu · RF-1027";
    public string DetailSubtitle { get; init; } =
        "Trần Mai Linh · AV4R6C · Vé 738 2400012751";
<<<<<<< Updated upstream
    public IReadOnlyList<AdminDetailField> DetailFields { get; init; } = Array.Empty<AdminDetailField>();
    public string EligibleTitle { get; init; } = "Đủ điều kiện gửi yêu cầu hoàn";
    public string EligibleText { get; init; } =
=======
    public IReadOnlyList<AdminDetailField> DetailFields { get; set; } = Array.Empty<AdminDetailField>();
    public string EligibleTitle { get; set; } = UiLanguage.T("Đủ điều kiện gửi yêu cầu hoàn", "Eligible for a refund request");
    public string EligibleText { get; set; } =
>>>>>>> Stashed changes
        "Vé đã thanh toán và chưa sử dụng. Yêu cầu gửi trước giờ bay hơn 3 tiếng.";
    public string ConditionText { get; init; } =
        "Điều kiện: phí hoàn 500.000 ₫; không hoàn phí dịch vụ 70.000 ₫ và hành lý mua thêm 250.000 ₫.";

<<<<<<< Updated upstream
    public string DecisionTitle { get; init; } = "Phê duyệt / từ chối";
    public string ReasonLabel { get; init; } = "Lý do xử lý *";
    public string ReasonValue { get; init; } = "Vé đủ điều kiện; khách xác nhận mức phí hoàn.";
    public string DecisionNote { get; init; } =
        "Khi từ chối, nêu rõ điều kiện không đáp ứng. Khi phê duyệt, hệ thống tạo lệnh hoàn và thông báo khách.";
    public string RejectText { get; init; } = "Từ chối yêu cầu";
    public string ApproveText { get; init; } = "Phê duyệt hoàn vé";
    public string ProcessTitle { get; init; } = "Quy trình đổi vé · 5 yêu cầu đang chờ";
    public string ProcessText { get; init; } =
        "Kiểm tra chỗ trên chuyến mới → tính phí đổi 360.000 ₫ + chênh lệch giá → khách xác nhận và " +
        "thanh toán thêm → xuất lại vé. Chưa thanh toán phần chênh lệch thì không xuất lại.";

    public string RefundTitle { get; init; } = "Tính hoàn tiền dự kiến";
    public IReadOnlyList<ManagerRefundRow> RefundRows { get; init; } = Array.Empty<ManagerRefundRow>();
    public string RefundMethodLabel { get; init; } = "Phương thức";
    public string RefundMethodValue { get; init; } = "Visa · •••• 1762";
    public string RefundTimeLabel { get; init; } = "Thời gian dự kiến";
    public string RefundTimeValue { get; init; } = "7–15 ngày làm việc";
    public string WarningTitle { get; init; } = "Chưa thực hiện hoàn tiền";
=======
    /// <summary>Chỉ yêu cầu đang chờ duyệt mới hiện form duyệt / từ chối.</summary>
    public bool CanDecide { get; set; }
    public bool CanCompleteDemoRefund { get; set; }
    public string? RefundLedgerBookingCode { get; set; }

    /// <summary>Yêu cầu hoàn mới cần hộp thoại xác nhận tạo lệnh hoàn tiền.</summary>
    public bool IsRefundRequest { get; set; }

    public string? Message { get; init; }
    public string MessageTone { get; init; } = "info";

    public string DecisionTitle { get; init; } = UiLanguage.T("Phê duyệt / từ chối", "Approve / Reject");
    public string ReasonLabel { get; init; } = UiLanguage.T("Lý do xử lý *", "Processing reason *");
    public string ReasonValue { get; set; } = "Vé đủ điều kiện; khách xác nhận mức phí hoàn.";
    public string DecisionNote { get; init; } =
        UiLanguage.T("Khi từ chối, nêu rõ điều kiện không đáp ứng. Khi phê duyệt, hệ thống tạo lệnh hoàn và thông báo khách.",
            "When rejecting, specify which requirement was unmet. Approving creates a refund order and notifies the customer.");
    public string RejectText { get; init; } = UiLanguage.T("Từ chối yêu cầu", "Reject request");
    public string ApproveText { get; set; } = UiLanguage.T("Phê duyệt hoàn vé", "Approve refund");
    public string ProcessTitle { get; set; } = "Quy trình đổi vé · 5 yêu cầu đang chờ";
    public string ProcessText { get; set; } =
        "Kiểm tra chỗ trên chuyến mới → tính phí đổi 360.000 ₫ + chênh lệch giá → khách xác nhận và " +
        "thanh toán thêm → xuất lại vé. Chưa thanh toán phần chênh lệch thì không xuất lại.";

    public string RefundTitle { get; set; } = UiLanguage.T("Tính hoàn tiền dự kiến", "Estimated refund");
    public IReadOnlyList<ManagerRefundRow> RefundRows { get; set; } = Array.Empty<ManagerRefundRow>();
    public string RefundMethodLabel { get; init; } = UiLanguage.T("Phương thức", "Method");
    public string RefundMethodValue { get; set; } = "Visa · •••• 1762";
    public string RefundTimeLabel { get; init; } = UiLanguage.T("Thời gian dự kiến", "Estimated time");
    public string RefundTimeValue { get; init; } = UiLanguage.T("7–15 ngày làm việc", "7–15 business days");
    public string WarningTitle { get; init; } = UiLanguage.T("Chưa thực hiện hoàn tiền", "Refund pending");
>>>>>>> Stashed changes
    public string WarningText { get; init; } =
        "Phê duyệt chỉ chuyển sang “Đã duyệt · Chờ hoàn tiền”. Chỉ ghi nhận hoàn tất khi cổng thanh toán xác nhận.";

    public bool ShowApproveDialog { get; init; }
    public ManagerApproveRefundDialogViewModel ApproveDialog { get; init; } = new();
}

public sealed class ManagerApproveRefundDialogViewModel
{
    public string Title { get; init; } = UiLanguage.T("Duyệt và tạo lệnh hoàn vé?", "Approve and create refund order?");
    public string Subtitle { get; init; } =
        "RF-1027 / AV4R6C · Trần Mai Linh · Trạng thái: Chờ duyệt";
    public string TicketLabel { get; init; } = UiLanguage.T("Số vé", "Ticket number");
    public string TicketValue { get; init; } = "738 2400012751";
    public string FlightLabel { get; init; } = UiLanguage.T("Chuyến bay gốc", "Original flight");
    public string FlightValue { get; init; } = "VN216 · SGN → HAN · 22/11/2026 12:00";
    public string EligibleText { get; init; } =
        "Đủ điều kiện · Chưa sử dụng · Gửi trước giờ bay hơn 3 tiếng";
    public string TotalLabel { get; init; } = UiLanguage.T("Tổng đã thanh toán", "Total paid");
    public string TotalValue { get; init; } = "2.200.000 ₫";
    public string FeeLabel { get; init; } = UiLanguage.T("Phí hoàn vé", "Refund fee");
    public string FeeValue { get; init; } = "−500.000 ₫";
    public string ServiceLabel { get; init; } = UiLanguage.T("Phí dịch vụ không hoàn", "Non-refundable service fee");
    public string ServiceValue { get; init; } = "−70.000 ₫";
    public string BaggageLabel { get; init; } = UiLanguage.T("Hành lý thêm không hoàn", "Non-refundable extra baggage");
    public string BaggageValue { get; init; } = "−250.000 ₫";
    public string AmountLabel { get; init; } = UiLanguage.T("Số tiền tạo lệnh hoàn", "Refund order amount");
    public string AmountValue { get; init; } = "1.380.000 ₫";
    public string MethodLabel { get; init; } = UiLanguage.T("Hoàn về nguồn thanh toán", "Refund to original payment method");
    public string MethodValue { get; init; } = "Visa ••••1762";
    public string TimeLabel { get; init; } = UiLanguage.T("Thời gian dự kiến sau khi duyệt", "Estimated time after approval");
    public string TimeValue { get; init; } = "7–15 ngày làm việc";
    public string ReasonLabel { get; init; } = UiLanguage.T("Lý do duyệt *", "Approval reason *");
    public string ReasonValue { get; init; } = "Vé đủ điều kiện; khách xác nhận mức phí hoàn.";
    public string NoteTitle { get; init; } = UiLanguage.T("Duyệt chưa có nghĩa tiền đã hoàn", "Refund payment follows approval");
    public string NoteText { get; init; } =
        "Xác nhận sẽ tạo lệnh hoàn và chuyển sang “Đã duyệt · Chờ hoàn tiền”, đồng thời thông báo khách. " +
        "Chỉ hoàn tất khi cổng thanh toán xác nhận; hiện chưa tạo lệnh.";
    public string ConfirmCheckText { get; init; } =
        "Tôi đã kiểm tra số tiền 1.380.000 ₫ và nguồn nhận hoàn Visa ••••1762.";
    public string CancelHref { get; init; } = "/quan-ly/doi-hoan";
    public string CancelText { get; init; } = UiLanguage.T("Hủy thao tác", "Cancel action");
    public string SubmitText { get; init; } = UiLanguage.T("Duyệt và tạo lệnh hoàn", "Approve and create refund order");
}

// -------------------------------------------------- 18 · báo cáo

public sealed record ManagerRevenueBar(string Label, string ValueText, decimal Value, bool IsCurrent);

public sealed record ManagerAirlineRevenue(
    string Name,
    string Amount,
    string ShareText,
    int Percent,
    string Tone);

public sealed record ManagerRouteRevenue(
    string Route,
    string Tickets,
    string Gross,
    string Refunded,
    string Net,
    bool IsTotal = false);

public sealed record ManagerFlightRevenue(
    string Flight, string Departure, string Route, string Tickets, string Gross, string Refunded, string Net);

public sealed class ManagerReportsViewModel
{
    public string Subtitle { get; init; } =
        UiLanguage.T("Theo dõi hiệu quả bán vé, doanh thu và biến động đổi / hoàn.",
            "Track ticket sales, revenue, changes and refunds.");

    public string PeriodLabel { get; init; } = UiLanguage.T("Khoảng thời gian", "Date range");
    public string PeriodValue { get; init; } = "01/10/2026 – 31/10/2026";
    public string? PeriodError { get; init; }
    public string AirlineLabel { get; init; } = UiLanguage.T("Hãng hàng không", "Airline");
    public string AirlineValue { get; init; } = "Tất cả hãng";
<<<<<<< Updated upstream
    public string RouteLabel { get; init; } = "Tuyến bay";
    public string RouteValue { get; init; } = "Tất cả tuyến";
    public string ViewText { get; init; } = "Xem báo cáo";
    public string ExportText { get; init; } = "Xuất báo cáo";
=======
    public IReadOnlyList<ManagerSelectOption> AirlineOptions { get; init; } = Array.Empty<ManagerSelectOption>();
    public string RouteLabel { get; init; } = UiLanguage.T("Tuyến bay", "Route");
    public string RouteValue { get; init; } = "Tất cả tuyến";
    public IReadOnlyList<ManagerSelectOption> RouteOptions { get; init; } = Array.Empty<ManagerSelectOption>();
    public string FlightValue { get; init; } = string.Empty;
    public string ViewText { get; init; } = UiLanguage.T("Xem báo cáo", "View reports");
    public string ExportText { get; init; } = UiLanguage.T("Xuất báo cáo", "Export report");
>>>>>>> Stashed changes

    public IReadOnlyList<ManagerKpi> Kpis { get; init; } = Array.Empty<ManagerKpi>();

    public string TrendTitle { get; init; } = UiLanguage.T("Xu hướng doanh thu", "Revenue trend");
    public string TrendSubtitle { get; init; } = "Tháng 05–10/2026 · Đơn vị: tỷ VND";
    public string ChartUnit { get; init; } = UiLanguage.T("Đơn vị: tỷ VND", "Unit: billion VND");
    public string ChartLegend { get; init; } = UiLanguage.T("● Doanh thu gộp", "● Gross revenue");
    public IReadOnlyList<ManagerRevenueBar> Bars { get; init; } = Array.Empty<ManagerRevenueBar>();
    public decimal BarsMax => Bars.Count == 0 ? 1m : Bars.Max(bar => bar.Value);
    public int HeightPercent(ManagerRevenueBar bar) =>
        BarsMax <= 0 ? 0 : (int)Math.Round(bar.Value / BarsMax * 100m);

    public string AirlineTitle { get; init; } = UiLanguage.T("Doanh thu theo hãng", "Revenue by airline");
    public string AirlineSubtitle { get; init; } = "Tháng 10/2026 · Tổng 18,6 tỷ ₫";
    public IReadOnlyList<ManagerAirlineRevenue> Airlines { get; init; } = Array.Empty<ManagerAirlineRevenue>();

    public string RoutesTitle { get; init; } = UiLanguage.T("Chi tiết doanh thu theo tuyến", "Revenue by route");
    public string RoutesSubtitle { get; init; } =
        "Doanh thu vé và dịch vụ trước hoàn tiền · Đơn vị tiền tệ: VND";
    public IReadOnlyList<ManagerRouteRevenue> Routes { get; init; } = Array.Empty<ManagerRouteRevenue>();
    public IReadOnlyList<ManagerFlightRevenue> Flights { get; init; } = Array.Empty<ManagerFlightRevenue>();
    public string Note { get; init; } =
        "Cơ sở: 10.000 đặt chỗ tạo trong tháng, 240 đơn hủy; 9.600 vé đã xuất, 173 vé đã hoàn. " +
        "160 đặt chỗ còn lại chưa xuất vé tại cuối kỳ. Tiền hoàn chỉ tính giao dịch đã xác nhận thành công.";
}
