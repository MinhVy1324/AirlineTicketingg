namespace AirlineTicketing.Web.ViewModels;

// ----------------------------------------------------------- manager chrome

public sealed record ManagerNavItem(string Key, string Label, string Icon, string Href);

public static class ManagerNav
{
    public const string WorkspaceName = "Điều hành khai thác";
    public const string ScopeTitle = "Quyền vận hành được cấp";
    public const string ScopeText = "Không cấp quyền hoặc thay đổi cấu hình bảo mật.";

    public static readonly IReadOnlyList<ManagerNavItem> Items = new[]
    {
        new ManagerNavItem("overview", "Tổng quan vận hành", "layout-dashboard", "/quan-ly"),
        new ManagerNavItem("schedules", "Lịch bay & tồn chỗ", "calendar", "/quan-ly/lich-bay"),
        new ManagerNavItem("bookings", "Quản lý đặt vé", "ticket", "/quan-ly/dat-ve"),
        new ManagerNavItem("changes", "Xử lý đổi / hoàn", "refresh", "/quan-ly/doi-hoan"),
        new ManagerNavItem("reports", "Báo cáo kinh doanh", "chart", "/quan-ly/bao-cao")
    };

    public static string LabelFor(string? key) =>
        Items.FirstOrDefault(item => item.Key == key)?.Label ?? "Tổng quan vận hành";
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
    public string ShiftText { get; init; } = "Ca sáng · 06:00–14:00";
    public string UpdatedText { get; init; } = "Cập nhật lúc 10:00 · Giờ Việt Nam (UTC+7)";

    public IReadOnlyList<ManagerKpi> Kpis { get; init; } = Array.Empty<ManagerKpi>();

    public string AttentionTitle { get; init; } = "2 chuyến cần chú ý trong ca trực";
    public string AttentionText { get; init; } =
        "VN216: thay đổi giờ khởi hành +30 phút. VJ640: còn 4 ghế. " +
        "Kiểm tra đặt chỗ liên quan trước khi cập nhật.";

    public string FlightsTitle { get; init; } = "Chuyến cần chú ý";
    public string FlightsSubtitle { get; init; } = "Lịch khai thác ngày 01/11/2026";
    public string FlightsLinkText { get; init; } = "Xem lịch bay";
    public string FlightsLinkHref { get; init; } = "/quan-ly/lich-bay";
    public IReadOnlyList<ManagerFlightRow> Flights { get; init; } = Array.Empty<ManagerFlightRow>();
    public string FlightsNote { get; init; } =
        "Giờ hiển thị là lịch mới nhất. Thay đổi lịch phải gửi thông báo cho hành khách.";

    public string WorkloadTitle { get; init; } = "Khối lượng xử lý";
    public IReadOnlyList<ManagerWorkloadRow> Workload { get; init; } = Array.Empty<ManagerWorkloadRow>();
    public string WorkloadDetailLabel { get; init; } = "Chờ đối soát thanh toán";
    public string WorkloadDetailValue { get; init; } = "3 giao dịch";
    public string WorkloadButtonText { get; init; } = "Mở hàng đợi yêu cầu";
    public string WorkloadButtonHref { get; init; } = "/quan-ly/doi-hoan";

    public string HoldsTitle { get; init; } = "Đặt chỗ chờ thanh toán";
    public string HoldsLinkText { get; init; } = "Quản lý đặt vé";
    public string HoldsLinkHref { get; init; } = "/quan-ly/dat-ve";
    public IReadOnlyList<ManagerHoldRow> Holds { get; init; } = Array.Empty<ManagerHoldRow>();

    public string ChecklistTitle { get; init; } = "Checklist ca trực";
    public IReadOnlyList<ManagerChecklistItem> Checklist { get; init; } = Array.Empty<ManagerChecklistItem>();
    public string SafetyTitle { get; init; } = "Xuất vé an toàn";
    public string SafetyText { get; init; } = "Chỉ xuất vé sau khi thanh toán đã được xác nhận.";
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
    bool IsSelected = false,
    int Id = 0,
    string? Href = null);

public sealed record ManagerFareRow(string Cabin, string Capacity, string Sold, string BasePrice);

public sealed record ManagerSelectOption(string Value, string Label, bool IsSelected = false);

public sealed class ManagerSchedulesViewModel
{
    public string Subtitle { get; init; } =
        "Quản lý lịch khai thác và giá theo hạng. Không thay đổi dữ liệu danh mục nền.";
    public string AddText { get; init; } = "Thêm lịch khai thác";

    public string DateLabel { get; init; } = "Ngày khởi hành";
    public string DateValue { get; init; } = "20/11/2026";
    public string RouteLabel { get; init; } = "Tuyến bay";
    public string RouteValue { get; init; } = "SGN → HAN";
    public string AirlineStatusLabel { get; init; } = "Hãng / Trạng thái";
    public string AirlineStatusValue { get; init; } = "Tất cả";
    public string SearchText { get; init; } = "Tìm chuyến";

    public string ListTitle { get; init; } = "Lịch khai thác · 20/11/2026";
    public string ListSubtitle { get; init; } =
        "Giờ Việt Nam (UTC+7) · Tồn chỗ = sức chứa − đã bán − đang giữ";
    public IReadOnlyList<ManagerScheduleRow> Rows { get; init; } = Array.Empty<ManagerScheduleRow>();
    public string ResultText { get; init; } = "Hiển thị 1–4 trong 18 chuyến cùng tuyến";

    public string EditorTitle { get; init; } = "Chỉnh sửa chuyến · VN210";
    public string EditorSubtitle { get; init; } = "20/11/2026 · Vietnam Airlines · Đã có 147 ghế bán";
    public string EditorDateLabel { get; init; } = "Ngày bay";
    public string EditorDateValue { get; init; } = "20/11/2026";
    public string EditorTimeLabel { get; init; } = "Giờ đi / đến";
    public string EditorTimeValue { get; init; } = "08:00 / 10:10";
    public string EditorStatusLabel { get; init; } = "Trạng thái";
    public string EditorStatusValue { get; init; } = "Mở bán";
    public IReadOnlyList<ManagerFareRow> Fares { get; init; } = Array.Empty<ManagerFareRow>();
    public string EconomyPriceLabel { get; init; } = "Giá cơ bản · Phổ thông";
    public string EconomyPriceValue { get; init; } = "1.500.000 VND";
    public string BusinessPriceLabel { get; init; } = "Giá cơ bản · Thương gia";
    public string BusinessPriceValue { get; init; } = "4.500.000 VND";
    public string EditorNote { get; init; } =
        "Giá cơ bản chưa gồm thuế/phí. Giá bán Phổ thông Tiêu chuẩn: 1.950.000 ₫. " +
        "Số ghế đang giữ: 0 · Còn trống: 33.";
    public string CancelEditText { get; init; } = "Hủy chỉnh sửa";
    public string SaveText { get; init; } = "Lưu lịch & giá";

    public string RulesTitle { get; init; } = "Quy tắc cập nhật";
    public string ChangeTitle { get; init; } = "Đổi lịch phải thông báo";
    public string ChangeText { get; init; } =
        "Nếu đổi ngày hoặc giờ, bắt buộc gửi email đến toàn bộ hành khách có đặt chỗ.";
    public bool EmailChecked { get; init; } = true;
    public string ReasonLabel { get; init; } = "Lý do thay đổi";
    public string ReasonPlaceholder { get; init; } = "Nhập lý do nếu đổi lịch";
    public string RulesNote { get; init; } =
        "Không giảm sức chứa dưới số ghế đã bán và đang giữ. Giá mới chỉ áp dụng cho đặt chỗ mới.";
    public string ThresholdTitle { get; init; } = "Ngưỡng giá được cấp";
    public string ThresholdText { get; init; } =
        "Phổ thông: 900.000–3.500.000 ₫. Ngoài ngưỡng cần Admin phê duyệt.";

    public bool ShowCancelDialog { get; set; }
    public ManagerCancelFlightDialogViewModel CancelDialog { get; set; } = new();

    public bool ShowPriceDialog { get; set; }
    public ManagerPriceDialogViewModel PriceDialog { get; set; } = new();

    public IReadOnlyList<ManagerSelectOption> RouteOptions { get; init; } = Array.Empty<ManagerSelectOption>();
    public IReadOnlyList<ManagerSelectOption> StatusOptions { get; init; } = Array.Empty<ManagerSelectOption>();
    public IReadOnlyList<ManagerSelectOption> EditorRouteOptions { get; init; } = Array.Empty<ManagerSelectOption>();
    public IReadOnlyList<ManagerSelectOption> EditorAircraftOptions { get; init; } = Array.Empty<ManagerSelectOption>();
    public IReadOnlyList<ManagerSelectOption> EditorAirlineOptions { get; init; } = Array.Empty<ManagerSelectOption>();

    public bool EditorIsNew { get; init; }
    public int SelectedFlightId { get; init; }
    public int EditorRouteId { get; init; }
    public int EditorAircraftId { get; init; }
    public int EditorAirlineId { get; init; }
    public string EditorFlightNumber { get; init; } = string.Empty;
    public string? Message { get; init; }
    public string MessageTone { get; init; } = "info";
}

public sealed class ManagerCancelFlightDialogViewModel
{
    public string Title { get; init; } = "Xác nhận hủy chuyến VN210?";
    public string Subtitle { get; init; } = "Lịch bay & tồn chỗ / VN210 · Hiện đang mở bán";
    public string FlightLabel { get; init; } = "Chuyến bay";
    public string FlightValue { get; init; } = "VN210 · Vietnam Airlines · SGN → HAN";
    public string DepartureLabel { get; init; } = "Khởi hành";
    public string DepartureValue { get; init; } = "20/11/2026 · 08:00 (UTC+7)";
    public string SeatsLabel { get; init; } = "Ghế đã bán / Đang giữ";
    public string SeatsValue { get; init; } = "147 / 0";
    public string ImpactTitle { get; init; } = "Ảnh hưởng đến hành khách trên 147 ghế đã bán";
    public string ImpactText { get; init; } =
        "Sau khi xác nhận, dừng mở bán và đánh dấu chuyến bị hủy. Cần liên hệ hành khách để kiểm tra " +
        "phương án chuyển chuyến hoặc hoàn theo chính sách hãng.";
    public string ReasonLabel { get; init; } = "Lý do hủy chuyến *";
    public string ReasonValue { get; init; } =
        "Điều chỉnh kế hoạch khai thác theo thông báo hãng.";
    public string MessageLabel { get; init; } = "Nội dung thông báo cho hành khách *";
    public string MessageValue { get; init; } =
        "Vietnam Airlines thông báo hủy VN210 ngày 20/11/2026. AeroViet sẽ liên hệ để hỗ trợ chuyến " +
        "thay thế hoặc tiếp nhận yêu cầu hoàn theo chính sách hãng.";
    public string EmailCheckText { get; init; } =
        "Gửi email thông báo tới toàn bộ hành khách có đặt chỗ (bắt buộc).";
    public string NoteTitle { get; init; } = "Không tự động hoàn tiền toàn bộ";
    public string NoteText { get; init; } =
        "Hủy chuyến chỉ cập nhật lịch khai thác và dừng mở bán. Từng yêu cầu chuyển chuyến / hoàn tiền " +
        "được kiểm tra và xử lý riêng; chưa ghi nhận tiền đã hoàn.";
    public string Footnote { get; init; } =
        "Chưa hủy chuyến. Chọn “Hủy thao tác” để giữ nguyên lịch bay và trạng thái mở bán hiện tại.";
    public string CancelHref { get; init; } = "/quan-ly/lich-bay";
    public string CancelText { get; init; } = "Hủy thao tác";
    public string SubmitText { get; init; } = "Xác nhận hủy chuyến";
}

public sealed class ManagerPriceDialogViewModel
{
    public string Title { get; init; } = "Xác nhận thay đổi giá Phổ thông";
    public string Subtitle { get; init; } = "VN210 · SGN → HAN · 20/11/2026 · 08:00 (UTC+7)";
    public string CurrentLabel { get; init; } = "Giá cơ bản hiện tại / khách";
    public string CurrentValue { get; init; } = "1.500.000 VND";
    public string NewLabel { get; init; } = "Giá cơ bản mới / khách";
    public string NewValue { get; init; } = "1.650.000 VND";
    public decimal NewAmount { get; init; } = 1_650_000m;
    public string DeltaLabel { get; init; } = "Mức tăng";
    public string DeltaValue { get; init; } = "+150.000 VND / khách (+10%)";
    public string ThresholdLabel { get; init; } = "Ngưỡng giá được cấp";
    public string ThresholdValue { get; init; } = "900.000–3.500.000 VND";
    public string WithinText { get; init; } = "Trong ngưỡng giá được cấp";
    public string NoteTitle { get; init; } = "Chỉ áp dụng cho đặt chỗ mới";
    public string NoteText { get; init; } =
        "Vé đã thanh toán và đặt chỗ đang giữ còn hiệu lực giữ nguyên giá đã chốt. Không điều chỉnh số " +
        "tiền của các vé đang có hoặc thu thêm từ hành khách đã đặt.";
    public string ReasonLabel { get; init; } = "Lý do thay đổi giá *";
    public string ReasonValue { get; init; } =
        "Điều chỉnh giá mở bán theo kế hoạch kinh doanh tháng 11.";
    public string Footnote { get; init; } =
        "Giá cơ bản chưa gồm thuế, phí. Thay đổi được lưu trong nhật ký.";
    public string ConfirmCheckText { get; init; } =
        "Tôi xác nhận chỉ cập nhật giá cho đặt chỗ mới sau thời điểm áp dụng.";
    public string CancelHref { get; init; } = "/quan-ly/lich-bay";
    public string CancelText { get; init; } = "Hủy thao tác";
    public string SubmitText { get; init; } = "Áp dụng giá mới";
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
        "Kiểm tra giữ chỗ, đối soát thanh toán và tình trạng xuất vé.";

    public string SearchLabel { get; init; } = "PNR / Tên khách hàng";
    public string SearchPlaceholder { get; init; } = "AV8K2M hoặc Nguyễn Minh Anh";
    public string DateLabel { get; init; } = "Ngày đặt";
    public string DateValue { get; init; } = "01/11/2026";
    public string StatusLabel { get; init; } = "Trạng thái";
    public string StatusValue { get; init; } = "Tất cả trạng thái";
    public string SearchText { get; init; } = "Tìm đặt chỗ";

    public string ListTitle { get; init; } = "Đặt chỗ gần đây";
    public string ListMeta { get; init; } = "32 đơn chờ thanh toán";
    public IReadOnlyList<ManagerBookingRow> Rows { get; init; } = Array.Empty<ManagerBookingRow>();
    public string ResultText { get; init; } = "Hiển thị 1–4 trong 126 đặt chỗ ngày 01/11/2026";

    public string DetailTitle { get; init; } = "Đặt chỗ · AV8K2M";
    public string DetailSubtitle { get; init; } =
        "Tạo lúc 09:30 ngày 01/11/2026 · Khách hàng tự đặt";
    public IReadOnlyList<ManagerStatusBadge> Statuses { get; init; } = Array.Empty<ManagerStatusBadge>();
    public IReadOnlyList<AdminDetailField> Fields { get; init; } = Array.Empty<AdminDetailField>();
    public string TotalLabel { get; init; } = "Tổng đã thanh toán";
    public string TotalValue { get; init; } = "2.200.000 ₫";
    public string PrimaryActionText { get; init; } = "Xem vé điện tử";
    public string SecondaryActionText { get; init; } = "Gửi lại email vé";

    public string SafeTitle { get; init; } = "Không xuất vé trùng";
    public string SafeText { get; init; } =
        "Đơn này đã có số vé hợp lệ. Chức năng xuất vé được khóa để tránh phát hành hai lần.";

    public string TimelineTitle { get; init; } = "Lịch sử giao dịch";
    public IReadOnlyList<ManagerTimelineRow> Timeline { get; init; } = Array.Empty<ManagerTimelineRow>();
    public string TransactionCode { get; init; } = "Mã giao dịch: TX0111260942";
    public string TransactionConfirmed { get; init; } = "Cổng thanh toán đã xác nhận";

    public string ConditionsTitle { get; init; } = "Điều kiện trước khi xuất vé";
    public IReadOnlyList<ManagerChecklistItem> Conditions { get; init; } = Array.Empty<ManagerChecklistItem>();
    public string IssuedText { get; init; } = "Đã xuất vé";
    public string ConditionsNote { get; init; } =
        "Đơn chưa thanh toán không được xuất vé; chuyển sang nhắc thanh toán hoặc chờ đối soát.";
}

// ------------------------------------------- 17 · xử lý đổi / hoàn

public sealed record ManagerQueueTab(string Label, string Href, bool IsActive);

public sealed record ManagerRequestRow(
    string Request,
    string Customer,
    string Type,
    string Submitted,
    string StatusText,
    string StatusTone,
    bool IsSelected = false,
    int Id = 0,
    string Href = "");

public sealed record ManagerRefundRow(string Label, string Value, bool IsTotal = false);

public sealed class ManagerChangeRefundsViewModel
{
    public string Subtitle { get; init; } =
        "Kiểm tra điều kiện, tính phí và phê duyệt yêu cầu. Tách biệt phê duyệt với hoàn tiền.";

    public IReadOnlyList<ManagerQueueTab> Tabs { get; init; } = Array.Empty<ManagerQueueTab>();
    public string QueueTitle { get; init; } = "Hàng đợi yêu cầu";
    public string QueueSubtitle { get; init; } = "Chờ duyệt: 5 đổi vé · 7 hoàn vé";
    public string SearchLabel { get; init; } = "Tìm theo PNR / Mã yêu cầu";
    public string SearchPlaceholder { get; init; } = "Nhập mã yêu cầu";
    public string TypeLabel { get; init; } = "Loại yêu cầu";
    public string TypeValue { get; init; } = "Tất cả";
    public string DateLabel { get; init; } = "Ngày gửi";
    public string DateValue { get; init; } = "01/11/2026";
    public string FilterText { get; init; } = "Lọc";
    public IReadOnlyList<ManagerRequestRow> Rows { get; init; } = Array.Empty<ManagerRequestRow>();
    public string ResultText { get; init; } = "Hiển thị 1–4 trong 18 yêu cầu";

    public string DetailTitle { get; init; } = "Chi tiết yêu cầu · RF-1027";
    public string DetailSubtitle { get; init; } =
        "Trần Mai Linh · AV4R6C · Vé 738 2400012751";
    public IReadOnlyList<AdminDetailField> DetailFields { get; init; } = Array.Empty<AdminDetailField>();
    public string EligibleTitle { get; init; } = "Đủ điều kiện gửi yêu cầu hoàn";
    public string EligibleText { get; init; } =
        "Vé đã thanh toán và chưa sử dụng. Yêu cầu gửi trước giờ bay hơn 3 tiếng.";
    public string ConditionText { get; init; } =
        "Điều kiện: phí hoàn 500.000 ₫; không hoàn phí dịch vụ 70.000 ₫ và hành lý mua thêm 250.000 ₫.";

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
    public string WarningText { get; init; } =
        "Phê duyệt chỉ chuyển sang “Đã duyệt · Chờ hoàn tiền”. Chỉ ghi nhận hoàn tất khi cổng thanh toán xác nhận.";

    public bool ShowApproveDialog { get; init; }
    public ManagerApproveRefundDialogViewModel ApproveDialog { get; init; } = new();
}

public sealed class ManagerApproveRefundDialogViewModel
{
    public string Title { get; init; } = "Duyệt và tạo lệnh hoàn vé?";
    public string Subtitle { get; init; } =
        "RF-1027 / AV4R6C · Trần Mai Linh · Trạng thái: Chờ duyệt";
    public string TicketLabel { get; init; } = "Số vé";
    public string TicketValue { get; init; } = "738 2400012751";
    public string FlightLabel { get; init; } = "Chuyến bay gốc";
    public string FlightValue { get; init; } = "VN216 · SGN → HAN · 22/11/2026 12:00";
    public string EligibleText { get; init; } =
        "Đủ điều kiện · Chưa sử dụng · Gửi trước giờ bay hơn 3 tiếng";
    public string TotalLabel { get; init; } = "Tổng đã thanh toán";
    public string TotalValue { get; init; } = "2.200.000 ₫";
    public string FeeLabel { get; init; } = "Phí hoàn vé";
    public string FeeValue { get; init; } = "−500.000 ₫";
    public string ServiceLabel { get; init; } = "Phí dịch vụ không hoàn";
    public string ServiceValue { get; init; } = "−70.000 ₫";
    public string BaggageLabel { get; init; } = "Hành lý thêm không hoàn";
    public string BaggageValue { get; init; } = "−250.000 ₫";
    public string AmountLabel { get; init; } = "Số tiền tạo lệnh hoàn";
    public string AmountValue { get; init; } = "1.380.000 ₫";
    public string MethodLabel { get; init; } = "Hoàn về nguồn thanh toán";
    public string MethodValue { get; init; } = "Visa ••••1762";
    public string TimeLabel { get; init; } = "Thời gian dự kiến sau khi duyệt";
    public string TimeValue { get; init; } = "7–15 ngày làm việc";
    public string ReasonLabel { get; init; } = "Lý do duyệt *";
    public string ReasonValue { get; init; } = "Vé đủ điều kiện; khách xác nhận mức phí hoàn.";
    public string NoteTitle { get; init; } = "Duyệt chưa có nghĩa tiền đã hoàn";
    public string NoteText { get; init; } =
        "Xác nhận sẽ tạo lệnh hoàn và chuyển sang “Đã duyệt · Chờ hoàn tiền”, đồng thời thông báo khách. " +
        "Chỉ hoàn tất khi cổng thanh toán xác nhận; hiện chưa tạo lệnh.";
    public string ConfirmCheckText { get; init; } =
        "Tôi đã kiểm tra số tiền 1.380.000 ₫ và nguồn nhận hoàn Visa ••••1762.";
    public string CancelHref { get; init; } = "/quan-ly/doi-hoan";
    public string CancelText { get; init; } = "Hủy thao tác";
    public string SubmitText { get; init; } = "Duyệt và tạo lệnh hoàn";
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

public sealed class ManagerReportsViewModel
{
    public string Subtitle { get; init; } =
        "Theo dõi hiệu quả bán vé, doanh thu và biến động đổi / hoàn.";

    public string PeriodLabel { get; init; } = "Khoảng thời gian";
    public string PeriodValue { get; init; } = "01/10/2026 – 31/10/2026";
    public string AirlineLabel { get; init; } = "Hãng hàng không";
    public string AirlineValue { get; init; } = "Tất cả hãng";
    public string RouteLabel { get; init; } = "Tuyến bay";
    public string RouteValue { get; init; } = "Tất cả tuyến";
    public string ViewText { get; init; } = "Xem báo cáo";
    public string ExportText { get; init; } = "Xuất báo cáo";

    public IReadOnlyList<ManagerKpi> Kpis { get; init; } = Array.Empty<ManagerKpi>();

    public string TrendTitle { get; init; } = "Xu hướng doanh thu";
    public string TrendSubtitle { get; init; } = "Tháng 05–10/2026 · Đơn vị: tỷ VND";
    public string ChartUnit { get; init; } = "Đơn vị: tỷ VND";
    public string ChartLegend { get; init; } = "● Doanh thu gộp";
    public IReadOnlyList<ManagerRevenueBar> Bars { get; init; } = Array.Empty<ManagerRevenueBar>();
    public decimal BarsMax => Bars.Count == 0 ? 1m : Bars.Max(bar => bar.Value);
    public int HeightPercent(ManagerRevenueBar bar) =>
        BarsMax <= 0 ? 0 : (int)Math.Round(bar.Value / BarsMax * 100m);

    public string AirlineTitle { get; init; } = "Doanh thu theo hãng";
    public string AirlineSubtitle { get; init; } = "Tháng 10/2026 · Tổng 18,6 tỷ ₫";
    public IReadOnlyList<ManagerAirlineRevenue> Airlines { get; init; } = Array.Empty<ManagerAirlineRevenue>();

    public string RoutesTitle { get; init; } = "Chi tiết doanh thu theo tuyến";
    public string RoutesSubtitle { get; init; } =
        "Doanh thu vé và dịch vụ trước hoàn tiền · Đơn vị tiền tệ: VND";
    public IReadOnlyList<ManagerRouteRevenue> Routes { get; init; } = Array.Empty<ManagerRouteRevenue>();
    public string Note { get; init; } =
        "Cơ sở: 10.000 đặt chỗ tạo trong tháng, 240 đơn hủy; 9.600 vé đã xuất, 173 vé đã hoàn. " +
        "160 đặt chỗ còn lại chưa xuất vé tại cuối kỳ. Tiền hoàn chỉ tính giao dịch đã xác nhận thành công.";
}
