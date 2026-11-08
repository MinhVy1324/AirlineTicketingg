using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Controllers;

public class AdminController(AppDbContext dbContext) : Controller
{
    private const string ActiveText = "Hoạt động";
    private const string InactiveText = "Tạm dừng";
    private const string RouteActiveText = "Đang khai thác";
    private const string RouteInactiveText = "Tạm dừng";

    [HttpGet("/admin")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var paidRevenue = await dbContext.ThanhToans.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiThanhToan.ThanhCong)
            .SumAsync(x => (decimal?)x.SoTien, cancellationToken) ?? 0m;
        var tickets = await dbContext.Ves.AsNoTracking().CountAsync(cancellationToken);
        var customers = await dbContext.KhachHangs.AsNoTracking().CountAsync(cancellationToken);
        var flights = await dbContext.ChuyenBays.AsNoTracking().CountAsync(cancellationToken);

        return View(new AdminOverviewViewModel
        {
            StatusText = "Dữ liệu hệ thống",
            StatusMeta = $"Cập nhật {DateTimeOffset.UtcNow.AddHours(7):HH:mm}, {DateTimeOffset.UtcNow.AddHours(7):dd/MM/yyyy} (UTC+7)",
            PeriodText = "Theo toàn bộ dữ liệu hiện có",
            Kpis = new[]
            {
                new AdminKpi("Doanh thu đã thanh toán", Money.Vnd(paidRevenue), "Giao dịch thanh toán thành công", "success", "chart"),
                new AdminKpi("Vé trong hệ thống", tickets.ToString(), $"{flights} chuyến bay đã cấu hình", "info", "ticket"),
                new AdminKpi("Khách hàng", customers.ToString(), "Theo bảng khách hàng", "info", "users"),
                new AdminKpi("Cổng dữ liệu", "Kết nối", "Đọc từ PostgreSQL qua EF Core", "success", "activity")
            },
            Revenue = Array.Empty<AdminRevenuePoint>(),
            Health = Array.Empty<AdminHealthRow>(),
            Activity = Array.Empty<AdminActivityRow>()
        });
    }

    [HttpGet("/admin/tai-khoan")]
    public IActionResult Accounts() => View(NotConfiguredAccounts());

    [HttpGet("/admin/tai-khoan/khoa")]
    public IActionResult LockAccount() => View("Accounts", NotConfiguredAccounts());

    [HttpPost("/admin/tai-khoan/khoa")]
    [ValidateAntiForgeryToken]
    public IActionResult LockAccount(IFormCollection form) => RedirectToAction(nameof(Accounts));

    [HttpGet("/admin/vai-tro")]
    public IActionResult Roles() => View(NotConfiguredRoles());

    [HttpPost("/admin/vai-tro")]
    [ValidateAntiForgeryToken]
    public IActionResult Roles(IFormCollection form) => RedirectToAction(nameof(Roles));

    [HttpGet("/admin/danh-muc")]
    public async Task<IActionResult> Catalog(
        string? tab,
        string? q,
        [FromQuery(Name = "trang-thai")] string? trangThai,
        int? id,
        CancellationToken cancellationToken)
    {
        var key = NormalizeTab(tab);
        var danhMuc = FlightServices.DanhMuc(dbContext);

        var airports = await danhMuc.LaySanBayAsync(cancellationToken: cancellationToken);
        var aircraft = await danhMuc.LayMayBayAsync(cancellationToken: cancellationToken);
        var routes = await danhMuc.LayTuyenBayAsync(cancellationToken: cancellationToken);

        var tabs = new[]
        {
            new AdminCatalogTab("airports", "Sân bay", airports.Count.ToString(),
                key == "airports", Href("airports")),
            new AdminCatalogTab("routes", "Tuyến bay", routes.Count.ToString(),
                key == "routes", Href("routes")),
            new AdminCatalogTab("aircraft", "Tàu bay", aircraft.Count.ToString(),
                key == "aircraft", Href("aircraft"))
        };

        var model = key switch
        {
            "routes" => BuildRouteCatalog(tabs, routes, q, trangThai, id),
            "aircraft" => BuildAircraftCatalog(tabs, aircraft, q, trangThai, id),
            _ => BuildAirportCatalog(tabs, airports, q, trangThai, id)
        };

        model.Message = TempData["CatalogMessage"] as string;
        model.MessageTone = TempData["CatalogTone"] as string ?? "info";
        return View(model);
    }

    [HttpPost("/admin/danh-muc")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Catalog(IFormCollection form, CancellationToken cancellationToken)
    {
        var key = NormalizeTab(form["tab"]);
        var id = int.TryParse(form["id"], out var parsedId) ? parsedId : 0;
        var danhMuc = FlightServices.DanhMuc(dbContext);

        KetQuaXuLy result;

        switch (key)
        {
            case "routes":
            {
                var sanBayDi = await danhMuc.TimSanBayTheoTuKhoaAsync(form["san-bay-di"], cancellationToken);
                var sanBayDen = await danhMuc.TimSanBayTheoTuKhoaAsync(form["san-bay-den"], cancellationToken);

                if (sanBayDi is null || sanBayDen is null)
                {
                    result = KetQuaXuLy.ThatBai("Sân bay đi/đến không hợp lệ. Nhập mã IATA, ví dụ SGN.");
                    break;
                }

                result = await danhMuc.LuuTuyenBayAsync(new TuyenBayDTO
                {
                    Id = id,
                    SanBayDiId = sanBayDi.Id,
                    SanBayDenId = sanBayDen.Id,
                    ThoiLuongDuKienPhut = ParseNullableInt(form["thoi-luong"]),
                    DangKhaiThac = !IsInactive(form["trang-thai"], RouteInactiveText)
                }, cancellationToken);
                break;
            }

            case "aircraft":
            {
                result = await danhMuc.LuuMayBayAsync(new MayBayDTO
                {
                    Id = id,
                    MaMayBay = form["ma"].ToString(),
                    TenMayBay = form["ten"].ToString(),
                    HangSanXuat = form["hang-san-xuat"].ToString(),
                    SoGhe = ParseInt(form["so-ghe"]),
                    DangHoatDong = !IsInactive(form["trang-thai"], InactiveText)
                }, cancellationToken);
                break;
            }

            default:
            {
                result = await danhMuc.LuuSanBayAsync(new SanBayDTO
                {
                    Id = id,
                    MaSanBay = form["ma"].ToString(),
                    TenSanBay = form["ten"].ToString(),
                    ThanhPho = form["thanh-pho"].ToString(),
                    QuocGia = form["quoc-gia"].ToString(),
                    DangHoatDong = !IsInactive(form["trang-thai"], InactiveText)
                }, cancellationToken);
                break;
            }
        }

        TempData["CatalogMessage"] = result.ThanhCong
            ? "Đã lưu dữ liệu danh mục."
            : result.Loi;
        TempData["CatalogTone"] = result.ThanhCong ? "success" : "danger";

        return RedirectToAction(nameof(Catalog), new { tab = TabQuery(key) });
    }

    [HttpGet("/admin/cau-hinh")]
    public IActionResult Settings() => View(NotConfiguredSettings());

    [HttpPost("/admin/cau-hinh")]
    [ValidateAntiForgeryToken]
    public IActionResult Settings(IFormCollection form) => RedirectToAction(nameof(Settings));

    [HttpGet("/admin/nhat-ky")]
    public IActionResult Audit() => View(NotConfiguredAudit());

    [HttpPost("/admin/nhat-ky")]
    [ValidateAntiForgeryToken]
    public IActionResult Audit(IFormCollection form) => RedirectToAction(nameof(Audit));

    private static AdminCatalogViewModel BuildAirportCatalog(
        IReadOnlyList<AdminCatalogTab> tabs,
        IReadOnlyList<SanBayDTO> airports,
        string? keyword,
        string? status,
        int? selectedId)
    {
        var filtered = airports.Where(item =>
            Matches(keyword, item.MaSanBay, item.TenSanBay, item.ThanhPho, item.QuocGia)
            && MatchesStatus(status, item.DangHoatDong)).ToArray();

        var editor = selectedId switch
        {
            0 => null,
            > 0 => airports.FirstOrDefault(item => item.Id == selectedId) ?? filtered.FirstOrDefault() ?? airports.FirstOrDefault(),
            _ => filtered.FirstOrDefault() ?? airports.FirstOrDefault()
        };

        var rows = filtered.Select(item => new AdminCatalogRow(
            item.MaSanBay,
            item.TenSanBay,
            item.ThanhPho,
            item.QuocGia,
            item.DangHoatDong ? ActiveText : InactiveText,
            item.Id == editor?.Id,
            item.Id,
            Href("airports", item.Id))).ToArray();

        var active = airports.Count(item => item.DangHoatDong);

        return new AdminCatalogViewModel
        {
            Tabs = tabs,
            SectionTitle = "Sân bay",
            SectionSubtitle = $"{airports.Count} sân bay · {active} đang hoạt động",
            SearchLabel = "Tìm theo tên / IATA",
            SearchPlaceholder = "Tên sân bay hoặc mã",
            StatusLabel = "Trạng thái",
            StatusFilter = string.IsNullOrWhiteSpace(status) ? "Tất cả" : status,
            Columns = new AdminCatalogColumns("IATA", "Sân bay / Thành phố", "Quốc gia", "Trạng thái"),
            Rows = rows,
            ResultText = ResultText(rows.Length, airports.Count, "sân bay"),
            EditorTitle = editor is null ? "Thêm sân bay" : "Chỉnh sửa sân bay",
            EditorSubtitle = editor is null ? "Tạo dữ liệu nền mới" : $"{editor.MaSanBay} · Dữ liệu đang được sử dụng",
            EditorFields = new[]
            {
                new AdminEditorField("Mã IATA *", editor?.MaSanBay ?? string.Empty,
                    "Mã gồm đúng 3 chữ cái, ví dụ SGN.", IsRequired: true, Name: "ma"),
                new AdminEditorField("Tên sân bay *", editor?.TenSanBay ?? string.Empty,
                    IsRequired: true, Name: "ten"),
                new AdminEditorField("Thành phố *", editor?.ThanhPho ?? string.Empty,
                    IsRequired: true, Name: "thanh-pho"),
                new AdminEditorField("Quốc gia", editor?.QuocGia ?? "Việt Nam", Name: "quoc-gia"),
                new AdminEditorField("Trạng thái", StatusText(editor?.DangHoatDong ?? true, ActiveText, InactiveText),
                    Name: "trang-thai", Icon: "chevron-down",
                    Options: new[] { ActiveText, InactiveText })
            },
            EditorNote = "Không xóa dữ liệu đã phát sinh đặt chỗ; có thể ngừng sử dụng cho chuyến mới.",
            EditorId = editor?.Id ?? 0,
            EditorTabKey = "airports",
            AddText = "Thêm sân bay"
        };
    }

    private static AdminCatalogViewModel BuildRouteCatalog(
        IReadOnlyList<AdminCatalogTab> tabs,
        IReadOnlyList<TuyenBayDTO> routes,
        string? keyword,
        string? status,
        int? selectedId)
    {
        var filtered = routes.Where(item =>
            Matches(keyword, item.SanBayDi.MaSanBay, item.SanBayDen.MaSanBay,
                item.SanBayDi.ThanhPho, item.SanBayDen.ThanhPho)
            && MatchesStatus(status, item.DangKhaiThac)).ToArray();

        var editor = selectedId switch
        {
            0 => null,
            > 0 => routes.FirstOrDefault(item => item.Id == selectedId) ?? filtered.FirstOrDefault() ?? routes.FirstOrDefault(),
            _ => filtered.FirstOrDefault() ?? routes.FirstOrDefault()
        };

        var rows = filtered.Select(item => new AdminCatalogRow(
            $"{item.SanBayDi.MaSanBay}-{item.SanBayDen.MaSanBay}",
            $"{item.SanBayDi.MaSanBay} → {item.SanBayDen.MaSanBay}",
            $"{item.SanBayDi.ThanhPho} → {item.SanBayDen.ThanhPho}",
            item.ThoiLuongDuKienPhut is { } minutes ? $"{minutes} phút" : "Chưa đặt thời lượng",
            item.DangKhaiThac ? RouteActiveText : RouteInactiveText,
            item.Id == editor?.Id,
            item.Id,
            Href("routes", item.Id))).ToArray();

        var active = routes.Count(item => item.DangKhaiThac);

        return new AdminCatalogViewModel
        {
            Tabs = tabs,
            SectionTitle = "Tuyến bay",
            SectionSubtitle = $"{routes.Count} tuyến bay · {active} đang khai thác",
            SearchLabel = "Tìm theo sân bay / mã tuyến",
            SearchPlaceholder = "Ví dụ SGN hoặc SGN-HAN",
            StatusLabel = "Trạng thái",
            StatusFilter = string.IsNullOrWhiteSpace(status) ? "Tất cả" : status,
            Columns = new AdminCatalogColumns("Mã tuyến", "Tuyến bay / Thành phố", "Thời lượng", "Trạng thái"),
            Rows = rows,
            ResultText = ResultText(rows.Length, routes.Count, "tuyến bay"),
            EditorTitle = editor is null ? "Thêm tuyến bay" : "Chỉnh sửa tuyến bay",
            EditorSubtitle = editor is null
                ? "Tạo dữ liệu nền mới"
                : $"{editor.SanBayDi.MaSanBay}-{editor.SanBayDen.MaSanBay} · Dữ liệu đang được sử dụng",
            EditorFields = new[]
            {
                new AdminEditorField("Sân bay đi *", editor?.SanBayDi.MaSanBay ?? string.Empty,
                    "Nhập mã IATA, ví dụ SGN.", IsRequired: true, Name: "san-bay-di"),
                new AdminEditorField("Sân bay đến *", editor?.SanBayDen.MaSanBay ?? string.Empty,
                    "Nhập mã IATA, ví dụ HAN.", IsRequired: true, Name: "san-bay-den"),
                new AdminEditorField("Thời lượng dự kiến (phút)",
                    editor?.ThoiLuongDuKienPhut?.ToString() ?? "130", Name: "thoi-luong"),
                new AdminEditorField("Trạng thái",
                    StatusText(editor?.DangKhaiThac ?? true, RouteActiveText, RouteInactiveText),
                    Name: "trang-thai", Icon: "chevron-down",
                    Options: new[] { RouteActiveText, RouteInactiveText })
            },
            EditorNote = "Lịch bay chi tiết được quản lý tại khu vực Manager. Ngừng tuyến không ảnh hưởng vé đã xuất.",
            EditorId = editor?.Id ?? 0,
            EditorTabKey = "routes",
            AddText = "Thêm tuyến bay"
        };
    }

    private static AdminCatalogViewModel BuildAircraftCatalog(
        IReadOnlyList<AdminCatalogTab> tabs,
        IReadOnlyList<MayBayDTO> aircraft,
        string? keyword,
        string? status,
        int? selectedId)
    {
        var filtered = aircraft.Where(item =>
            Matches(keyword, item.MaMayBay, item.TenMayBay, item.HangSanXuat)
            && MatchesStatus(status, item.DangHoatDong)).ToArray();

        var editor = selectedId switch
        {
            0 => null,
            > 0 => aircraft.FirstOrDefault(item => item.Id == selectedId) ?? filtered.FirstOrDefault() ?? aircraft.FirstOrDefault(),
            _ => filtered.FirstOrDefault() ?? aircraft.FirstOrDefault()
        };

        var rows = filtered.Select(item => new AdminCatalogRow(
            item.MaMayBay,
            item.TenMayBay,
            item.HangSanXuat,
            $"{item.SoGhe} ghế",
            item.DangHoatDong ? ActiveText : InactiveText,
            item.Id == editor?.Id,
            item.Id,
            Href("aircraft", item.Id))).ToArray();

        var active = aircraft.Count(item => item.DangHoatDong);

        return new AdminCatalogViewModel
        {
            Tabs = tabs,
            SectionTitle = "Đội tàu bay",
            SectionSubtitle = $"{aircraft.Count} tàu bay · {active} đang khai thác",
            SearchLabel = "Tìm theo dòng / mã tàu",
            SearchPlaceholder = "Ví dụ A321 hoặc Airbus",
            StatusLabel = "Trạng thái",
            StatusFilter = string.IsNullOrWhiteSpace(status) ? "Tất cả" : status,
            Columns = new AdminCatalogColumns("Mã tàu", "Tàu bay / Hãng sản xuất", "Số ghế", "Trạng thái"),
            Rows = rows,
            ResultText = ResultText(rows.Length, aircraft.Count, "tàu bay"),
            EditorTitle = editor is null ? "Thêm tàu bay" : "Chỉnh sửa tàu bay",
            EditorSubtitle = editor is null ? "Tạo dữ liệu nền mới" : $"{editor.MaMayBay} · Dữ liệu đang được sử dụng",
            EditorFields = new[]
            {
                new AdminEditorField("Mã tàu *", editor?.MaMayBay ?? string.Empty,
                    "Mã không đổi khi đã có chuyến khai thác.", IsRequired: true, Name: "ma"),
                new AdminEditorField("Dòng tàu bay *", editor?.TenMayBay ?? string.Empty,
                    IsRequired: true, Name: "ten"),
                new AdminEditorField("Hãng sản xuất", editor?.HangSanXuat ?? string.Empty, Name: "hang-san-xuat"),
                new AdminEditorField("Số ghế", editor?.SoGhe.ToString() ?? "180", Name: "so-ghe"),
                new AdminEditorField("Trạng thái", StatusText(editor?.DangHoatDong ?? true, ActiveText, InactiveText),
                    Name: "trang-thai", Icon: "chevron-down",
                    Options: new[] { ActiveText, InactiveText })
            },
            EditorNote = "Không giảm sức chứa xuống thấp hơn số ghế đã bán/đang giữ trên bất kỳ chuyến nào.",
            EditorId = editor?.Id ?? 0,
            EditorTabKey = "aircraft",
            AddText = "Thêm tàu bay"
        };
    }

    private static string NormalizeTab(string? tab) => tab?.Trim().ToLowerInvariant() switch
    {
        "tuyen-bay" or "routes" => "routes",
        "tau-bay" or "aircraft" => "aircraft",
        _ => "airports"
    };

    private static string TabQuery(string key) => key switch
    {
        "routes" => "tuyen-bay",
        "aircraft" => "tau-bay",
        _ => string.Empty
    };

    private static string Href(string key, int? id = null)
    {
        var query = TabQuery(key);
        var url = string.IsNullOrEmpty(query) ? "/admin/danh-muc" : $"/admin/danh-muc?tab={query}";
        return id is null ? url : $"{url}{(url.Contains('?') ? "&" : "?")}id={id}";
    }

    private static bool Matches(string? keyword, params string[] values)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        var needle = keyword.Trim();
        return values.Any(value => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesStatus(string? status, bool active)
    {
        if (string.IsNullOrWhiteSpace(status) || status == "Tất cả")
        {
            return true;
        }

        var wantsActive = status.Contains("Hoạt động", StringComparison.OrdinalIgnoreCase)
            || status.Contains("khai thác", StringComparison.OrdinalIgnoreCase);
        return wantsActive == active;
    }

    private static string StatusText(bool active, string activeText, string inactiveText) =>
        active ? activeText : inactiveText;

    private static bool IsInactive(string? value, string inactiveText) =>
        string.Equals(value, inactiveText, StringComparison.OrdinalIgnoreCase);

    private static int ParseInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : 0;

    private static int? ParseNullableInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;

    private static string ResultText(int shown, int total, string unit) =>
        total == 0
            ? $"Chưa có {unit} nào"
            : shown == 0
                ? $"Không có {unit} khớp bộ lọc trong {total} {unit}"
                : $"Hiển thị {shown} trong {total} {unit}";

    private static AdminAccountsViewModel NotConfiguredAccounts() => new()
    {
        Stats = Array.Empty<AdminKpi>(),
        Accounts = Array.Empty<AdminAccountRow>(),
        Detail = new AdminAccountDetailViewModel
        {
            Title = "Tài khoản & phân quyền",
            Subtitle = "Chưa khả dụng trên nhánh NgDang",
            Fields = new[]
            {
                new AdminDetailField("Trạng thái", "Schema xác thực/tài khoản (GĐ2) chưa được merge vào NgDang."),
                new AdminDetailField("Hành động", "Sử dụng nhánh có GĐ2 (main) hoặc merge schema GĐ2 để quản lý tài khoản.")
            },
            PrimaryActionText = "Quay lại tổng quan",
            DangerActionText = string.Empty,
            DangerActionHref = "/admin"
        },
        ResultText = "0 tài khoản",
        ShowLockDialog = false,
        LockDialog = new AdminLockDialogViewModel()
    };

    private static AdminRolesViewModel NotConfiguredRoles() => new()
    {
        Roles = Array.Empty<AdminRoleCard>(),
        Permissions = Array.Empty<AdminPermissionRow>(),
        PolicyVersion = "NgDang",
        PermissionLegend = "Quản lý vai trò/phân quyền cần schema GĐ2.",
        PermissionFootnote = "Chức năng này chưa khả dụng trên nhánh hiện tại.",
        LimitsTitle = "Chưa khả dụng",
        LimitTitle = "Cần schema phân quyền (GĐ2)",
        LimitText = "Merge/port GĐ2 từ main để bật quản lý vai trò và phân quyền.",
        LimitNote = string.Empty,
        ControlTitle = "Trạng thái",
        ControlStatus = "Chưa cấu hình",
        ControlUpdated = "—",
        ControlActor = "—"
    };

    private static AdminSettingsViewModel NotConfiguredSettings() => new()
    {
        VersionText = "NgDang · chưa có bảng cấu hình GĐ2",
        DirtyText = "Không thể lưu trên nhánh hiện tại",
        UpdatedText = "—",
        BookingTitle = "Đặt chỗ & phí dịch vụ",
        BookingFields = new[] { new AdminSettingField("Trạng thái", "Cần schema CauHinhHeThong (GĐ2).") },
        ExampleTitle = "Ghi chú",
        ExampleText = "Merge/port GĐ2 từ main để bật cấu hình nghiệp vụ.",
        PolicyTitle = "Chính sách đổi / hoàn",
        PolicySubtitle = "Chưa khả dụng trên NgDang",
        PolicyFields = Array.Empty<AdminSettingField>(),
        PolicyChecks = Array.Empty<string>(),
        PaymentTitle = "Tích hợp thanh toán",
        Payment = new AdminPaymentViewModel
        {
            Name = "—",
            StatusText = "Chưa cấu hình",
            StatusTone = "neutral",
            Environment = "—",
            MerchantId = "—",
            SecretMasked = "—",
            Note = "Cần schema/cấu hình GĐ2.",
            CheckText = "—"
        },
        NotificationTitle = "Thông báo",
        NotificationFields = Array.Empty<AdminSettingField>(),
        EmailSender = new AdminSettingField("Email gửi hệ thống", "—"),
        FooterNote = "Chức năng cấu hình cần schema GĐ2.",
        SaveNoteTitle = "Chưa khả dụng",
        SaveNoteText = "Không thay đổi dữ liệu trên nhánh hiện tại."
    };

    private static AdminAuditViewModel NotConfiguredAudit() => new()
    {
        FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToString("dd/MM/yyyy"),
        ToDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToString("dd/MM/yyyy"),
        ActorFilter = "Tất cả",
        TypeFilter = "Tất cả",
        FilterText = "Lọc",
        ExportText = "Xuất",
        Rows = Array.Empty<AdminAuditRow>(),
        ResultText = "0 sự kiện · nhật ký cần schema NhatKyHoatDong (GĐ2)",
        Detail = new AdminAuditDetailViewModel
        {
            Title = "Nhật ký hoạt động",
            Subtitle = "Chưa khả dụng trên nhánh NgDang",
            Left = new[] { new AdminDetailField("Trạng thái", "Cần schema GĐ2.") },
            Right = Array.Empty<AdminDetailField>(),
            Content = "Merge/port GĐ2 từ main để bật nhật ký hoạt động."
        },
        IntegrityTitle = "Tính toàn vẹn nhật ký",
        IntegrityStatus = "Chưa cấu hình",
        IntegrityNoteTitle = "Ghi chú",
        IntegrityNoteText = "Nhật ký hoạt động chưa khả dụng trên nhánh hiện tại.",
        IntegrityCode = "—"
    };
}
