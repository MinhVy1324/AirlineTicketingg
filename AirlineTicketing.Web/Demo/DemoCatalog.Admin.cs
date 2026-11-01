using AirlineTicketing.Web.ViewModels;

namespace AirlineTicketing.Web.Demo;

public static partial class DemoCatalog
{
    public const string AdminName = "Trần Hoàng Tùng";
    public const string AdminInitials = "HT";
    public const string AdminRole = "Quản trị viên";

    // --------------------------------------------------------- 08 · tổng quan

    public static AdminOverviewViewModel AdminOverview() => new()
    {
        Kpis = new[]
        {
            new AdminKpi("Doanh thu gộp · Tháng 10", "18,6 tỷ ₫", "↑ 8,1% so với tháng 9", "success", "chart"),
            new AdminKpi("Vé đã xuất · Tháng 10", "9.600", "↑ 600 vé so với tháng 9", "success", "ticket"),
            new AdminKpi("Tài khoản đang hoạt động", "12.486", "Tại 10:00 · 01/11/2026", "info", "users"),
            new AdminKpi("Tính sẵn sàng · Tháng 10", "99,98%", "Mục tiêu ≥ 99,9%", "success", "activity")
        },
        Revenue = new[]
        {
            new AdminRevenuePoint("T05", "13,2", 13.2m, false),
            new AdminRevenuePoint("T06", "14,1", 14.1m, false),
            new AdminRevenuePoint("T07", "15,6", 15.6m, false),
            new AdminRevenuePoint("T08", "16,4", 16.4m, false),
            new AdminRevenuePoint("T09", "17,2", 17.2m, false),
            new AdminRevenuePoint("T10", "18,6", 18.6m, true)
        },
        Health = new[]
        {
            new AdminHealthRow("Cổng thanh toán", "Kết nối ổn định"),
            new AdminHealthRow("Đồng bộ chuyến bay", "Hoạt động"),
            new AdminHealthRow("Email thông báo", "99,6% gửi thành công"),
            new AdminHealthRow("Cơ sở dữ liệu", "Phản hồi 42 ms")
        },
        Activity = new[]
        {
            new AdminActivityRow("09:50", AdminName, "Cập nhật phí dịch vụ · CFG-102", "Thành công", "success"),
            new AdminActivityRow("09:43", "Lê Quốc Long", "Xuất vé · AV8K2M", "Thành công", "success"),
            new AdminActivityRow("09:15", AdminName, "Tạo tài khoản Manager", "Thành công", "success"),
            new AdminActivityRow("08:52", "Hệ thống", "Đối soát thanh toán VNPay", "Thành công", "success")
        },
        Attention = new[]
        {
            new AdminAttentionItem(
                "alert",
                "3 giao dịch cần đối soát",
                "Đã nhận thông báo từ cổng, đang kiểm tra kết quả. Không xuất vé khi chưa xác nhận."),
            new AdminAttentionItem(
                "shield-alert",
                "5 lần đăng nhập thất bại",
                "Trong 24 giờ · 2 tài khoản bị khóa tạm.",
                "info",
                "Xem nhật ký bảo mật",
                "/admin/nhat-ky")
        }
    };

    // --------------------------------------------------------- 09 · tài khoản

    public static AdminAccountsViewModel AdminAccounts(bool showLockDialog = false) => new()
    {
        Stats = new[]
        {
            new AdminKpi("Tất cả tài khoản", "12.534", "Toàn hệ thống", "info", "users"),
            new AdminKpi("Đang hoạt động", "12.486", "Đăng nhập trong 30 ngày", "success", "check-circle"),
            new AdminKpi("Đã khóa", "48", "Cần rà soát định kỳ", "warning", "lock"),
            new AdminKpi("Nhân sự nội bộ", "18", "Admin và Manager", "info", "id-card")
        },
        Accounts = new[]
        {
            new AdminAccountRow(
                AdminName, "tung.admin@example.com", "Admin", "Hoạt động", "success",
                "01/11/2026 · 08:00", "Xem chi tiết", "/admin/tai-khoan"),
            new AdminAccountRow(
                "Lê Quốc Long", "long.manager@example.com", "Manager", "Hoạt động", "success",
                "01/11/2026 · 07:45", "Xem chi tiết", "/admin/tai-khoan", IsSelected: true),
            new AdminAccountRow(
                "Nguyễn Minh Anh", "minhanh@example.com", "Khách hàng", "Hoạt động", "success",
                "01/11/2026 · 09:20", "Xem chi tiết", "/admin/tai-khoan"),
            new AdminAccountRow(
                "Phạm Thanh Hà", "ha.manager@example.com", "Manager", "Hoạt động", "success",
                "31/10/2026 · 17:30", "Xem chi tiết", "/admin/tai-khoan"),
            new AdminAccountRow(
                "Đỗ Đức Nam", "nam.user@example.com", "Khách hàng", "Đã khóa", "danger",
                "30/10/2026 · 14:12", "Mở tài khoản", "/admin/tai-khoan")
        },
        Detail = new AdminAccountDetailViewModel
        {
            Fields = new[]
            {
                new AdminDetailField("Vai trò được cấp", "Manager · Vận hành"),
                new AdminDetailField("Email xác thực", "long.manager@example.com"),
                new AdminDetailField("Quyền bảo mật", "Không được cấp quyền")
            }
        },
        ShowLockDialog = showLockDialog
    };

    // ------------------------------------------------- 10 · vai trò & phân quyền

    public static AdminRolesViewModel AdminRoles() => new()
    {
        Roles = new[]
        {
            new AdminRoleCard("Admin", "2 tài khoản", "Toàn quyền quản trị và vận hành"),
            new AdminRoleCard("Manager", "16 tài khoản", "Vận hành trong phạm vi được cấp"),
            new AdminRoleCard("Khách hàng", "12.516 tài khoản", "Chỉ dữ liệu và đặt chỗ của chính mình")
        },
        Permissions = new[]
        {
            new AdminPermissionRow("Tài khoản & phân quyền", "X · T · S · D · C", "—", "X · S (hồ sơ cá nhân)"),
            new AdminPermissionRow("Danh mục dữ liệu nền", "X · T · S · D · C", "X", "X (dữ liệu công khai)"),
            new AdminPermissionRow("Lịch bay, giá & tồn chỗ", "X · T · S · D · C", "X · T · S", "X (chuyến được mở bán)"),
            new AdminPermissionRow("Đặt chỗ & xuất vé", "X · T · S · D · C", "X · T · S · D *", "X · T (đơn của mình)"),
            new AdminPermissionRow("Yêu cầu đổi / hoàn", "X · T · S · D · C", "X · S · D *", "X · T (đơn của mình)"),
            new AdminPermissionRow("Báo cáo kinh doanh", "X · T · S · D · C", "X · T (xuất báo cáo)", "—"),
            new AdminPermissionRow("Cấu hình & bảo mật", "X · T · S · D · C", "—", "—"),
            new AdminPermissionRow("Nhật ký hoạt động", "X (toàn hệ thống) **", "X (thao tác của mình)", "—")
        }
    };

    // ---------------------------------------------------------- 11 · danh mục

    public static AdminCatalogViewModel AdminCatalog(string? tab = null)
    {
        var key = tab?.Trim().ToLowerInvariant() switch
        {
            "hang-bay" => "airlines",
            "tuyen-bay" => "routes",
            "tau-bay" => "aircraft",
            "hang-ghe" => "cabin",
            _ => "airports"
        };

        var tabs = new[]
        {
            new AdminCatalogTab("airports", "Sân bay", "22", key == "airports", "/admin/danh-muc"),
            new AdminCatalogTab("airlines", "Hãng bay", "6", key == "airlines", "/admin/danh-muc?tab=hang-bay"),
            new AdminCatalogTab("routes", "Tuyến bay", "48", key == "routes", "/admin/danh-muc?tab=tuyen-bay"),
            new AdminCatalogTab("aircraft", "Tàu bay", "12", key == "aircraft", "/admin/danh-muc?tab=tau-bay"),
            new AdminCatalogTab("cabin", "Hạng ghế", "3", key == "cabin", "/admin/danh-muc?tab=hang-ghe")
        };

        var model = key switch
        {
            "airlines" => new AdminCatalogViewModel
            {
                Tabs = tabs,
                SectionTitle = "Hãng hàng không",
                SectionSubtitle = "6 hãng bay · 5 đang khai thác",
                SearchLabel = "Tìm theo tên / mã",
                SearchPlaceholder = "Tên hãng hoặc mã",
                Columns = new AdminCatalogColumns("Mã", "Hãng bay / Quốc gia", "Đội bay", "Trạng thái"),
                Rows = new[]
                {
                    new AdminCatalogRow("VN", "Vietnam Airlines", "Việt Nam", "78 tàu bay", "Hoạt động", true),
                    new AdminCatalogRow("VJ", "Vietjet Air", "Việt Nam", "105 tàu bay", "Hoạt động"),
                    new AdminCatalogRow("QH", "Bamboo Airways", "Việt Nam", "30 tàu bay", "Hoạt động"),
                    new AdminCatalogRow("BL", "Pacific Airlines", "Việt Nam", "18 tàu bay", "Hoạt động"),
                    new AdminCatalogRow("VU", "Vietravel Airlines", "Việt Nam", "6 tàu bay", "Hoạt động"),
                    new AdminCatalogRow("0V", "VASCO", "Việt Nam", "6 tàu bay", "Tạm dừng")
                },
                ResultText = "Hiển thị 1–6 trong 6 hãng bay",
                EditorTitle = "Chỉnh sửa hãng bay",
                EditorSubtitle = "VN · Dữ liệu đang được sử dụng",
                EditorFields = new[]
                {
                    new AdminEditorField("Mã hãng *", "VN", "Mã không đổi khi đã có chuyến khai thác.", IsRequired: true),
                    new AdminEditorField("Tên hãng *", "Vietnam Airlines", IsRequired: true),
                    new AdminEditorField("Quốc gia", "Việt Nam"),
                    new AdminEditorField("Mã ICAO", "HVN"),
                    new AdminEditorField("Trạng thái", "Đang hoạt động", Icon: "chevron-down")
                },
                EditorNote = "126 tuyến bay đang tham chiếu VN. Ngừng khai thác không xóa lịch sử đặt chỗ.",
                EditorUpdated = "Cập nhật: 20/10/2026 · Admin",
                AddText = "Thêm hãng bay"
            },
            "routes" => new AdminCatalogViewModel
            {
                Tabs = tabs,
                SectionTitle = "Tuyến bay nội địa",
                SectionSubtitle = "48 tuyến bay · 46 đang khai thác",
                SearchLabel = "Tìm theo sân bay / mã tuyến",
                SearchPlaceholder = "Ví dụ SGN hoặc SGN-HAN",
                Columns = new AdminCatalogColumns("Mã tuyến", "Tuyến bay / Loại", "Tần suất", "Trạng thái"),
                Rows = new[]
                {
                    new AdminCatalogRow("SGN-HAN", "SGN → HAN", "Nội địa", "12 chuyến/ngày", "Đang khai thác", true),
                    new AdminCatalogRow("SGN-DAD", "SGN → DAD", "Nội địa", "8 chuyến/ngày", "Đang khai thác"),
                    new AdminCatalogRow("HAN-DAD", "HAN → DAD", "Nội địa", "6 chuyến/ngày", "Đang khai thác"),
                    new AdminCatalogRow("SGN-PQC", "SGN → PQC", "Nội địa", "5 chuyến/ngày", "Đang khai thác"),
                    new AdminCatalogRow("HAN-PQC", "HAN → PQC", "Nội địa", "3 chuyến/ngày", "Đang khai thác"),
                    new AdminCatalogRow("SGN-CXR", "SGN → CXR", "Nội địa", "4 chuyến/ngày", "Đang khai thác")
                },
                ResultText = "Hiển thị 1–6 trong 48 tuyến bay",
                EditorTitle = "Chỉnh sửa tuyến bay",
                EditorSubtitle = "SGN-HAN · 12 chuyến mỗi ngày",
                EditorFields = new[]
                {
                    new AdminEditorField("Mã tuyến *", "SGN-HAN", "Mã tuyến không đổi khi đã có chuyến khai thác.", IsRequired: true),
                    new AdminEditorField("Sân bay đi *", "SGN · Tân Sơn Nhất", Icon: "chevron-down", IsRequired: true),
                    new AdminEditorField("Sân bay đến *", "HAN · Nội Bài", Icon: "chevron-down", IsRequired: true),
                    new AdminEditorField("Tần suất", "12 chuyến/ngày"),
                    new AdminEditorField("Trạng thái", "Đang khai thác", Icon: "chevron-down")
                },
                EditorNote = "Lịch bay chi tiết được quản lý tại khu vực Manager. Ngừng tuyến không ảnh hưởng vé đã xuất.",
                EditorUpdated = "Cập nhật: 20/10/2026 · Admin",
                AddText = "Thêm tuyến bay"
            },
            "aircraft" => new AdminCatalogViewModel
            {
                Tabs = tabs,
                SectionTitle = "Đội tàu bay",
                SectionSubtitle = "12 tàu bay · 11 đang khai thác",
                SearchLabel = "Tìm theo dòng / mã tàu",
                SearchPlaceholder = "Ví dụ A321 hoặc Airbus",
                Columns = new AdminCatalogColumns("Mã tàu", "Tàu bay / Dòng", "Số ghế", "Trạng thái"),
                Rows = new[]
                {
                    new AdminCatalogRow("A321", "Airbus A321", "A321", "203 ghế", "Hoạt động", true),
                    new AdminCatalogRow("A320", "Airbus A320", "A320", "180 ghế", "Hoạt động"),
                    new AdminCatalogRow("A350", "Airbus A350-900", "A350", "305 ghế", "Hoạt động"),
                    new AdminCatalogRow("B789", "Boeing 787-9", "B789", "274 ghế", "Hoạt động"),
                    new AdminCatalogRow("AT7", "ATR 72-600", "AT76", "68 ghế", "Hoạt động"),
                    new AdminCatalogRow("E190", "Embraer E190", "E190", "98 ghế", "Bảo trì")
                },
                ResultText = "Hiển thị 1–6 trong 12 tàu bay",
                EditorTitle = "Chỉnh sửa tàu bay",
                EditorSubtitle = "A321 · Dữ liệu đang được sử dụng",
                EditorFields = new[]
                {
                    new AdminEditorField("Mã tàu *", "A321", "Mã tàu không đổi khi đã có chuyến khai thác.", IsRequired: true),
                    new AdminEditorField("Dòng tàu bay *", "Airbus A321", IsRequired: true),
                    new AdminEditorField("Số ghế", "203"),
                    new AdminEditorField("Năm khai thác", "2019"),
                    new AdminEditorField("Trạng thái", "Đang hoạt động", Icon: "chevron-down")
                },
                EditorNote = "Tàu bay đang được xếp cho 4 chuyến mỗi ngày. Bảo trì không tự hủy chuyến.",
                EditorUpdated = "Cập nhật: 20/10/2026 · Admin",
                AddText = "Thêm tàu bay"
            },
            "cabin" => new AdminCatalogViewModel
            {
                Tabs = tabs,
                SectionTitle = "Hạng ghế",
                SectionSubtitle = "3 hạng ghế · 3 đang bán",
                SearchLabel = "Tìm theo tên / mã hạng",
                SearchPlaceholder = "Ví dụ phổ thông hoặc Y",
                Columns = new AdminCatalogColumns("Mã hạng", "Hạng ghế / Nhóm", "Hành lý", "Trạng thái"),
                Rows = new[]
                {
                    new AdminCatalogRow("Y", "Phổ thông Tiết kiệm", "Hạng phổ thông", "23 kg ký gửi", "Đang bán", true),
                    new AdminCatalogRow("M", "Phổ thông Tiêu chuẩn", "Hạng phổ thông", "23 kg ký gửi", "Đang bán"),
                    new AdminCatalogRow("C", "Thương gia", "Hạng thương gia", "32 kg ký gửi", "Đang bán")
                },
                ResultText = "Hiển thị 1–3 trong 3 hạng ghế",
                EditorTitle = "Chỉnh sửa hạng ghế",
                EditorSubtitle = "M · Phổ thông Tiêu chuẩn",
                EditorFields = new[]
                {
                    new AdminEditorField("Mã hạng *", "M", "Mã hạng không đổi khi đã có vé phát hành.", IsRequired: true),
                    new AdminEditorField("Tên hạng ghế *", "Phổ thông Tiêu chuẩn", IsRequired: true),
                    new AdminEditorField("Hành lý ký gửi", "23 kg"),
                    new AdminEditorField("Điều kiện đổi / hoàn", "Đổi 360.000 ₫ · Hoàn 500.000 ₫", Icon: "chevron-down"),
                    new AdminEditorField("Trạng thái", "Đang bán", Icon: "chevron-down")
                },
                EditorNote = "Điều kiện chi tiết của từng hãng bay được ưu tiên áp dụng khi xuất vé.",
                EditorUpdated = "Cập nhật: 20/10/2026 · Admin",
                AddText = "Thêm hạng ghế"
            },
            _ => BuildAirportsCatalog(tabs)
        };

        return model;
    }

    private static AdminCatalogViewModel BuildAirportsCatalog(IReadOnlyList<AdminCatalogTab> tabs) => new()
    {
        Tabs = tabs,
        NoteTitle = "Dữ liệu nền, không phải lịch khai thác",
        NoteText = "Danh mục xác định sân bay và tuyến hợp lệ. Ngày bay, giờ bay, tồn chỗ và giá từng chuyến " +
                   "được quản lý tại khu vực Manager.",
        SectionTitle = "Sân bay nội địa",
        SectionSubtitle = "22 sân bay · 21 đang hoạt động",
        SearchLabel = "Tìm theo tên / IATA",
        SearchPlaceholder = "Tên sân bay hoặc mã",
        StatusLabel = "Trạng thái",
        StatusFilter = "Tất cả",
        ApplyText = "Áp dụng",
        Columns = new AdminCatalogColumns("IATA", "Sân bay / Thành phố", "Múi giờ", "Trạng thái"),
        Rows = new[]
        {
            new AdminCatalogRow("SGN", "Tân Sơn Nhất", "TP. Hồ Chí Minh", "UTC+7", "Hoạt động", true),
            new AdminCatalogRow("HAN", "Nội Bài", "Hà Nội", "UTC+7", "Hoạt động"),
            new AdminCatalogRow("DAD", "Đà Nẵng", "Đà Nẵng", "UTC+7", "Hoạt động"),
            new AdminCatalogRow("PQC", "Phú Quốc", "Phú Quốc", "UTC+7", "Hoạt động"),
            new AdminCatalogRow("CXR", "Cam Ranh", "Khánh Hòa", "UTC+7", "Hoạt động"),
            new AdminCatalogRow("HUI", "Phú Bài", "Huế", "UTC+7", "Hoạt động")
        },
        ResultText = "Hiển thị 1–6 trong 22 sân bay",
        EditorTitle = "Chỉnh sửa sân bay",
        EditorSubtitle = "SGN · Dữ liệu đang được sử dụng",
        EditorFields = new[]
        {
            new AdminEditorField("Mã IATA *", "SGN", "Mã không đổi khi đã có chuyến khai thác.", IsRequired: true),
            new AdminEditorField("Tên sân bay *", "Tân Sơn Nhất", IsRequired: true),
            new AdminEditorField("Thành phố *", "TP. Hồ Chí Minh", IsRequired: true),
            new AdminEditorField("Quốc gia", "Việt Nam"),
            new AdminEditorField("Múi giờ", "UTC+7"),
            new AdminEditorField("Trạng thái", "Đang hoạt động", Icon: "chevron-down")
        },
        EditorNote = "48 tuyến bay đang tham chiếu SGN. Không xóa dữ liệu đã phát sinh đặt chỗ; " +
                     "có thể ngừng sử dụng cho chuyến mới.",
        EditorUpdated = "Cập nhật: 20/10/2026 · Admin",
        AddText = "Thêm sân bay"
    };

    // ---------------------------------------------------------- 12 · cấu hình

    public static AdminSettingsViewModel AdminSettings() => new()
    {
        BookingFields = new[]
        {
            new AdminSettingField("Thời gian giữ chỗ", "15 phút", "Hết hạn tự hủy đơn chưa thanh toán."),
            new AdminSettingField("Phí dịch vụ / khách / chặng", "70.000 VND"),
            new AdminSettingField(
                "Thuế & phí sân bay",
                "Theo biểu phí hãng / sân bay",
                "Hiển thị tách riêng trong tổng tiền. Không cộng thuế hai lần.",
                "chevron-down")
        },
        PolicyFields = new[]
        {
            new AdminSettingField("Phí đổi / khách", "360.000 VND + chênh lệch"),
            new AdminSettingField("Phí hoàn / khách", "500.000 VND"),
            new AdminSettingField("Thời hạn gửi yêu cầu", "Trước giờ bay ít nhất 3 tiếng"),
            new AdminSettingField("Thời gian hoàn tiền dự kiến", "7–15 ngày làm việc")
        },
        PolicyChecks = new[]
        {
            "Không hoàn phí dịch vụ và hành lý mua thêm",
            "Không đổi/hoàn khi khách không có mặt",
            "Chỉ hoàn về phương thức thanh toán ban đầu"
        },
        NotificationFields = new[]
        {
            new AdminSettingField("Email vé điện tử", "Đang bật"),
            new AdminSettingField("Thông báo đổi lịch", "Bắt buộc"),
            new AdminSettingField("Nhắc thanh toán", "Trước hết hạn 5 phút")
        }
    };

    // --------------------------------------------------------- 13 · nhật ký

    public static AdminAuditViewModel AdminAudit() => new()
    {
        Rows = new[]
        {
            new AdminAuditRow("01/11/2026 / 09:50:12", $"{AdminName} / Admin",
                "Cập nhật phí dịch vụ", "CFG-102", "Thành công", "success"),
            new AdminAuditRow("01/11/2026 / 09:43:06", "Lê Quốc Long / Manager",
                "Xuất vé điện tử", Pnr, "Thành công", "success"),
            new AdminAuditRow("01/11/2026 / 09:42:18", "Cổng VNPay / Hệ thống",
                "Xác nhận thanh toán", TransactionCode, "Thành công", "success"),
            new AdminAuditRow("01/11/2026 / 09:30:02", "Nguyễn Minh Anh / Khách hàng",
                "Tạo đặt chỗ", Pnr, "Thành công", "success"),
            new AdminAuditRow("01/11/2026 / 09:15:42", $"{AdminName} / Admin",
                "Tạo tài khoản nhân sự", "NV-018", "Thành công", "success"),
            new AdminAuditRow("01/11/2026 / 08:57:20", "Đỗ Đức Nam / Khách hàng",
                "Đăng nhập sai 5 lần", "TK-0426", "Đã chặn", "danger")
        },
        Detail = new AdminAuditDetailViewModel
        {
            Left = new[]
            {
                new AdminDetailField("Người thực hiện", AdminName),
                new AdminDetailField("Vai trò", "Admin"),
                new AdminDetailField("Địa chỉ IP nội bộ", "10.10.1.24")
            },
            Right = new[]
            {
                new AdminDetailField("Đối tượng", "CFG-102 · Phí dịch vụ"),
                new AdminDetailField("Giá trị trước", "70.000 ₫ / khách / chặng"),
                new AdminDetailField("Giá trị sau", "70.000 ₫ / khách / chặng")
            }
        }
    };
}
