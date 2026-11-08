using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AdminController(AppDbContext db) : Controller
{
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);

    [HttpGet("/admin")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var gross = await db.ThanhToans.Where(t => t.TrangThai == TrangThaiThanhToan.ThanhCong && t.NgayTao >= monthStart).SumAsync(t => (decimal?)t.SoTien, ct) ?? 0m;
        var tickets = await db.Ves.CountAsync(v => v.TrangThai == TrangThaiVe.DaPhatHanh && v.NgayPhatHanh >= monthStart, ct);
        var activeAccounts = await db.TaiKhoans.CountAsync(a => a.TrangThai == TrangThaiTaiKhoan.HoatDong, ct);
        var recent = await db.NhatKyHoatDongs.AsNoTracking().OrderByDescending(n => n.ThoiGian).Take(6).ToListAsync(ct);

        return View(new AdminOverviewViewModel
        {
            StatusMeta = $"Cập nhật {now.ToOffset(Vn):HH:mm}, {now.ToOffset(Vn):dd/MM/yyyy} (dữ liệu thực)",
            PeriodText = $"{monthStart:MM/yyyy}",
            Kpis = new[]
            {
                new AdminKpi("Doanh thu gộp (tháng)", Money.Vnd(gross), "Thanh toán thành công (DB)", "success", "chart"),
                new AdminKpi("Vé đã xuất (tháng)", tickets.ToString("N0"), "Theo NgayPhatHanh", "success", "ticket"),
                new AdminKpi("Tài khoản hoạt động", activeAccounts.ToString("N0"), "Từ bảng TaiKhoans", "info", "users"),
                new AdminKpi("Chuyến bay trong hệ thống", (await db.ChuyenBays.CountAsync(ct)).ToString("N0"), "Tổng bản ghi", "success", "activity")
            },
            Activity = recent.Select(n => new AdminActivityRow(
                n.ThoiGian.ToOffset(Vn).ToString("HH:mm"),
                n.TenNguoiDung, $"{n.HanhDong} · {n.DoiTuong}",
                n.KetQua.ToString(), n.KetQua == KetQuaNhatKy.ThanhCong ? "success" : "danger")).ToList(),
            Attention = new[]
            {
                new AdminAttentionItem("alert", "Dữ liệu đang lấy từ cơ sở dữ liệu", "Các trang admin hiển thị số liệu thực từ PostgreSQL.", "info", "Xem nhật ký", "/admin/nhat-ky")
            },
            Health = new[]
            {
                new AdminHealthRow("Cơ sở dữ liệu", "Đang kết nối"),
                new AdminHealthRow("Tài khoản seed", activeAccounts > 0 ? "Có dữ liệu" : "Chưa có dữ liệu"),
                new AdminHealthRow("Nhật ký", recent.Count > 0 ? "Có bản ghi" : "Chưa có bản ghi")
            },
            Revenue = Array.Empty<AdminRevenuePoint>()
        });
    }

    private async Task<AdminAccountsViewModel> BuildAccountsVmAsync(int? id, CancellationToken ct, bool showLock = false, bool showForm = false, AdminAccountFormViewModel? form = null, string? msg = null, string tone = "info")
    {
        var list = await db.TaiKhoans.AsNoTracking().OrderByDescending(a => a.NgayTao).Take(200).ToListAsync(ct);
        var selected = id.HasValue ? list.FirstOrDefault(a => a.Id == id.Value) ?? list.FirstOrDefault() : list.FirstOrDefault();
        var rows = list.Select(a => new AdminAccountRow(
            a.HoTen, a.Email, a.VaiTro.ToString(),
            a.TrangThai == TrangThaiTaiKhoan.HoatDong ? "Hoạt động" : "Đã khóa",
            a.TrangThai == TrangThaiTaiKhoan.HoatDong ? "success" : "danger",
            a.LanDangNhapCuoi?.ToOffset(Vn).ToString("dd/MM/yyyy HH:mm") ?? "—",
            "Xem", $"/admin/tai-khoan?id={a.Id}", selected != null && a.Id == selected.Id)).ToList();

        var detail = selected == null ? new AdminAccountDetailViewModel() : new AdminAccountDetailViewModel
        {
            Id = selected.Id,
            Title = $"Chi tiết · {selected.HoTen}",
            Subtitle = $"{selected.MaNhanSu ?? selected.TenDangNhap} · {selected.Email}",
            Fields = new[]
            {
                new AdminDetailField("Mã tài khoản", selected.Id.ToString()),
                new AdminDetailField("Vai trò", selected.VaiTro.ToString()),
                new AdminDetailField("Trạng thái", selected.TrangThai == TrangThaiTaiKhoan.HoatDong ? "Hoạt động" : "Đã khóa"),
                new AdminDetailField("Tên đăng nhập", selected.TenDangNhap),
                new AdminDetailField("Đăng nhập cuối", selected.LanDangNhapCuoi?.ToOffset(Vn).ToString("dd/MM/yyyy HH:mm") ?? "—")
            },
            PrimaryActionHref = $"/admin/tai-khoan/sua",
            DangerActionText = selected.TrangThai == TrangThaiTaiKhoan.HoatDong ? "Khóa tài khoản" : "Mở khóa tài khoản",
            DangerActionHref = $"/admin/tai-khoan/khoa?id={selected.Id}"
        };

        var lockDialog = new AdminLockDialogViewModel();
        if (showLock && selected != null)
        {
            lockDialog = new AdminLockDialogViewModel
            {
                AccountName = selected.HoTen,
                Email = selected.Email,
                RoleStatus = $"{selected.VaiTro} · {(selected.TrangThai == TrangThaiTaiKhoan.HoatDong ? "Hoạt động" : "Đã khóa")}",
                Reason = selected.TrangThai == TrangThaiTaiKhoan.HoatDong ? "Tạm ngừng quyền truy cập theo yêu cầu quản trị." : "Mở lại quyền truy cập theo xác nhận.",
                SubmitHref = "/admin/tai-khoan/khoa",
                CancelHref = $"/admin/tai-khoan?id={selected.Id}"
            };
        }

        return new AdminAccountsViewModel
        {
            Stats = new[]
            {
                new AdminKpi("Tất cả tài khoản", list.Count.ToString("N0"), "Theo DB", "info", "users"),
                new AdminKpi("Hoạt động", list.Count(a => a.TrangThai == TrangThaiTaiKhoan.HoatDong).ToString("N0"), "", "success", "check-circle"),
                new AdminKpi("Đã khóa", list.Count(a => a.TrangThai != TrangThaiTaiKhoan.HoatDong).ToString("N0"), "", "warning", "lock"),
                new AdminKpi("Nội bộ", list.Count(a => a.VaiTro != VaiTro.KhachHang).ToString("N0"), "Admin/Manager", "info", "id-card")
            },
            Accounts = rows,
            ResultText = $"Hiển thị {rows.Count} tài khoản (dữ liệu thực)",
            Detail = detail,
            ShowLockDialog = showLock,
            LockDialog = lockDialog,
            ShowFormDialog = showForm,
            Form = form ?? new AdminAccountFormViewModel(),
            Message = msg ?? (TempData["msg"] as string),
            MessageTone = string.IsNullOrWhiteSpace(msg) ? (TempData["tone"] as string ?? tone) : tone
        };
    }

    [HttpGet("/admin/tai-khoan")]
    public async Task<IActionResult> Accounts(int? id, CancellationToken ct)
        => View(await BuildAccountsVmAsync(id, ct));

    [HttpGet("/admin/tai-khoan/khoa")]
    public async Task<IActionResult> LockAccount(int? id, CancellationToken ct)
    {
        if (!id.HasValue) return RedirectToAction(nameof(Accounts));
        return View("Accounts", await BuildAccountsVmAsync(id, ct, showLock: true));
    }

    [HttpPost("/admin/tai-khoan/khoa")][ValidateAntiForgeryToken]
    public async Task<IActionResult> LockAccount(IFormCollection form, CancellationToken ct)
    {
        var id = int.TryParse(form["id"].ToString(), out var parsed) ? parsed : (int?)null;
        if (!id.HasValue && int.TryParse(Request.Query["id"], out var qid)) id = qid;
        var reason = (form["reason"].ToString() ?? "").Trim();
        if (!id.HasValue) return RedirectToAction(nameof(Accounts));

        var account = await db.TaiKhoans.FirstOrDefaultAsync(a => a.Id == id.Value, ct);
        if (account == null) return RedirectToAction(nameof(Accounts));

        var myId = User.TaiKhoanId();
        if (myId.HasValue && account.Id == myId.Value)
        {
            TempData["msg"] = "Không thể tự khóa tài khoản đang đăng nhập.";
            TempData["tone"] = "danger";
            return RedirectToAction(nameof(Accounts), new { id = account.Id });
        }

        if (account.TrangThai == TrangThaiTaiKhoan.HoatDong && account.VaiTro == VaiTro.Admin)
        {
            var activeAdmins = await db.TaiKhoans.CountAsync(a => a.VaiTro == VaiTro.Admin && a.TrangThai == TrangThaiTaiKhoan.HoatDong && a.Id != account.Id, ct);
            if (activeAdmins <= 0)
            {
                TempData["msg"] = "Không thể khóa Admin hoạt động cuối cùng.";
                TempData["tone"] = "danger";
                return RedirectToAction(nameof(Accounts), new { id = account.Id });
            }
        }

        var locking = account.TrangThai == TrangThaiTaiKhoan.HoatDong;
        account.TrangThai = locking ? TrangThaiTaiKhoan.BiKhoa : TrangThaiTaiKhoan.HoatDong;
        account.GhiChu = string.IsNullOrWhiteSpace(reason) ? account.GhiChu : (account.GhiChu == null ? reason : account.GhiChu + " | " + reason);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(locking ? "Khóa tài khoản" : "Mở khóa tài khoản", $"{account.HoTen} ({account.TenDangNhap})", "Thành công", reason, ct);
        TempData["msg"] = locking ? "Đã khóa tài khoản." : "Đã mở khóa tài khoản.";
        TempData["tone"] = "success";
        return RedirectToAction(nameof(Accounts), new { id = account.Id });
    }

    [HttpGet("/admin/tai-khoan/tao")]
    public async Task<IActionResult> CreateAccount(CancellationToken ct)
        => View("Accounts", await BuildAccountsVmAsync(null, ct, showForm: true, form: new AdminAccountFormViewModel()));

    [HttpGet("/admin/tai-khoan/sua")]
    public async Task<IActionResult> EditAccount(int? id, CancellationToken ct)
    {
        if (!id.HasValue) return RedirectToAction(nameof(Accounts));
        var a = await db.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id.Value, ct);
        if (a == null) return RedirectToAction(nameof(Accounts));
        return View("Accounts", await BuildAccountsVmAsync(id, ct, showForm: true, form: new AdminAccountFormViewModel
        {
            Id = a.Id,
            Title = $"Chỉnh sửa tài khoản · {a.HoTen}",
            SubmitHref = "/admin/tai-khoan/sua",
            CancelHref = $"/admin/tai-khoan?id={a.Id}",
            HoTen = a.HoTen, TenDangNhap = a.TenDangNhap, Email = a.Email,
            SoDienThoai = a.SoDienThoai ?? "", MaNhanSu = a.MaNhanSu ?? "",
            VaiTro = a.VaiTro.ToString(), TrangThai = a.TrangThai.ToString()
        }));
    }

    [HttpPost("/admin/tai-khoan/tao")][ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAccount(IFormCollection form, CancellationToken ct)
    {
        var vm = ReadAccountForm(form);
        if (string.IsNullOrWhiteSpace(vm.HoTen) || string.IsNullOrWhiteSpace(vm.TenDangNhap) || string.IsNullOrWhiteSpace(vm.Email) || string.IsNullOrWhiteSpace(form["matKhau"].ToString()))
            return View("Accounts", await BuildAccountsVmAsync(null, ct, showForm: true, form: vm with { Error = "Vui lòng nhập đầy đủ họ tên, tên đăng nhập, email và mật khẩu." }));
        if (await db.TaiKhoans.AnyAsync(a => a.TenDangNhap == vm.TenDangNhap || a.Email == vm.Email, ct))
            return View("Accounts", await BuildAccountsVmAsync(null, ct, showForm: true, form: vm with { Error = "Tên đăng nhập hoặc email đã tồn tại." }));
        if (!Enum.TryParse<VaiTro>(vm.VaiTro, true, out var vaiTro)) vaiTro = VaiTro.Manager;
        if (!Enum.TryParse<TrangThaiTaiKhoan>(vm.TrangThai, true, out var trangThai)) trangThai = TrangThaiTaiKhoan.HoatDong;
        var account = new TaiKhoan
        {
            TenDangNhap = vm.TenDangNhap.Trim(),
            MatKhauHash = PasswordHelper.Hash(form["matKhau"].ToString()!),
            HoTen = vm.HoTen.Trim(),
            Email = vm.Email.Trim(),
            SoDienThoai = NullIfEmpty(vm.SoDienThoai),
            MaNhanSu = NullIfEmpty(vm.MaNhanSu),
            VaiTro = vaiTro,
            TrangThai = trangThai,
            NgayTao = DateTimeOffset.UtcNow
        };
        db.TaiKhoans.Add(account);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync("Tạo tài khoản", $"{account.HoTen} ({account.TenDangNhap})", "Thành công", $"Vai trò {account.VaiTro}", ct);
        TempData["msg"] = "Đã tạo tài khoản.";
        TempData["tone"] = "success";
        return RedirectToAction(nameof(Accounts), new { id = account.Id });
    }

    [HttpPost("/admin/tai-khoan/sua")][ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAccount(IFormCollection form, CancellationToken ct)
    {
        var vm = ReadAccountForm(form);
        if (vm.Id <= 0) return RedirectToAction(nameof(Accounts));
        var account = await db.TaiKhoans.FirstOrDefaultAsync(a => a.Id == vm.Id, ct);
        if (account == null) return RedirectToAction(nameof(Accounts));
        if (string.IsNullOrWhiteSpace(vm.HoTen) || string.IsNullOrWhiteSpace(vm.Email))
            return View("Accounts", await BuildAccountsVmAsync(vm.Id, ct, showForm: true, form: vm with { Title = $"Chỉnh sửa tài khoản · {account.HoTen}", SubmitHref = "/admin/tai-khoan/sua", CancelHref = $"/admin/tai-khoan?id={vm.Id}", Error = "Vui lòng nhập họ tên và email." }));
        if (await db.TaiKhoans.AnyAsync(a => a.Email == vm.Email && a.Id != vm.Id, ct))
            return View("Accounts", await BuildAccountsVmAsync(vm.Id, ct, showForm: true, form: vm with { Title = $"Chỉnh sửa tài khoản · {account.HoTen}", SubmitHref = "/admin/tai-khoan/sua", CancelHref = $"/admin/tai-khoan?id={vm.Id}", Error = "Email đã được sử dụng." }));
        if (!Enum.TryParse<VaiTro>(vm.VaiTro, true, out var vaiTro)) vaiTro = account.VaiTro;
        if (!Enum.TryParse<TrangThaiTaiKhoan>(vm.TrangThai, true, out var trangThai)) trangThai = account.TrangThai;
        // protect last active admin from demotion/lock via edit
        var demotingLastAdmin = account.VaiTro == VaiTro.Admin && account.TrangThai == TrangThaiTaiKhoan.HoatDong && (vaiTro != VaiTro.Admin || trangThai != TrangThaiTaiKhoan.HoatDong)
            && await db.TaiKhoans.CountAsync(a => a.VaiTro == VaiTro.Admin && a.TrangThai == TrangThaiTaiKhoan.HoatDong && a.Id != account.Id, ct) <= 0;
        if (demotingLastAdmin)
            return View("Accounts", await BuildAccountsVmAsync(vm.Id, ct, showForm: true, form: vm with { Title = $"Chỉnh sửa tài khoản · {account.HoTen}", SubmitHref = "/admin/tai-khoan/sua", CancelHref = $"/admin/tai-khoan?id={vm.Id}", Error = "Không thể hạ quyền hoặc khóa Admin hoạt động cuối cùng." }));
        account.HoTen = vm.HoTen.Trim();
        account.Email = vm.Email.Trim();
        account.SoDienThoai = NullIfEmpty(vm.SoDienThoai);
        account.MaNhanSu = NullIfEmpty(vm.MaNhanSu);
        account.VaiTro = vaiTro;
        account.TrangThai = trangThai;
        var newPw = form["matKhau"].ToString();
        if (!string.IsNullOrWhiteSpace(newPw)) account.MatKhauHash = PasswordHelper.Hash(newPw);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync("Cập nhật tài khoản", $"{account.HoTen} ({account.TenDangNhap})", "Thành công", "Admin chỉnh sửa thông tin/vai trò/trạng thái", ct);
        TempData["msg"] = "Đã cập nhật tài khoản.";
        TempData["tone"] = "success";
        return RedirectToAction(nameof(Accounts), new { id = account.Id });
    }

    private static AdminAccountFormViewModel ReadAccountForm(IFormCollection form) => new()
    {
        Id = int.TryParse(form["id"], out var id) ? id : 0,
        HoTen = form["hoTen"].ToString(),
        TenDangNhap = form["tenDangNhap"].ToString(),
        Email = form["email"].ToString(),
        SoDienThoai = form["soDienThoai"].ToString(),
        MaNhanSu = form["maNhanSu"].ToString(),
        VaiTro = string.IsNullOrWhiteSpace(form["vaiTro"]) ? "Manager" : form["vaiTro"].ToString(),
        TrangThai = string.IsNullOrWhiteSpace(form["trangThai"]) ? "HoatDong" : form["trangThai"].ToString(),
        SubmitHref = (int.TryParse(form["id"], out var id2) && id2 > 0) ? "/admin/tai-khoan/sua" : "/admin/tai-khoan/tao"
    };

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    [HttpGet("/admin/vai-tro")]
    public async Task<IActionResult> Roles(CancellationToken ct)
    {
        var counts = await db.TaiKhoans.GroupBy(a => a.VaiTro).Select(g => new { Role = g.Key, Count = g.Count() }).ToListAsync(ct);
        int C(VaiTro r) => counts.FirstOrDefault(x => x.Role == r)?.Count ?? 0;
        return View(new AdminRolesViewModel
        {
            Roles = new[]
            {
                new AdminRoleCard("Admin", $"{C(VaiTro.Admin)} tài khoản", "Toàn quyền quản trị"),
                new AdminRoleCard("Manager", $"{C(VaiTro.Manager)} tài khoản", "Vận hành nghiệp vụ"),
                new AdminRoleCard("Khách hàng", $"{C(VaiTro.KhachHang)} tài khoản", "Đặt vé và tra cứu")
            },
            Permissions = new[]
            {
                new AdminPermissionRow("Tài khoản & phân quyền", "X · T · S · D", "—", "X (cá nhân)"),
                new AdminPermissionRow("Danh mục & lịch bay", "X · T · S · D", "X · T · S", "X"),
                new AdminPermissionRow("Đặt chỗ & vé", "X · T · S · D", "X · T · S", "X · T (của mình)"),
                new AdminPermissionRow("Đổi / hoàn", "X · T · S · D", "X · S · D", "X · T (của mình)"),
                new AdminPermissionRow("Báo cáo & nhật ký", "X", "X (phạm vi)", "—")
            },
            ControlUpdated = DateTimeOffset.UtcNow.ToOffset(Vn).ToString("dd/MM/yyyy · HH:mm")
        });
    }

    [HttpPost("/admin/vai-tro")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Roles(IFormCollection form, CancellationToken ct)
    {
        await WriteAuditAsync("Xác nhận phân quyền", "Vai trò hệ thống (GĐ2)", "Thành công", "Phân quyền được kiểm soát bởi vai trò Admin/Manager/Khách hàng; ma trận hiển thị theo quy tắc nghiệp vụ.", ct);
        TempData["msg"] = "Đã ghi nhận. Phân quyền trên GĐ2 được kiểm soát theo vai trò hệ thống (Admin/Manager/Khách hàng).";
        TempData["tone"] = "info";
        return RedirectToAction(nameof(Roles));
    }

    [HttpGet("/admin/danh-muc")]
    public async Task<IActionResult> Catalog(string? tab, int? id, string? q, CancellationToken ct)
    {
        var tabQuery = (tab ?? "").Trim().ToLowerInvariant();
        var key = tabQuery switch { "hang-bay" => "airlines", "tuyen-bay" => "routes", "tau-bay" => "aircraft", "hang-ghe" => "cabin", _ => "airports" };
        string EditHref(string k, int eid) => k == "airports" ? $"/admin/danh-muc?id={eid}" : $"/admin/danh-muc?tab={tabQuery}&id={eid}";

        var airports = await db.SanBays.AsNoTracking().OrderBy(a => a.MaSanBay).ToListAsync(ct);
        var airlines = await db.HangHangKhongS.AsNoTracking().OrderBy(a => a.MaHang).ToListAsync(ct);
        var routes = await db.TuyenBays.AsNoTracking().Include(r => r.SanBayDi).Include(r => r.SanBayDen).OrderBy(r => r.SanBayDi.MaSanBay).ThenBy(r => r.SanBayDen.MaSanBay).ToListAsync(ct);
        var aircraft = await db.MayBays.AsNoTracking().OrderBy(a => a.MaMayBay).ToListAsync(ct);
        var cabins = await db.HangVes.AsNoTracking()
            .GroupBy(h => new { h.MaHang, h.TenHang, h.NhomHang, h.HanhLyKyGuiKg })
            .Select(g => new { g.Key.MaHang, g.Key.TenHang, g.Key.NhomHang, g.Key.HanhLyKyGuiKg, Count = g.Count() })
            .OrderBy(x => x.MaHang).Take(50).ToListAsync(ct);

        var tabs = new[]
        {
            new AdminCatalogTab("airports", "Sân bay", airports.Count.ToString(), key == "airports", "/admin/danh-muc"),
            new AdminCatalogTab("airlines", "Hãng bay", airlines.Count.ToString(), key == "airlines", "/admin/danh-muc?tab=hang-bay"),
            new AdminCatalogTab("routes", "Tuyến bay", routes.Count.ToString(), key == "routes", "/admin/danh-muc?tab=tuyen-bay"),
            new AdminCatalogTab("aircraft", "Tàu bay", aircraft.Count.ToString(), key == "aircraft", "/admin/danh-muc?tab=tau-bay"),
            new AdminCatalogTab("cabin", "Hạng ghế", cabins.Count.ToString(), key == "cabin", "/admin/danh-muc?tab=hang-ghe")
        };

        bool Has(string? hay, string? needle) => string.IsNullOrWhiteSpace(needle) || (hay ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase);
        var editId = id.GetValueOrDefault();
        AdminCatalogViewModel vm;
        if (key == "airlines")
        {
            var filtered = airlines.Where(a => Has(a.MaHang, q) || Has(a.TenHang, q) || Has(a.QuocGia, q)).ToList();
            var sel = editId > 0 ? airlines.FirstOrDefault(a => a.Id == editId) : null;
            vm = new AdminCatalogViewModel { Tabs = tabs, SectionTitle = "Hãng hàng không", SectionSubtitle = $"{filtered.Count}/{airlines.Count} hãng bay", SearchLabel = "Tìm", SearchPlaceholder = "Tên hoặc mã", Columns = new AdminCatalogColumns("Mã", "Hãng bay / Quốc gia", "ICAO", "Trạng thái"),
                Rows = filtered.Select(a => new AdminCatalogRow(a.MaHang, a.TenHang, a.QuocGia, a.MaIcao ?? "—", a.DangHoatDong ? "Hoạt động" : "Tạm dừng", sel?.Id == a.Id, a.Id, EditHref(key, a.Id))).ToList(),
                ResultText = $"Hiển thị {filtered.Count} hãng bay", EditorTitle = sel == null ? "Thêm hãng bay" : $"Sửa hãng bay · {sel.MaHang}", EditorSubtitle = sel == null ? "Tạo mới" : "Dữ liệu thực", EditorId = sel?.Id ?? 0, EditorTabKey = "airlines", AddText = "Thêm hãng bay",
                EditorFields = new[] { new AdminEditorField("Mã hãng", sel?.MaHang ?? "", null, null, true, "MaHang"), new AdminEditorField("Tên hãng", sel?.TenHang ?? "", null, null, true, "TenHang"), new AdminEditorField("Quốc gia", sel?.QuocGia ?? "", null, null, false, "QuocGia"), new AdminEditorField("ICAO", sel?.MaIcao ?? "", null, null, false, "MaIcao"), new AdminEditorField("Trạng thái", (sel?.DangHoatDong ?? true) ? "Hoạt động" : "Tạm dừng", null, null, false, "TrangThai", new[] { "Hoạt động", "Tạm dừng" }) } };
        }
        else if (key == "routes")
        {
            var filtered = routes.Where(r => Has(r.SanBayDi.MaSanBay + r.SanBayDen.MaSanBay, q) || Has(r.SanBayDi.ThanhPho, q)).ToList();
            var sel = editId > 0 ? routes.FirstOrDefault(r => r.Id == editId) : null;
            var apOpts = airports.Select(a => $"{a.Id}|{a.MaSanBay} - {a.TenSanBay}").ToList();
            string ApVal(int? aid) { var a = airports.FirstOrDefault(x => x.Id == aid); return a == null ? (apOpts.FirstOrDefault() ?? "") : $"{a.Id}|{a.MaSanBay} - {a.TenSanBay}"; }
            vm = new AdminCatalogViewModel { Tabs = tabs, SectionTitle = "Tuyến bay", SectionSubtitle = $"{filtered.Count}/{routes.Count} tuyến bay", SearchLabel = "Tìm", SearchPlaceholder = "Mã tuyến", Columns = new AdminCatalogColumns("Mã tuyến", "Tuyến bay", "Thành phố", "Trạng thái"),
                Rows = filtered.Select(r => new AdminCatalogRow($"{r.SanBayDi.MaSanBay}-{r.SanBayDen.MaSanBay}", $"{r.SanBayDi.MaSanBay} → {r.SanBayDen.MaSanBay}", $"{r.SanBayDi.ThanhPho} → {r.SanBayDen.ThanhPho}", r.DangKhaiThac ? "Đang khai thác" : "Ngừng", r.DangKhaiThac ? "Đang khai thác" : "Ngừng", sel?.Id == r.Id, r.Id, EditHref(key, r.Id))).ToList(),
                ResultText = $"Hiển thị {filtered.Count} tuyến bay", EditorTitle = sel == null ? "Thêm tuyến bay" : $"Sửa tuyến bay · {sel.SanBayDi.MaSanBay}-{sel.SanBayDen.MaSanBay}", EditorSubtitle = sel == null ? "Tạo mới" : "Dữ liệu thực", EditorId = sel?.Id ?? 0, EditorTabKey = "routes", AddText = "Thêm tuyến bay",
                EditorFields = new[] { new AdminEditorField("Sân bay đi", ApVal(sel?.SanBayDiId), null, null, true, "SanBayDiId", apOpts), new AdminEditorField("Sân bay đến", ApVal(sel?.SanBayDenId), null, null, true, "SanBayDenId", apOpts), new AdminEditorField("Thời lượng dự kiến (phút)", sel?.ThoiLuongDuKienPhut?.ToString() ?? "", null, null, false, "ThoiLuongDuKienPhut"), new AdminEditorField("Trạng thái", (sel?.DangKhaiThac ?? true) ? "Đang khai thác" : "Ngừng", null, null, false, "TrangThai", new[] { "Đang khai thác", "Ngừng" }) } };
        }
        else if (key == "aircraft")
        {
            var filtered = aircraft.Where(a => Has(a.MaMayBay, q) || Has(a.TenMayBay, q) || Has(a.HangSanXuat, q)).ToList();
            var sel = editId > 0 ? aircraft.FirstOrDefault(a => a.Id == editId) : null;
            vm = new AdminCatalogViewModel { Tabs = tabs, SectionTitle = "Đội tàu bay", SectionSubtitle = $"{filtered.Count}/{aircraft.Count} tàu bay", SearchLabel = "Tìm", SearchPlaceholder = "Mã hoặc dòng tàu", Columns = new AdminCatalogColumns("Mã tàu", "Tàu bay", "Số ghế", "Trạng thái"),
                Rows = filtered.Select(a => new AdminCatalogRow(a.MaMayBay, a.TenMayBay, a.HangSanXuat, $"{a.SoGhe} ghế", a.DangHoatDong ? "Hoạt động" : "Bảo trì", sel?.Id == a.Id, a.Id, EditHref(key, a.Id))).ToList(),
                ResultText = $"Hiển thị {filtered.Count} tàu bay", EditorTitle = sel == null ? "Thêm tàu bay" : $"Sửa tàu bay · {sel.MaMayBay}", EditorSubtitle = sel == null ? "Tạo mới" : "Dữ liệu thực", EditorId = sel?.Id ?? 0, EditorTabKey = "aircraft", AddText = "Thêm tàu bay",
                EditorFields = new[] { new AdminEditorField("Mã tàu bay", sel?.MaMayBay ?? "", null, null, true, "MaMayBay"), new AdminEditorField("Tên tàu bay", sel?.TenMayBay ?? "", null, null, true, "TenMayBay"), new AdminEditorField("Hãng sản xuất", sel?.HangSanXuat ?? "", null, null, false, "HangSanXuat"), new AdminEditorField("Số ghế", sel?.SoGhe.ToString() ?? "", null, null, true, "SoGhe"), new AdminEditorField("Năm khai thác", sel?.NamKhaiThac?.ToString() ?? "", null, null, false, "NamKhaiThac"), new AdminEditorField("Trạng thái", (sel?.DangHoatDong ?? true) ? "Hoạt động" : "Bảo trì", null, null, false, "TrangThai", new[] { "Hoạt động", "Bảo trì" }) } };
        }
        else if (key == "cabin")
        {
            var filtered = cabins.Where(c => Has(c.MaHang, q) || Has(c.TenHang, q)).ToList();
            vm = new AdminCatalogViewModel { Tabs = tabs, SectionTitle = "Hạng ghế", SectionSubtitle = $"{filtered.Count} nhóm hạng ghế (từ dữ liệu chuyến bay)", SearchLabel = "Tìm", SearchPlaceholder = "Mã hoặc tên hạng", Columns = new AdminCatalogColumns("Mã hạng", "Hạng ghế / Nhóm", "Hành lý", "Trạng thái"),
                Rows = filtered.Select(c => new AdminCatalogRow(c.MaHang, c.TenHang, c.NhomHang, $"{c.HanhLyKyGuiKg} kg ký gửi", $"Áp dụng {c.Count} chuyến", false)).ToList(),
                ResultText = $"Hiển thị {filtered.Count} nhóm hạng ghế", EditorTitle = "Hạng ghế", EditorSubtitle = "Hạng ghế được quản lý theo chuyến bay (dữ liệu đọc từ HangVe).", EditorFields = Array.Empty<AdminEditorField>(), AddText = "Thêm hạng ghế" };
        }
        else
        {
            var filtered = airports.Where(a => Has(a.MaSanBay, q) || Has(a.TenSanBay, q) || Has(a.ThanhPho, q)).ToList();
            var sel = editId > 0 ? airports.FirstOrDefault(a => a.Id == editId) : null;
            vm = new AdminCatalogViewModel { Tabs = tabs, SectionTitle = "Sân bay", SectionSubtitle = $"{filtered.Count}/{airports.Count} sân bay", SearchLabel = "Tìm", SearchPlaceholder = "Tên hoặc IATA", Columns = new AdminCatalogColumns("IATA", "Sân bay / Thành phố", "Quốc gia", "Trạng thái"),
                Rows = filtered.Select(a => new AdminCatalogRow(a.MaSanBay, a.TenSanBay, a.ThanhPho, a.QuocGia, a.DangHoatDong ? "Hoạt động" : "Tạm dừng", sel?.Id == a.Id, a.Id, EditHref(key, a.Id))).ToList(),
                ResultText = $"Hiển thị {filtered.Count} sân bay", EditorTitle = sel == null ? "Thêm sân bay" : $"Sửa sân bay · {sel.MaSanBay}", EditorSubtitle = sel == null ? "Tạo mới" : "Dữ liệu thực", EditorId = sel?.Id ?? 0, EditorTabKey = "airports", AddText = "Thêm sân bay",
                EditorFields = new[] { new AdminEditorField("Mã IATA", sel?.MaSanBay ?? "", null, null, true, "MaSanBay"), new AdminEditorField("Tên sân bay", sel?.TenSanBay ?? "", null, null, true, "TenSanBay"), new AdminEditorField("Thành phố", sel?.ThanhPho ?? "", null, null, false, "ThanhPho"), new AdminEditorField("Quốc gia", sel?.QuocGia ?? "", null, null, false, "QuocGia"), new AdminEditorField("Múi giờ", sel?.MuiGio ?? "UTC+7", null, null, false, "MuiGio"), new AdminEditorField("Trạng thái", (sel?.DangHoatDong ?? true) ? "Hoạt động" : "Tạm dừng", null, null, false, "TrangThai", new[] { "Hoạt động", "Tạm dừng" }) } };
        }
        vm.Message = TempData["msg"] as string ?? "Dữ liệu hiển thị từ cơ sở dữ liệu PostgreSQL.";
        vm.MessageTone = TempData["tone"] as string ?? (TempData["msg"] == null ? "success" : "success");
        return View(vm);
    }

    [HttpPost("/admin/danh-muc")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Catalog(IFormCollection form, CancellationToken ct)
    {
        var tabQuery = (form["tab"].ToString() ?? "").Trim().ToLowerInvariant();
        var key = tabQuery switch { "hang-bay" => "airlines", "tuyen-bay" => "routes", "tau-bay" => "aircraft", "hang-ghe" => "cabin", _ => "airports" };
        var eid = int.TryParse(form["id"], out var parsedId) ? parsedId : 0;
        if (key == "cabin")
        {
            TempData["msg"] = "Hạng ghế được quản lý theo chuyến bay; không chỉnh sửa trực tiếp tại danh mục.";
            TempData["tone"] = "info";
            return RedirectToAction(nameof(Catalog), new { tab = tabQuery });
        }

        try
        {
            if (key == "airports")
            {
                var ma = (form["MaSanBay"].ToString() ?? "").Trim().ToUpperInvariant();
                var ten = (form["TenSanBay"].ToString() ?? "").Trim();
                if (string.IsNullOrWhiteSpace(ma) || string.IsNullOrWhiteSpace(ten)) throw new InvalidOperationException("Mã IATA và tên sân bay là bắt buộc.");
                var row = eid > 0 ? await db.SanBays.FirstOrDefaultAsync(x => x.Id == eid, ct) : null;
                if (eid > 0 && row == null) throw new InvalidOperationException("Không tìm thấy sân bay.");
                if (await db.SanBays.AnyAsync(x => x.MaSanBay == ma && x.Id != eid, ct)) throw new InvalidOperationException("Mã IATA đã tồn tại.");
                row ??= new SanBay();
                row.MaSanBay = ma; row.TenSanBay = ten; row.ThanhPho = (form["ThanhPho"].ToString() ?? "").Trim(); row.QuocGia = (form["QuocGia"].ToString() ?? "").Trim(); row.MuiGio = NullIfEmpty(form["MuiGio"].ToString()) ?? "UTC+7"; row.DangHoatDong = (form["TrangThai"].ToString() ?? "").Contains("Hoạt", StringComparison.OrdinalIgnoreCase);
                if (eid <= 0) db.SanBays.Add(row);
                await db.SaveChangesAsync(ct);
                await WriteAuditAsync(eid > 0 ? "Cập nhật danh mục" : "Thêm danh mục", $"Sân bay {row.MaSanBay}", "Thành công", row.TenSanBay, ct);
                eid = row.Id;
            }
            else if (key == "airlines")
            {
                var ma = (form["MaHang"].ToString() ?? "").Trim().ToUpperInvariant();
                var ten = (form["TenHang"].ToString() ?? "").Trim();
                if (string.IsNullOrWhiteSpace(ma) || string.IsNullOrWhiteSpace(ten)) throw new InvalidOperationException("Mã hãng và tên hãng là bắt buộc.");
                var row = eid > 0 ? await db.HangHangKhongS.FirstOrDefaultAsync(x => x.Id == eid, ct) : null;
                if (eid > 0 && row == null) throw new InvalidOperationException("Không tìm thấy hãng bay.");
                if (await db.HangHangKhongS.AnyAsync(x => x.MaHang == ma && x.Id != eid, ct)) throw new InvalidOperationException("Mã hãng đã tồn tại.");
                row ??= new HangHangKhong();
                row.MaHang = ma; row.TenHang = ten; row.QuocGia = (form["QuocGia"].ToString() ?? "").Trim(); row.MaIcao = NullIfEmpty(form["MaIcao"].ToString()); row.DangHoatDong = !(form["TrangThai"].ToString() ?? "").Contains("Tạm", StringComparison.OrdinalIgnoreCase);
                if (eid <= 0) db.HangHangKhongS.Add(row);
                await db.SaveChangesAsync(ct);
                await WriteAuditAsync(eid > 0 ? "Cập nhật danh mục" : "Thêm danh mục", $"Hãng bay {row.MaHang}", "Thành công", row.TenHang, ct);
                eid = row.Id;
            }
            else if (key == "aircraft")
            {
                var ma = (form["MaMayBay"].ToString() ?? "").Trim().ToUpperInvariant();
                var ten = (form["TenMayBay"].ToString() ?? "").Trim();
                var soGhe = (int)ParseDec(form["SoGhe"].ToString(), 0);
                if (string.IsNullOrWhiteSpace(ma) || string.IsNullOrWhiteSpace(ten) || soGhe <= 0) throw new InvalidOperationException("Mã tàu, tên tàu và số ghế hợp lệ là bắt buộc.");
                var row = eid > 0 ? await db.MayBays.FirstOrDefaultAsync(x => x.Id == eid, ct) : null;
                if (eid > 0 && row == null) throw new InvalidOperationException("Không tìm thấy tàu bay.");
                if (await db.MayBays.AnyAsync(x => x.MaMayBay == ma && x.Id != eid, ct)) throw new InvalidOperationException("Mã tàu bay đã tồn tại.");
                row ??= new MayBay();
                row.MaMayBay = ma; row.TenMayBay = ten; row.HangSanXuat = (form["HangSanXuat"].ToString() ?? "").Trim(); row.SoGhe = soGhe; row.NamKhaiThac = int.TryParse(new string((form["NamKhaiThac"].ToString() ?? "").Where(char.IsDigit).ToArray()), out var nk) ? nk : null; row.DangHoatDong = !(form["TrangThai"].ToString() ?? "").Contains("Bảo", StringComparison.OrdinalIgnoreCase);
                if (eid <= 0) db.MayBays.Add(row);
                await db.SaveChangesAsync(ct);
                await WriteAuditAsync(eid > 0 ? "Cập nhật danh mục" : "Thêm danh mục", $"Tàu bay {row.MaMayBay}", "Thành công", row.TenMayBay, ct);
                eid = row.Id;
            }
            else if (key == "routes")
            {
                int ParseApId(string? v) { var s = (v ?? "").Trim(); var p = s.IndexOf('|'); return int.TryParse(p >= 0 ? s[..p] : s, out var i) ? i : 0; }
                var di = ParseApId(form["SanBayDiId"].ToString());
                var den = ParseApId(form["SanBayDenId"].ToString());
                if (di <= 0 || den <= 0 || di == den) throw new InvalidOperationException("Vui lòng chọn sân bay đi/đến hợp lệ và khác nhau.");
                var row = eid > 0 ? await db.TuyenBays.FirstOrDefaultAsync(x => x.Id == eid, ct) : null;
                if (eid > 0 && row == null) throw new InvalidOperationException("Không tìm thấy tuyến bay.");
                if (await db.TuyenBays.AnyAsync(x => x.SanBayDiId == di && x.SanBayDenId == den && x.Id != eid, ct)) throw new InvalidOperationException("Tuyến bay này đã tồn tại.");
                row ??= new TuyenBay();
                row.SanBayDiId = di; row.SanBayDenId = den; row.ThoiLuongDuKienPhut = int.TryParse(new string((form["ThoiLuongDuKienPhut"].ToString() ?? "").Where(char.IsDigit).ToArray()), out var tl) ? tl : null; row.DangKhaiThac = !(form["TrangThai"].ToString() ?? "").Contains("Ngừng", StringComparison.OrdinalIgnoreCase);
                if (eid <= 0) db.TuyenBays.Add(row);
                await db.SaveChangesAsync(ct);
                await WriteAuditAsync(eid > 0 ? "Cập nhật danh mục" : "Thêm danh mục", $"Tuyến bay {di}->{den}", "Thành công", null, ct);
                eid = row.Id;
            }
            TempData["msg"] = "Đã lưu danh mục.";
            TempData["tone"] = "success";
        }
        catch (Exception ex)
        {
            TempData["msg"] = ex.Message;
            TempData["tone"] = "danger";
        }
        return RedirectToAction(nameof(Catalog), new { tab = key == "airports" ? null : tabQuery, id = eid > 0 ? eid : (int?)null });
    }

    [HttpGet("/admin/cau-hinh")]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        var list = await db.CauHinhHeThongs.AsNoTracking().OrderBy(c => c.Nhom).ThenBy(c => c.Khoa).ToListAsync(ct);
        string Get(string key, string fallback) => list.FirstOrDefault(c => c.Khoa == key)?.GiaTri ?? fallback;
        var hold = Get("booking.hold_minutes", "15");
        var serviceFee = Get("booking.service_fee", "70000");
        var tax = Get("booking.tax_default", "380000");
        var changeFee = Get("policy.change_fee", "360000");
        var refundFee = Get("policy.refund_fee", "500000");
        var hotline = Get("contact.hotline", "1900 636 063");
        var email = Get("contact.email", "support@aeroviet.vn");
        var sender = Get("notification.sender_email", Get("contact.sender_email", "no-reply@aeroviet.vn"));
        return View(new AdminSettingsViewModel
        {
            VersionText = "Dữ liệu thực từ CauHinhHeThong",
            DirtyText = $"{list.Count} cấu hình",
            UpdatedText = $"Cập nhật: {list.OrderByDescending(c => c.NgayCapNhat).FirstOrDefault()?.NgayCapNhat.ToOffset(Vn).ToString("dd/MM/yyyy HH:mm") ?? "—"}",
            BookingFields = new[]
            {
                new AdminSettingField("Thời gian giữ chỗ (phút)", hold + " phút", "Hết hạn tự hủy đơn chưa thanh toán.", Name: "booking.hold_minutes", FormValue: hold),
                new AdminSettingField("Phí dịch vụ (VNĐ)", Money.Vnd(ParseDec(serviceFee, 70000m)), null, Name: "booking.service_fee", FormValue: serviceFee),
                new AdminSettingField("Thuế & phí mặc định (VNĐ)", Money.Vnd(ParseDec(tax, 380000m)), null, Name: "booking.tax_default", FormValue: tax)
            },
            PolicyFields = new[]
            {
                new AdminSettingField("Phí đổi (VNĐ)", Money.Vnd(ParseDec(changeFee, 360000m)) + " + chênh lệch", null, Name: "policy.change_fee", FormValue: changeFee),
                new AdminSettingField("Phí hoàn (VNĐ)", Money.Vnd(ParseDec(refundFee, 500000m)), null, Name: "policy.refund_fee", FormValue: refundFee),
                new AdminSettingField("Hotline", hotline, null, Name: "contact.hotline", FormValue: hotline)
            },
            NotificationFields = new[] { new AdminSettingField("Email hỗ trợ", email, null, Name: "contact.email", FormValue: email) },
            PolicyChecks = list.Select(c => $"{c.Khoa} = {c.GiaTri}").Take(8).ToList(),
            EmailSender = new AdminSettingField("Email gửi hệ thống", sender, null, Name: "notification.sender_email", FormValue: sender)
        });
    }

    [HttpPost("/admin/cau-hinh")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(IFormCollection form, CancellationToken ct)
    {
        var keys = new (string Key, string Group, string Kind)[]
        {
            ("booking.hold_minutes","booking","int"), ("booking.service_fee","booking","money"), ("booking.tax_default","booking","money"),
            ("policy.change_fee","policy","money"), ("policy.refund_fee","policy","money"),
            ("contact.hotline","contact","text"), ("contact.email","contact","text"), ("notification.sender_email","notification","text")
        };
        var changed = 0;
        foreach (var (key, group, kind) in keys)
        {
            if (!form.ContainsKey(key)) continue;
            var raw = (form[key].ToString() ?? "").Trim();
            var value = kind switch { "int" => Math.Max(0, (int)ParseDec(raw, 0)).ToString(), "money" => Math.Max(0, ParseDec(raw, 0)).ToString("0"), _ => raw };
            if (string.IsNullOrWhiteSpace(value) && kind == "text") continue;
            var row = await db.CauHinhHeThongs.FirstOrDefaultAsync(c => c.Khoa == key, ct);
            if (row == null)
            {
                db.CauHinhHeThongs.Add(new CauHinhHeThong { Khoa = key, GiaTri = value, Nhom = group, NgayCapNhat = DateTimeOffset.UtcNow, NguoiCapNhat = User.DisplayName() });
                changed++;
            }
            else if (row.GiaTri != value)
            {
                row.GiaTri = value; row.NgayCapNhat = DateTimeOffset.UtcNow; row.NguoiCapNhat = User.DisplayName(); changed++;
            }
        }
        if (changed > 0)
        {
            await db.SaveChangesAsync(ct);
            await WriteAuditAsync("Cập nhật cấu hình", "CauHinhHeThong", "Thành công", $"Đã lưu {changed} cấu hình", ct);
            TempData["msg"] = $"Đã lưu {changed} cấu hình nghiệp vụ.";
            TempData["tone"] = "success";
        }
        else
        {
            TempData["msg"] = "Không có thay đổi cấu hình.";
            TempData["tone"] = "info";
        }
        return RedirectToAction(nameof(Settings));
    }

    private static decimal ParseDec(string? raw, decimal fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        var digits = new string(raw.Where(c => char.IsDigit(c) || c == '-' || c == ',').ToArray()).Replace(',', '.');
        // VN display may be 70.000 (thousand sep) - if multiple dots treat as thousand separators
        if (digits.Count(c => c == '.') > 1) digits = digits.Replace(".", "");
        return decimal.TryParse(digits, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : fallback;
    }

    [HttpGet("/admin/nhat-ky")]
    public async Task<IActionResult> Audit(string? tu_ngay, string? nguoi_thuc_hien, string? loai_thao_tac, string? q, string? export, CancellationToken ct)
    {
        // view posts name="tu-ngay" etc; MVC binds hyphens variably, also accept Request.Query
        tu_ngay ??= Request.Query["tu-ngay"].ToString();
        nguoi_thuc_hien ??= Request.Query["nguoi-thuc-hien"].ToString();
        loai_thao_tac ??= Request.Query["loai-thao-tac"].ToString();

        var query = db.NhatKyHoatDongs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(loai_thao_tac) && !loai_thao_tac.Contains("Tất cả", StringComparison.OrdinalIgnoreCase))
            query = query.Where(n => n.HanhDong.Contains(loai_thao_tac) || n.DoiTuong.Contains(loai_thao_tac));
        if (!string.IsNullOrWhiteSpace(nguoi_thuc_hien) && !nguoi_thuc_hien.Contains("Tất cả", StringComparison.OrdinalIgnoreCase))
        {
            var actor = nguoi_thuc_hien.Split('/')[0].Trim();
            if (!string.IsNullOrWhiteSpace(actor)) query = query.Where(n => n.TenNguoiDung.Contains(actor) || n.VaiTro.Contains(actor));
        }
        // best-effort date range parse from "dd/MM/yyyy – dd/MM/yyyy" or "dd/MM/yyyy - dd/MM/yyyy"
        if (!string.IsNullOrWhiteSpace(tu_ngay))
        {
            var parts = tu_ngay.Split(new[] { '–', '-', '—' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 1 && DateTime.TryParseExact(parts[0].Trim(), "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d1))
            {
                var fromUtc = new DateTimeOffset(d1, Vn).ToUniversalTime();
                query = query.Where(n => n.ThoiGian >= fromUtc);
                if (parts.Length >= 2 && DateTime.TryParseExact(parts[1].Trim(), "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d2))
                {
                    var toUtc = new DateTimeOffset(d2.AddDays(1), Vn).ToUniversalTime();
                    query = query.Where(n => n.ThoiGian < toUtc);
                }
                else
                {
                    var toUtc = new DateTimeOffset(d1.AddDays(1), Vn).ToUniversalTime();
                    query = query.Where(n => n.ThoiGian < toUtc);
                }
            }
        }

        var list = await query.OrderByDescending(n => n.ThoiGian).Take(500).ToListAsync(ct);
        if (string.Equals(export, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("ThoiDiem,NguoiThucHien,VaiTro,ThaoTac,DoiTuong,KetQua,ChiTiet,IP");
            foreach (var n in list)
            {
                string Esc(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
                sb.AppendLine(string.Join(",", Esc(n.ThoiGian.ToOffset(Vn).ToString("dd/MM/yyyy HH:mm:ss")), Esc(n.TenNguoiDung), Esc(n.VaiTro), Esc(n.HanhDong), Esc(n.DoiTuong), Esc(n.KetQua.ToString()), Esc(n.ChiTiet), Esc(n.DiaChiIp)));
            }
            return File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv; charset=utf-8", $"nhat-ky-{DateTimeOffset.UtcNow:yyyyMMddHHmm}.csv");
        }

        var rows = list.Take(200).Select(n => new AdminAuditRow(
            n.ThoiGian.ToOffset(Vn).ToString("dd/MM/yyyy HH:mm:ss"),
            $"{n.TenNguoiDung} / {n.VaiTro}", n.HanhDong, n.DoiTuong,
            n.KetQua.ToString(), n.KetQua == KetQuaNhatKy.ThanhCong ? "success" : "danger")).ToList();
        var first = list.FirstOrDefault();
        var fromDisp = ""; var toDisp = "";
        if (!string.IsNullOrWhiteSpace(tu_ngay))
        {
            var dp = tu_ngay.Split(new[] { '–', '-', '—' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            fromDisp = dp.Length > 0 ? dp[0] : tu_ngay; toDisp = dp.Length > 1 ? dp[1] : fromDisp;
        }
        return View(new AdminAuditViewModel
        {
            FromDate = fromDisp, ToDate = toDisp, ActorFilter = string.IsNullOrWhiteSpace(nguoi_thuc_hien) ? "Tất cả người dùng" : nguoi_thuc_hien, TypeFilter = string.IsNullOrWhiteSpace(loai_thao_tac) ? "Tất cả thao tác" : loai_thao_tac,
            Rows = rows,
            ResultText = $"Hiển thị {rows.Count}/{list.Count} sự kiện (dữ liệu thực)",
            Detail = first == null ? new AdminAuditDetailViewModel { Title = "Chưa có sự kiện phù hợp", Subtitle = "", Left = Array.Empty<AdminDetailField>(), Right = Array.Empty<AdminDetailField>(), Content = "" } : new AdminAuditDetailViewModel
            {
                Title = $"Chi tiết · {first.MaNhatKy}",
                Subtitle = $"{first.HanhDong} · {first.ThoiGian.ToOffset(Vn):dd/MM/yyyy HH:mm:ss}",
                Left = new[] { new AdminDetailField("Người thực hiện", first.TenNguoiDung), new AdminDetailField("Vai trò", first.VaiTro), new AdminDetailField("IP", first.DiaChiIp ?? "—") },
                Right = new[] { new AdminDetailField("Đối tượng", first.DoiTuong), new AdminDetailField("Kết quả", first.KetQua.ToString()), new AdminDetailField("Chi tiết", first.ChiTiet ?? "—") },
                Content = first.ChiTiet ?? ""
            }
        });
    }

    [HttpPost("/admin/nhat-ky")][ValidateAntiForgeryToken]
    public IActionResult Audit(IFormCollection form) => RedirectToAction(nameof(Audit));

    private async Task WriteAuditAsync(string hanhDong, string doiTuong, string ketQuaText, string? chiTiet, CancellationToken ct)
    {
        var ketQua = ketQuaText.Contains("Thất bại", StringComparison.OrdinalIgnoreCase) || ketQuaText.Contains("Lỗi", StringComparison.OrdinalIgnoreCase)
            ? KetQuaNhatKy.ThatBai : KetQuaNhatKy.ThanhCong;
        db.NhatKyHoatDongs.Add(new NhatKyHoatDong
        {
            MaNhatKy = $"LOG-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Random.Shared.Next(100,999)}",
            TaiKhoanId = User.TaiKhoanId(),
            TenNguoiDung = User.DisplayName(),
            VaiTro = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Admin",
            HanhDong = hanhDong,
            DoiTuong = doiTuong,
            KetQua = ketQua,
            ChiTiet = string.IsNullOrWhiteSpace(chiTiet) ? ketQuaText : $"{ketQuaText}. {chiTiet}",
            DiaChiIp = HttpContext?.Connection?.RemoteIpAddress?.ToString(),
            ThoiGian = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
