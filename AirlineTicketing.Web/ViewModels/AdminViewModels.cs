using AirlineTicketing.Web.Infrastructure;

namespace AirlineTicketing.Web.ViewModels;

// ------------------------------------------------------------- admin chrome

public sealed record AdminNavItem(string Key, string Label, string EnglishLabel, string Icon, string Href)
{
    public string DisplayLabel => UiLanguage.T(Label, EnglishLabel);
}

public static class AdminNav
{
    public static readonly IReadOnlyList<AdminNavItem> Items = new[]
    {
<<<<<<< Updated upstream
        new AdminNavItem("overview", "Tổng quan hệ thống", "chart", "/admin"),
        new AdminNavItem("accounts", "Tài khoản", "users", "/QuanLyTaiKhoan"),
        new AdminNavItem("roles", "Vai trò & phân quyền", "shield-check", "/QuanLyTaiKhoan"),
        new AdminNavItem("catalog", "Danh mục hệ thống", "building", "/admin/danh-muc"),
        new AdminNavItem("settings", "Cấu hình nghiệp vụ", "sliders", "/admin/cau-hinh"),
        new AdminNavItem("audit", "Nhật ký hoạt động", "clock", "/admin/nhat-ky")
=======
        new AdminNavItem("overview", "Tổng quan hệ thống", "System overview", "chart", "/admin"),
        new AdminNavItem("accounts", "Tài khoản", "Accounts", "users", "/admin/tai-khoan"),
        new AdminNavItem("roles", "Vai trò & phân quyền", "Roles & permissions", "shield-check", "/admin/vai-tro"),
        new AdminNavItem("catalog", "Danh mục hệ thống", "System catalog", "building", "/admin/danh-muc"),
        new AdminNavItem("settings", "Cấu hình nghiệp vụ", "Business settings", "sliders", "/admin/cau-hinh"),
        new AdminNavItem("audit", "Nhật ký hoạt động", "Activity log", "clock", "/admin/nhat-ky")
>>>>>>> Stashed changes
    };

    public static string LabelFor(string? key) =>
        (Items.FirstOrDefault(item => item.Key == key) ?? Items[0]).DisplayLabel;
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
    public string StatusText { get; init; } = UiLanguage.T("Hệ thống hoạt động bình thường", "System operating normally");
    public string StatusMeta { get; init; } = UiLanguage.T("Cập nhật 10:00, 01/11/2026", "Updated at 10:00 on 01/11/2026");
    public string PeriodText { get; init; } = "01/10/2026 – 31/10/2026";

    public IReadOnlyList<AdminKpi> Kpis { get; init; } = Array.Empty<AdminKpi>();

    public string RevenueTitle { get; init; } = UiLanguage.T("Doanh thu theo tháng", "Monthly revenue");
    public string RevenueSubtitle { get; init; } = UiLanguage.T("Tháng 05–10/2026 · Vé và dịch vụ, trước hoàn tiền", "May–October 2026 · Tickets and services before refunds");
    public string RevenueLinkText { get; init; } = UiLanguage.T("Xem báo cáo", "View reports");
    public string RevenueLinkHref { get; init; } = "/admin/nhat-ky";
    public string ChartUnitText { get; init; } = UiLanguage.T("Đơn vị: tỷ VND", "Unit: billion VND");
    public string ChartLegendText { get; init; } = UiLanguage.T("● Doanh thu gộp", "● Gross revenue");
    public IReadOnlyList<AdminRevenuePoint> Revenue { get; init; } = Array.Empty<AdminRevenuePoint>();

    public string HealthTitle { get; init; } = UiLanguage.T("Sức khỏe dịch vụ", "Service health");
    public string HealthSubtitle { get; init; } = UiLanguage.T("Kiểm tra gần nhất lúc 10:00", "Last checked at 10:00");
    public IReadOnlyList<AdminHealthRow> Health { get; init; } = Array.Empty<AdminHealthRow>();
    public string HealthNoticeTitle { get; init; } = UiLanguage.T("Sao lưu thành công", "Backup succeeded");
    public string HealthNoticeText { get; init; } = UiLanguage.T("Bản gần nhất: 01/11/2026 · 02:00", "Latest: 01/11/2026 · 02:00");

    public string ActivityTitle { get; init; } = UiLanguage.T("Hoạt động gần đây", "Recent activity");
    public string ActivityLinkText { get; init; } = UiLanguage.T("Tất cả nhật ký", "View all activity");
    public string ActivityLinkHref { get; init; } = "/admin/nhat-ky";
    public IReadOnlyList<AdminActivityRow> Activity { get; init; } = Array.Empty<AdminActivityRow>();

    public string AttentionTitle { get; init; } = UiLanguage.T("Cần chú ý", "Needs attention");
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
<<<<<<< Updated upstream
    public string PrimaryActionText { get; init; } = "Chỉnh sửa tài khoản";
    public string DangerActionText { get; init; } = "Khóa tài khoản";
=======
    public string PrimaryActionText { get; init; } = UiLanguage.T("Chỉnh sửa tài khoản", "Edit account");
    public string PrimaryActionHref { get; init; } = "/admin/tai-khoan/sua";
    public string DangerActionText { get; init; } = UiLanguage.T("Khóa tài khoản", "Lock account");
>>>>>>> Stashed changes
    public string DangerActionHref { get; init; } = "/admin/tai-khoan/khoa";
}

public sealed class AdminLockDialogViewModel
{
    public bool IsLocking { get; init; }
    public string AccountName { get; init; } = "Lê Quốc Long";
    public string Email { get; init; } = "long.manager@example.com";
    public string RoleStatus { get; init; } = "Manager · Hoạt động";
    public string Reason { get; init; } = "Tạm ngừng quyền truy cập theo yêu cầu quản lý nhân sự.";
    public string SubmitHref { get; init; } = "/admin/tai-khoan/khoa";
    public string CancelHref { get; init; } = "/admin/tai-khoan";
}

<<<<<<< Updated upstream
=======
public sealed record AdminAccountFormViewModel
{
    public int Id { get; init; }
    public bool IsEdit => Id > 0;
    public string Title { get; init; } = UiLanguage.T("Tạo tài khoản nhân sự", "Create staff account");
    public string SubmitHref { get; init; } = "/admin/tai-khoan/tao";
    public string CancelHref { get; init; } = "/admin/tai-khoan";
    public string HoTen { get; init; } = string.Empty;
    public string TenDangNhap { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string SoDienThoai { get; init; } = string.Empty;
    public string MaNhanSu { get; init; } = string.Empty;
    public string VaiTro { get; init; } = "Manager";
    public string TrangThai { get; init; } = "HoatDong";
    public string MatKhau { get; init; } = string.Empty;
    public string? Error { get; init; }
}

>>>>>>> Stashed changes
public sealed class AdminAccountsViewModel
{
    public IReadOnlyList<AdminKpi> Stats { get; init; } = Array.Empty<AdminKpi>();
    public IReadOnlyList<AdminAccountRow> Accounts { get; init; } = Array.Empty<AdminAccountRow>();
    public AdminAccountDetailViewModel Detail { get; init; } = new();
    public string ResultText { get; init; } = "Hiển thị 1–5 trong 12.534 tài khoản";
    public string SearchPlaceholder { get; init; } = UiLanguage.T("Tên hoặc email", "Name or email");
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
    public string PolicyVersion { get; init; } = UiLanguage.T("Chính sách v1.3", "Policy v1.3");
    public string PermissionLegend { get; init; } =
        UiLanguage.T("X = Xem · T = Tạo · S = Sửa · D = Duyệt · C = Cấu hình · — = Không được phép", "X = View · T = Create · S = Edit · D = Approve · C = Configure · — = No access");
    public string PermissionFootnote { get; init; } =
        UiLanguage.T(
            "* Xuất vé chỉ khi đã xác nhận thanh toán; hoàn tiền theo điều kiện và hạn mức được duyệt. ** Nhật ký là dữ liệu chỉ đọc; không vai trò nào được sửa hoặc xóa bản ghi.",
            "* Issue tickets after payment is confirmed. Refunds follow approved terms and limits. ** The audit log is read-only for all roles.");

    public string LimitsTitle { get; init; } = UiLanguage.T("Giới hạn bắt buộc của Manager", "Operations manager limits");
    public string LimitTitle { get; init; } = UiLanguage.T("Không được nâng quyền hoặc đổi cấu hình bảo mật", "Privilege and security changes require administrator access");
    public string LimitText { get; init; } =
        UiLanguage.T("Không tạo Admin, sửa vai trò, thay đổi tích hợp thanh toán hoặc thời gian giữ chỗ.",
            "Administrators manage account creation, role changes, payment integrations and booking hold times.");
    public string LimitNote { get; init; } =
        UiLanguage.T(
            "Được cập nhật lịch khai thác và giá theo hạng trong ngưỡng cấu hình. Khi đổi lịch, bắt buộc thông báo cho hành khách có đặt chỗ.",
            "Managers can update schedules and cabin fares within approved ranges. Notify booked passengers when a schedule changes.");

    public string ControlTitle { get; init; } = UiLanguage.T("Kiểm soát thay đổi", "Change controls");
    public string ControlStatusLabel { get; init; } = UiLanguage.T("Trạng thái", "Status");
    public string ControlStatus { get; init; } = UiLanguage.T("Đang áp dụng", "In effect");
    public string ControlUpdatedLabel { get; init; } = UiLanguage.T("Cập nhật cuối", "Last updated");
    public string ControlUpdated { get; init; } = "28/10/2026 · 16:20";
    public string ControlActorLabel { get; init; } = UiLanguage.T("Người cập nhật", "Updated by");
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
    public string NoteTitle { get; init; } = UiLanguage.T("Dữ liệu nền, không phải lịch khai thác", "Reference data for operations");
    public string NoteText { get; init; } =
        UiLanguage.T(
            "Danh mục xác định sân bay và tuyến hợp lệ. Ngày bay, giờ bay, tồn chỗ và giá từng chuyến được quản lý tại khu vực Manager.",
            "The catalog defines valid airports and routes. Manage flight dates, times, availability and fares in Operations.");

    public string SectionTitle { get; init; } = UiLanguage.T("Sân bay nội địa", "Domestic airports");
    public string SectionSubtitle { get; init; } = "22 sân bay · 21 đang hoạt động";
    public string SearchLabel { get; init; } = UiLanguage.T("Tìm theo tên / IATA", "Search by name / IATA");
    public string SearchPlaceholder { get; init; } = UiLanguage.T("Tên sân bay hoặc mã", "Airport name or code");
    public string StatusLabel { get; init; } = UiLanguage.T("Trạng thái", "Status");
    public string StatusFilter { get; init; } = "Tất cả";
    public string ApplyText { get; init; } = UiLanguage.T("Áp dụng", "Apply");
    public AdminCatalogColumns Columns { get; init; } = new("IATA", "Sân bay / Thành phố", "Múi giờ", "Trạng thái");
    public IReadOnlyList<AdminCatalogRow> Rows { get; init; } = Array.Empty<AdminCatalogRow>();
    public string ResultText { get; init; } = "Hiển thị 1–6 trong 22 sân bay";

    public string EditorTitle { get; init; } = UiLanguage.T("Chỉnh sửa sân bay", "Edit airport");
    public string EditorSubtitle { get; init; } = "SGN · Dữ liệu đang được sử dụng";
    public IReadOnlyList<AdminEditorField> EditorFields { get; init; } = Array.Empty<AdminEditorField>();
    public string EditorNote { get; init; } =
        "48 tuyến bay đang tham chiếu SGN. Không xóa dữ liệu đã phát sinh đặt chỗ; " +
        "có thể ngừng sử dụng cho chuyến mới.";
    public string EditorUpdated { get; init; } = "Cập nhật: 20/10/2026 · Admin";
<<<<<<< Updated upstream
    public string AddText { get; init; } = "Thêm sân bay";
=======
    public string AddText { get; init; } = UiLanguage.T("Thêm sân bay", "Add airport");

    /// <summary>Id bản ghi đang sửa; 0 nghĩa là tạo mới.</summary>
    public int EditorId { get; init; }
    public string EditorTabKey { get; init; } = "airports";
    public string? Message { get; set; }
    public string MessageTone { get; set; } = "info";
>>>>>>> Stashed changes
}

// ---------------------------------------------------------- 12 · cấu hình

public sealed record AdminSettingField(string Label, string Value, string? Hint = null, string? Icon = null);

public sealed class AdminPaymentViewModel
{
    public string Name { get; init; } = "VNPay";
    public string StatusText { get; init; } = UiLanguage.T("Đã kết nối", "Connected");
    public string StatusTone { get; init; } = "success";
    public string EnvironmentLabel { get; init; } = UiLanguage.T("Môi trường", "Environment");
    public string Environment { get; init; } = UiLanguage.T("Thử nghiệm (Sandbox)", "Sandbox");
    public string MerchantLabel { get; init; } = UiLanguage.T("Mã đối tác", "Merchant code");
    public string MerchantId { get; init; } = "AEROVIET_DEMO";
    public string SecretLabel { get; init; } = UiLanguage.T("Khóa xác thực", "Authentication key");
    public string SecretMasked { get; init; } = "••••••••••••••••";
    public string Note { get; init; } = UiLanguage.T("Chỉ hiển thị trạng thái, không lộ khóa bí mật.", "Only the connection status is displayed.");
    public string CheckText { get; init; } = UiLanguage.T("Kiểm tra kết nối", "Check connection");
}

public sealed class AdminSettingsViewModel
{
    public string VersionText { get; init; } = UiLanguage.T("Đang áp dụng · v1.3", "In effect · v1.3");
    public string DirtyText { get; init; } = UiLanguage.T("Có 2 thay đổi chưa lưu", "2 unsaved changes");
    public string UpdatedText { get; init; } = "Cập nhật gần nhất: 01/11/2026 · 09:50 · Trần Hoàng Tùng";

    public string BookingTitle { get; init; } = UiLanguage.T("01  Đặt chỗ & phí dịch vụ", "01  Bookings & service fees");
    public IReadOnlyList<AdminSettingField> BookingFields { get; init; } = Array.Empty<AdminSettingField>();
    public string ExampleTitle { get; init; } = UiLanguage.T("Ví dụ SGN → HAN · VN210", "Example: SGN → HAN · VN210");
    public string ExampleText { get; init; } =
        "Giá cơ bản 1.500.000 ₫ + thuế/phí 380.000 ₫ + dịch vụ 70.000 ₫ = 1.950.000 ₫, " +
        "chưa mua thêm hành lý.";

    public string PolicyTitle { get; init; } = UiLanguage.T("02  Chính sách đổi / hoàn mặc định", "02  Default change / refund policy");
    public string PolicySubtitle { get; init; } =
        UiLanguage.T("Phổ thông Tiêu chuẩn · Ưu tiên điều kiện thực tế từ hãng bay",
            "Standard Economy · Airline terms take priority");
    public IReadOnlyList<AdminSettingField> PolicyFields { get; init; } = Array.Empty<AdminSettingField>();
    public IReadOnlyList<string> PolicyChecks { get; init; } = Array.Empty<string>();

    public string PaymentTitle { get; init; } = UiLanguage.T("03  Tích hợp thanh toán", "03  Payment integration");
    public AdminPaymentViewModel Payment { get; init; } = new();

    public string NotificationTitle { get; init; } = UiLanguage.T("04  Thông báo", "04  Notifications");
    public IReadOnlyList<AdminSettingField> NotificationFields { get; init; } = Array.Empty<AdminSettingField>();
    public AdminSettingField EmailSender { get; init; } = new("Email gửi hệ thống", "no-reply@aeroviet.vn");

    public string FooterNote { get; init; } = UiLanguage.T("Thay đổi không ảnh hưởng các vé đã xuất.", "Changes apply to newly issued tickets.");
    public string SaveNoteTitle { get; init; } = UiLanguage.T("Chỉ Admin được chỉnh sửa", "Administrator access required to edit");
    public string SaveNoteText { get; init; } =
        UiLanguage.T("Thay đổi được kiểm tra hợp lệ và ghi lại vào nhật ký trước khi áp dụng.",
            "Changes are validated and recorded before they take effect.");
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
    public string FilterText { get; init; } = UiLanguage.T("Lọc nhật ký", "Filter log");
    public string ExportText { get; init; } = UiLanguage.T("Xuất nhật ký", "Export log");

    public IReadOnlyList<AdminAuditRow> Rows { get; init; } = Array.Empty<AdminAuditRow>();
    public string ResultText { get; init; } = "Hiển thị 1–6 trong 284 sự kiện hôm nay";
    public AdminAuditDetailViewModel Detail { get; init; } = new();

    public string IntegrityTitle { get; init; } = UiLanguage.T("Tính toàn vẹn nhật ký", "Log integrity");
    public string IntegrityStatus { get; init; } = UiLanguage.T("Đã ghi nhận thành công", "Recorded successfully");
    public string IntegrityNoteTitle { get; init; } = UiLanguage.T("Lưu vết không chỉnh sửa", "Read-only audit trail");
    public string IntegrityNoteText { get; init; } =
        UiLanguage.T("Ghi nhận cả thao tác thất bại và truy cập bị chặn. Dữ liệu nhạy cảm được che trước khi lưu.",
            "Failed actions and denied access are logged. Sensitive data is masked before storage.");
    public string IntegrityCode { get; init; } = "Mã đối chiếu: AUD-0284-20261101";
}
