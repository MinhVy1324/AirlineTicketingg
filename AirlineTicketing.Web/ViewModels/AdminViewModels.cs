namespace AirlineTicketing.Web.ViewModels;

// ------------------------------------------------------------- admin chrome

public sealed record AdminNavItem(string Key, string Label, string Icon, string Href);

public static class AdminNav
{
    public static readonly IReadOnlyList<AdminNavItem> Items = new[]
    {
        new AdminNavItem("overview", "Tổng quan hệ thống", "chart", "/admin"),
        new AdminNavItem("accounts", "Tài khoản", "users", "/QuanLyTaiKhoan"),
        new AdminNavItem("roles", "Vai trò & phân quyền", "shield-check", "/QuanLyTaiKhoan"),
        new AdminNavItem("catalog", "Danh mục hệ thống", "building", "/admin/danh-muc"),
        new AdminNavItem("settings", "Cấu hình nghiệp vụ", "sliders", "/admin/cau-hinh"),
        new AdminNavItem("audit", "Nhật ký hoạt động", "clock", "/admin/nhat-ky")
    };

    public static string LabelFor(string? key) =>
        Items.FirstOrDefault(item => item.Key == key)?.Label ?? "Tổng quan hệ thống";
}

public sealed record AdminDetailField(string Label, string Value);

public sealed record AdminKpi(string Label, string Value, string Meta, string MetaTone, string Icon);

// ----------------------------------------------------------- 08 · tổng quan

public sealed record AdminRevenuePoint(string Label, string ValueText, decimal Value, bool IsCurrent);

public sealed record AdminHealthRow(string Label, string Value);

public sealed record AdminActivityRow(string Time, string Actor, string Action, string ResultText, string ResultTone);

public sealed record AdminAttentionItem(
    string Icon,
    string Title,
    string Text,
    string Tone = "warning",
    string? LinkText = null,
    string? Href = null);

public sealed class AdminOverviewViewModel
{
    public string StatusText { get; init; } = "Hệ thống hoạt động bình thường";
    public string StatusMeta { get; init; } = "Cập nhật 10:00, 01/11/2026";
    public string PeriodText { get; init; } = "01/10/2026 – 31/10/2026";

    public IReadOnlyList<AdminKpi> Kpis { get; init; } = Array.Empty<AdminKpi>();

    public string RevenueTitle { get; init; } = "Doanh thu theo tháng";
    public string RevenueSubtitle { get; init; } = "Tháng 05–10/2026 · Vé và dịch vụ, trước hoàn tiền";
    public string RevenueLinkText { get; init; } = "Xem báo cáo";
    public string RevenueLinkHref { get; init; } = "/admin/nhat-ky";
    public string ChartUnitText { get; init; } = "Đơn vị: tỷ VND";
    public string ChartLegendText { get; init; } = "● Doanh thu gộp";
    public IReadOnlyList<AdminRevenuePoint> Revenue { get; init; } = Array.Empty<AdminRevenuePoint>();

    public string HealthTitle { get; init; } = "Sức khỏe dịch vụ";
    public string HealthSubtitle { get; init; } = "Kiểm tra gần nhất lúc 10:00";
    public IReadOnlyList<AdminHealthRow> Health { get; init; } = Array.Empty<AdminHealthRow>();
    public string HealthNoticeTitle { get; init; } = "Sao lưu thành công";
    public string HealthNoticeText { get; init; } = "Bản gần nhất: 01/11/2026 · 02:00";

    public string ActivityTitle { get; init; } = "Hoạt động gần đây";
    public string ActivityLinkText { get; init; } = "Tất cả nhật ký";
    public string ActivityLinkHref { get; init; } = "/admin/nhat-ky";
    public IReadOnlyList<AdminActivityRow> Activity { get; init; } = Array.Empty<AdminActivityRow>();

    public string AttentionTitle { get; init; } = "Cần chú ý";
    public IReadOnlyList<AdminAttentionItem> Attention { get; init; } = Array.Empty<AdminAttentionItem>();

    public decimal RevenueMax =>
        Revenue.Count == 0 ? 1m : Revenue.Max(point => point.Value);

    public int HeightPercent(AdminRevenuePoint point) =>
        RevenueMax <= 0 ? 0 : (int)Math.Round(point.Value / RevenueMax * 100m);
}

// ----------------------------------------------------------- 09 · tài khoản

public sealed record AdminAccountRow(
    string Name,
    string Email,
    string Role,
    string StatusText,
    string StatusTone,
    string LastLogin,
    string ActionText,
    string ActionHref,
    bool IsSelected = false);

public sealed class AdminAccountDetailViewModel
{
    public string Title { get; init; } = "Chi tiết · Lê Quốc Long";
    public string Subtitle { get; init; } = "Mã nhân sự NV-002 · Tạo ngày 15/09/2026";
    public IReadOnlyList<AdminDetailField> Fields { get; init; } = Array.Empty<AdminDetailField>();
    public string PrimaryActionText { get; init; } = "Chỉnh sửa tài khoản";
    public string DangerActionText { get; init; } = "Khóa tài khoản";
    public string DangerActionHref { get; init; } = "/admin/tai-khoan/khoa";
}

public sealed class AdminLockDialogViewModel
{
    public string AccountName { get; init; } = "Lê Quốc Long";
    public string Email { get; init; } = "long.manager@example.com";
    public string RoleStatus { get; init; } = "Manager · Hoạt động";
    public string Reason { get; init; } = "Tạm ngừng quyền truy cập theo yêu cầu quản lý nhân sự.";
    public string SubmitHref { get; init; } = "/admin/tai-khoan/khoa";
    public string CancelHref { get; init; } = "/admin/tai-khoan";
}

public sealed class AdminAccountsViewModel
{
    public IReadOnlyList<AdminKpi> Stats { get; init; } = Array.Empty<AdminKpi>();
    public IReadOnlyList<AdminAccountRow> Accounts { get; init; } = Array.Empty<AdminAccountRow>();
    public AdminAccountDetailViewModel Detail { get; init; } = new();
    public string ResultText { get; init; } = "Hiển thị 1–5 trong 12.534 tài khoản";
    public string SearchPlaceholder { get; init; } = "Tên hoặc email";
    public string RoleFilter { get; init; } = "Tất cả vai trò";
    public string StatusFilter { get; init; } = "Tất cả trạng thái";
    public bool ShowLockDialog { get; init; }
    public AdminLockDialogViewModel LockDialog { get; init; } = new();
}

// ------------------------------------------------- 10 · vai trò & phân quyền

public sealed record AdminRoleCard(string Name, string AccountCount, string Description);

public sealed record AdminPermissionRow(string Group, string Admin, string Manager, string Customer);

public sealed class AdminRolesViewModel
{
    public IReadOnlyList<AdminRoleCard> Roles { get; init; } = Array.Empty<AdminRoleCard>();
    public IReadOnlyList<AdminPermissionRow> Permissions { get; init; } = Array.Empty<AdminPermissionRow>();
    public string PolicyVersion { get; init; } = "Chính sách v1.3";
    public string PermissionLegend { get; init; } =
        "X = Xem · T = Tạo · S = Sửa · D = Duyệt · C = Cấu hình · — = Không được phép";
    public string PermissionFootnote { get; init; } =
        "* Xuất vé chỉ khi đã xác nhận thanh toán; hoàn tiền theo điều kiện và hạn mức được duyệt. " +
        "** Nhật ký là dữ liệu chỉ đọc; không vai trò nào được sửa hoặc xóa bản ghi.";

    public string LimitsTitle { get; init; } = "Giới hạn bắt buộc của Manager";
    public string LimitTitle { get; init; } = "Không được nâng quyền hoặc đổi cấu hình bảo mật";
    public string LimitText { get; init; } =
        "Không tạo Admin, sửa vai trò, thay đổi tích hợp thanh toán hoặc thời gian giữ chỗ.";
    public string LimitNote { get; init; } =
        "Được cập nhật lịch khai thác và giá theo hạng trong ngưỡng cấu hình. " +
        "Khi đổi lịch, bắt buộc thông báo cho hành khách có đặt chỗ.";

    public string ControlTitle { get; init; } = "Kiểm soát thay đổi";
    public string ControlStatusLabel { get; init; } = "Trạng thái";
    public string ControlStatus { get; init; } = "Đang áp dụng";
    public string ControlUpdatedLabel { get; init; } = "Cập nhật cuối";
    public string ControlUpdated { get; init; } = "28/10/2026 · 16:20";
    public string ControlActorLabel { get; init; } = "Người cập nhật";
    public string ControlActor { get; init; } = "Trần Hoàng Tùng";
}

// ---------------------------------------------------------- 11 · danh mục

public sealed record AdminCatalogTab(string Key, string Label, string CountText, bool IsActive, string Href);

public sealed record AdminCatalogColumns(string Code, string Title, string Meta, string Status);

public sealed record AdminCatalogRow(
    string Code,
    string Title,
    string? Subtitle,
    string Meta,
    string StatusText,
    bool IsSelected = false);

public sealed record AdminEditorField(
    string Label,
    string Value,
    string? Hint = null,
    string? Icon = null,
    bool IsRequired = false);

public sealed class AdminCatalogViewModel
{
    public IReadOnlyList<AdminCatalogTab> Tabs { get; init; } = Array.Empty<AdminCatalogTab>();
    public string NoteTitle { get; init; } = "Dữ liệu nền, không phải lịch khai thác";
    public string NoteText { get; init; } =
        "Danh mục xác định sân bay và tuyến hợp lệ. Ngày bay, giờ bay, tồn chỗ và giá từng chuyến " +
        "được quản lý tại khu vực Manager.";

    public string SectionTitle { get; init; } = "Sân bay nội địa";
    public string SectionSubtitle { get; init; } = "22 sân bay · 21 đang hoạt động";
    public string SearchLabel { get; init; } = "Tìm theo tên / IATA";
    public string SearchPlaceholder { get; init; } = "Tên sân bay hoặc mã";
    public string StatusLabel { get; init; } = "Trạng thái";
    public string StatusFilter { get; init; } = "Tất cả";
    public string ApplyText { get; init; } = "Áp dụng";
    public AdminCatalogColumns Columns { get; init; } = new("IATA", "Sân bay / Thành phố", "Múi giờ", "Trạng thái");
    public IReadOnlyList<AdminCatalogRow> Rows { get; init; } = Array.Empty<AdminCatalogRow>();
    public string ResultText { get; init; } = "Hiển thị 1–6 trong 22 sân bay";

    public string EditorTitle { get; init; } = "Chỉnh sửa sân bay";
    public string EditorSubtitle { get; init; } = "SGN · Dữ liệu đang được sử dụng";
    public IReadOnlyList<AdminEditorField> EditorFields { get; init; } = Array.Empty<AdminEditorField>();
    public string EditorNote { get; init; } =
        "48 tuyến bay đang tham chiếu SGN. Không xóa dữ liệu đã phát sinh đặt chỗ; " +
        "có thể ngừng sử dụng cho chuyến mới.";
    public string EditorUpdated { get; init; } = "Cập nhật: 20/10/2026 · Admin";
    public string AddText { get; init; } = "Thêm sân bay";
}

// ---------------------------------------------------------- 12 · cấu hình

public sealed record AdminSettingField(string Label, string Value, string? Hint = null, string? Icon = null);

public sealed class AdminPaymentViewModel
{
    public string Name { get; init; } = "VNPay";
    public string StatusText { get; init; } = "Đã kết nối";
    public string StatusTone { get; init; } = "success";
    public string EnvironmentLabel { get; init; } = "Môi trường";
    public string Environment { get; init; } = "Thử nghiệm (Sandbox)";
    public string MerchantLabel { get; init; } = "Mã đối tác";
    public string MerchantId { get; init; } = "AEROVIET_DEMO";
    public string SecretLabel { get; init; } = "Khóa xác thực";
    public string SecretMasked { get; init; } = "••••••••••••••••";
    public string Note { get; init; } = "Chỉ hiển thị trạng thái, không lộ khóa bí mật.";
    public string CheckText { get; init; } = "Kiểm tra kết nối";
}

public sealed class AdminSettingsViewModel
{
    public string VersionText { get; init; } = "Đang áp dụng · v1.3";
    public string DirtyText { get; init; } = "Có 2 thay đổi chưa lưu";
    public string UpdatedText { get; init; } = "Cập nhật gần nhất: 01/11/2026 · 09:50 · Trần Hoàng Tùng";

    public string BookingTitle { get; init; } = "01  Đặt chỗ & phí dịch vụ";
    public IReadOnlyList<AdminSettingField> BookingFields { get; init; } = Array.Empty<AdminSettingField>();
    public string ExampleTitle { get; init; } = "Ví dụ SGN → HAN · VN210";
    public string ExampleText { get; init; } =
        "Giá cơ bản 1.500.000 ₫ + thuế/phí 380.000 ₫ + dịch vụ 70.000 ₫ = 1.950.000 ₫, " +
        "chưa mua thêm hành lý.";

    public string PolicyTitle { get; init; } = "02  Chính sách đổi / hoàn mặc định";
    public string PolicySubtitle { get; init; } =
        "Phổ thông Tiêu chuẩn · Ưu tiên điều kiện thực tế từ hãng bay";
    public IReadOnlyList<AdminSettingField> PolicyFields { get; init; } = Array.Empty<AdminSettingField>();
    public IReadOnlyList<string> PolicyChecks { get; init; } = Array.Empty<string>();

    public string PaymentTitle { get; init; } = "03  Tích hợp thanh toán";
    public AdminPaymentViewModel Payment { get; init; } = new();

    public string NotificationTitle { get; init; } = "04  Thông báo";
    public IReadOnlyList<AdminSettingField> NotificationFields { get; init; } = Array.Empty<AdminSettingField>();
    public AdminSettingField EmailSender { get; init; } = new("Email gửi hệ thống", "no-reply@aeroviet.vn");

    public string FooterNote { get; init; } = "Thay đổi không ảnh hưởng các vé đã xuất.";
    public string SaveNoteTitle { get; init; } = "Chỉ Admin được chỉnh sửa";
    public string SaveNoteText { get; init; } =
        "Thay đổi được kiểm tra hợp lệ và ghi lại vào nhật ký trước khi áp dụng.";
}

// --------------------------------------------------------- 13 · nhật ký

public sealed record AdminAuditRow(
    string Time,
    string Actor,
    string Action,
    string Target,
    string ResultText,
    string ResultTone);

public sealed class AdminAuditDetailViewModel
{
    public string Title { get; init; } = "Chi tiết · LOG-20261101-0284";
    public string Subtitle { get; init; } = "Cập nhật phí dịch vụ · 09:50:12 ngày 01/11/2026";
    public IReadOnlyList<AdminDetailField> Left { get; init; } = Array.Empty<AdminDetailField>();
    public IReadOnlyList<AdminDetailField> Right { get; init; } = Array.Empty<AdminDetailField>();
    public string Content { get; init; } =
        "Nội dung: xác nhận lại mức phí hiện hành; bổ sung ghi chú “Không hoàn phí dịch vụ”. " +
        "Không thay đổi số tiền.";
}

public sealed class AdminAuditViewModel
{
    public string FromDate { get; init; } = "01/11/2026";
    public string ToDate { get; init; } = "01/11/2026";
    public string ActorFilter { get; init; } = "Tất cả người dùng";
    public string TypeFilter { get; init; } = "Tất cả thao tác";
    public string FilterText { get; init; } = "Lọc nhật ký";
    public string ExportText { get; init; } = "Xuất nhật ký";

    public IReadOnlyList<AdminAuditRow> Rows { get; init; } = Array.Empty<AdminAuditRow>();
    public string ResultText { get; init; } = "Hiển thị 1–6 trong 284 sự kiện hôm nay";
    public AdminAuditDetailViewModel Detail { get; init; } = new();

    public string IntegrityTitle { get; init; } = "Tính toàn vẹn nhật ký";
    public string IntegrityStatus { get; init; } = "Đã ghi nhận thành công";
    public string IntegrityNoteTitle { get; init; } = "Lưu vết không chỉnh sửa";
    public string IntegrityNoteText { get; init; } =
        "Ghi nhận cả thao tác thất bại và truy cập bị chặn. Dữ liệu nhạy cảm được che trước khi lưu.";
    public string IntegrityCode { get; init; } = "Mã đối chiếu: AUD-0284-20261101";
}
