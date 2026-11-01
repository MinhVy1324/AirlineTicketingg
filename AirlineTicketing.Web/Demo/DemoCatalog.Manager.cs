using AirlineTicketing.Web.ViewModels;

namespace AirlineTicketing.Web.Demo;

public static partial class DemoCatalog
{
    public const string ManagerName = "Lê Quốc Long";
    public const string ManagerInitials = "QL";
    public const string ManagerRole = "Quản lý vận hành";

    // --------------------------------------------------------- 14 · tổng quan

    public static ManagerOverviewViewModel ManagerOverview() => new()
    {
        Kpis = new[]
        {
            new ManagerKpi("Chuyến bay hôm nay", "24", "8 hoàn tất · 16 chưa khởi hành", "info", "plane"),
            new ManagerKpi("Đặt chỗ chờ thanh toán", "32", "5 đơn hết hạn trong 10 phút", "warning", "clock"),
            new ManagerKpi("Tỷ lệ lấp đầy hôm nay", "84,6%", "3.655 / 4.320 ghế đã bán", "success", "users"),
            new ManagerKpi("Yêu cầu cần xử lý", "12", "5 đổi vé · 7 hoàn vé", "warning", "inbox")
        },
        Flights = new[]
        {
            new ManagerFlightRow("VN216 · SGN → HAN", "12:30–14:40", "28 / 180", "Đổi giờ +30 phút", "warning"),
            new ManagerFlightRow("VJ640 · SGN → DAD", "13:10–14:30", "4 / 180", "Gần hết chỗ", "warning"),
            new ManagerFlightRow("VN1830 · SGN → PQC", "15:00–16:00", "42 / 180", "Đúng lịch", "success")
        },
        Workload = new[]
        {
            new ManagerWorkloadRow("Đổi vé chờ duyệt", "5 yêu cầu", 42, "warning"),
            new ManagerWorkloadRow("Hoàn vé chờ duyệt", "7 yêu cầu", 58, "success")
        },
        Holds = new[]
        {
            new ManagerHoldRow("AV9P4D", "Nguyễn Minh Anh", "1.290.000 ₫", "10:00 hôm nay"),
            new ManagerHoldRow("AV7T2Q", "Trần Bảo Ngọc", "2.450.000 ₫", "10:05 hôm nay"),
            new ManagerHoldRow("AV6H3L", "Phạm Hải Đăng", "3.900.000 ₫", "10:08 hôm nay")
        },
        Checklist = new[]
        {
            new ManagerChecklistItem("Đối soát thanh toán ca trước", true),
            new ManagerChecklistItem("Kiểm tra lịch khai thác hôm nay", true),
            new ManagerChecklistItem("Xác nhận thông báo VN216", false),
            new ManagerChecklistItem("Xử lý 12 yêu cầu đổi / hoàn", false)
        }
    };

    // -------------------------------------------------------- 15 · lịch bay

    public static ManagerSchedulesViewModel ManagerSchedules(
        string? dialog = null) => new()
    {
        Rows = new[]
        {
            new ManagerScheduleRow(
                "VN210 · A321", "SGN → HAN", "08:00–10:10", "Mở bán", "success",
                "180 / 147 / 0 / 33", "1.500.000 ₫", true),
            new ManagerScheduleRow(
                "VN216 · A321", "SGN → HAN", "12:00–14:10", "Mở bán", "success",
                "180 / 132 / 8 / 40", "1.700.000 ₫"),
            new ManagerScheduleRow(
                "VN220 · A321", "SGN → HAN", "16:00–18:10", "Mở bán", "success",
                "180 / 156 / 4 / 20", "2.000.000 ₫"),
            new ManagerScheduleRow(
                "VJ120 · A320", "SGN → HAN", "07:15–09:25", "Mở bán", "success",
                "180 / 162 / 6 / 12", "1.350.000 ₫")
        },
        Fares = new[]
        {
            new ManagerFareRow("Phổ thông", "168", "139", "1.500.000 ₫"),
            new ManagerFareRow("Thương gia", "12", "8", "4.500.000 ₫")
        },
        ShowCancelDialog = string.Equals(dialog, "huy-chuyen", StringComparison.OrdinalIgnoreCase),
        ShowPriceDialog = string.Equals(dialog, "doi-gia", StringComparison.OrdinalIgnoreCase)
    };

    // ---------------------------------------------------------- 16 · đặt vé

    public static ManagerBookingsViewModel ManagerBookings() => new()
    {
        Rows = new[]
        {
            new ManagerBookingRow(
                "AV8K2M · Nguyễn Minh Anh", "Nguyễn Minh Anh", "VN210 · 20/11/2026",
                "Đã xác nhận", "success", "Đã thanh toán", "success", "Đã xuất vé", "success",
                "2.200.000 ₫", true),
            new ManagerBookingRow(
                "AV9P4D · Nguyễn Minh Anh", "Nguyễn Minh Anh", "VJ620 · 05/12/2026",
                "Đến 10:00", "neutral", "Chưa thanh toán", "warning", "Chưa xuất", "warning",
                "1.290.000 ₫"),
            new ManagerBookingRow(
                "AV7T2Q · Trần Bảo Ngọc", "Trần Bảo Ngọc", "VN220 · 20/11/2026",
                "Đến 10:05", "neutral", "Chưa thanh toán", "warning", "Chưa xuất", "warning",
                "2.450.000 ₫"),
            new ManagerBookingRow(
                "AV5N8B · Đặng Thu Hà", "Đặng Thu Hà", "VN216 · 20/11/2026",
                "Đã xác nhận", "success", "Đã thanh toán", "success", "Chờ xuất", "warning",
                "2.150.000 ₫")
        },
        Statuses = new[]
        {
            new ManagerStatusBadge("Đã thanh toán", "success"),
            new ManagerStatusBadge("Đã xuất vé", "info")
        },
        Fields = new[]
        {
            new AdminDetailField("Hành khách", "NGUYEN MINH ANH · Người lớn"),
            new AdminDetailField("Hành trình", "SGN → HAN · VN210"),
            new AdminDetailField("Khởi hành", "20/11/2026 · 08:00 (UTC+7)"),
            new AdminDetailField("Hạng / Ghế / Hành lý", "Phổ thông · 18A · Ký gửi 33 kg"),
            new AdminDetailField("Số vé điện tử", "738 2400012865")
        },
        Timeline = new[]
        {
            new ManagerTimelineRow("09:30", "Tạo đặt chỗ", "Giữ chỗ đến 09:45"),
            new ManagerTimelineRow("09:42", "Thanh toán thành công", "Visa •••• 4242 · 2.200.000 ₫"),
            new ManagerTimelineRow("09:43", "Xuất vé thành công", "Lê Quốc Long · 738 2400012865")
        },
        Conditions = new[]
        {
            new ManagerChecklistItem("Thanh toán đã xác nhận", true),
            new ManagerChecklistItem("Số tiền khớp với đơn", true),
            new ManagerChecklistItem("Giữ chỗ hợp lệ và đủ thông tin", true)
        }
    };

    // ----------------------------------------------------- 17 · đổi / hoàn

    public static ManagerChangeRefundsViewModel ManagerChangeRefunds(
        string? tab = null,
        string? dialog = null)
    {
        var activeTab = string.IsNullOrWhiteSpace(tab) ? "all" : tab.Trim().ToLowerInvariant();

        var rows = new[]
        {
            new ManagerRequestRow(
                "RF-1027 · AV4R6C", "Trần Mai Linh", "Hoàn vé", "01/11/2026 · 09:10",
                "Chờ duyệt", "warning", true),
            new ManagerRequestRow(
                "CH-1026 · AV3L8N", "Hoàng Tuấn Kiệt", "Đổi vé", "01/11/2026 · 08:55",
                "Chờ duyệt", "warning"),
            new ManagerRequestRow(
                "RF-1025 · AV2B7H", "Phạm Hoài An", "Hoàn vé", "01/11/2026 · 08:40",
                "Chờ duyệt", "warning"),
            new ManagerRequestRow(
                "RF-1024 · AV1T9D", "Vũ Thanh Tâm", "Hoàn vé", "01/11/2026 · 08:15",
                "Đã duyệt · Chờ hoàn tiền", "info")
        };

        var filteredRows = activeTab switch
        {
            "cho-duyet" => rows.Where(row => row.StatusText == "Chờ duyệt").ToArray(),
            "da-duyet" => rows.Where(row =>
                row.StatusText.StartsWith("Đã duyệt", StringComparison.Ordinal)).ToArray(),
            "hoan-tat" => Array.Empty<ManagerRequestRow>(),
            _ => rows
        };

        var resultText = activeTab switch
        {
            "cho-duyet" => "Hiển thị 1–3 trong 12 yêu cầu chờ duyệt",
            "da-duyet" => "Hiển thị 1 trong 4 yêu cầu đã duyệt",
            "hoan-tat" => "Chưa có yêu cầu hoàn tất trong ngày 01/11/2026",
            _ => "Hiển thị 1–4 trong 18 yêu cầu"
        };

        return new ManagerChangeRefundsViewModel
        {
            Tabs = new[]
            {
                new ManagerQueueTab("Tất cả (18)", "/quan-ly/doi-hoan", activeTab == "all"),
                new ManagerQueueTab("Chờ duyệt (12)", "/quan-ly/doi-hoan?tab=cho-duyet", activeTab == "cho-duyet"),
                new ManagerQueueTab("Đã duyệt (4)", "/quan-ly/doi-hoan?tab=da-duyet", activeTab == "da-duyet"),
                new ManagerQueueTab("Hoàn tất (2)", "/quan-ly/doi-hoan?tab=hoan-tat", activeTab == "hoan-tat")
            },
            Rows = filteredRows,
            ResultText = resultText,
            DetailFields = new[]
            {
                new AdminDetailField("Chuyến bay gốc", "VN216 · SGN → HAN"),
                new AdminDetailField("Khởi hành", "22/11/2026 · 12:00 (UTC+7)"),
                new AdminDetailField("Loại vé", "Phổ thông Tiêu chuẩn"),
                new AdminDetailField("Lý do của khách", "Thay đổi kế hoạch cá nhân")
            },
            RefundRows = new[]
            {
                new ManagerRefundRow("Tổng đã thanh toán", "2.200.000 ₫"),
                new ManagerRefundRow("Phí hoàn vé", "−500.000 ₫"),
                new ManagerRefundRow("Phí dịch vụ không hoàn", "−70.000 ₫"),
                new ManagerRefundRow("Hành lý thêm không hoàn", "−250.000 ₫"),
                new ManagerRefundRow("Dự kiến hoàn", "1.380.000 ₫", true)
            },
            ShowApproveDialog = string.Equals(dialog, "duyet-hoan", StringComparison.OrdinalIgnoreCase)
        };
    }

    // -------------------------------------------------------- 18 · báo cáo

    public static ManagerReportsViewModel ManagerReports() => new()
    {
        Kpis = new[]
        {
            new ManagerKpi("Doanh thu gộp", "18,6 tỷ ₫", "↑ 8,1% so với tháng 9/2026", "success", "chart"),
            new ManagerKpi("Vé đã xuất", "9.600", "↑ 6,7% · Tháng 9: 9.000 vé", "success", "ticket"),
            new ManagerKpi("Tỷ lệ hủy đặt chỗ", "2,4%", "240 / 10.000 đặt chỗ", "warning", "x"),
            new ManagerKpi("Tỷ lệ vé hoàn", "1,8%", "173 / 9.600 vé đã xuất", "warning", "undo")
        },
        Bars = new[]
        {
            new ManagerRevenueBar("T05", "13,2", 13.2m, false),
            new ManagerRevenueBar("T06", "14,1", 14.1m, false),
            new ManagerRevenueBar("T07", "15,6", 15.6m, false),
            new ManagerRevenueBar("T08", "16,4", 16.4m, false),
            new ManagerRevenueBar("T09", "17,2", 17.2m, false),
            new ManagerRevenueBar("T10", "18,6", 18.6m, true)
        },
        Airlines = new[]
        {
            new ManagerAirlineRevenue("Vietnam Airlines", "10,2 tỷ ₫", "54,8% tổng doanh thu", 55, "primary"),
            new ManagerAirlineRevenue("Vietjet Air", "5,6 tỷ ₫", "30,1% tổng doanh thu", 30, "success"),
            new ManagerAirlineRevenue("Bamboo Airways", "2,8 tỷ ₫", "15,1% tổng doanh thu", 15, "muted")
        },
        Routes = new[]
        {
            new ManagerRouteRevenue(
                "SGN ↔ HAN", "3.100", "6.200.000.000 ₫", "80.000.000 ₫", "6.120.000.000 ₫"),
            new ManagerRouteRevenue(
                "SGN ↔ DAD", "2.300", "4.400.000.000 ₫", "50.000.000 ₫", "4.350.000.000 ₫"),
            new ManagerRouteRevenue(
                "HAN ↔ DAD", "1.600", "3.100.000.000 ₫", "40.000.000 ₫", "3.060.000.000 ₫"),
            new ManagerRouteRevenue(
                "SGN ↔ PQC", "1.500", "2.800.000.000 ₫", "30.000.000 ₫", "2.770.000.000 ₫"),
            new ManagerRouteRevenue(
                "Các tuyến khác", "1.100", "2.100.000.000 ₫", "20.000.000 ₫", "2.080.000.000 ₫"),
            new ManagerRouteRevenue(
                "Tổng cộng", "9.600", "18.600.000.000 ₫", "220.000.000 ₫", "18.380.000.000 ₫", true)
        }
    };
}
