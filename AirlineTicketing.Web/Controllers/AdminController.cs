using AirlineTicketing.Web.Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
<<<<<<< Updated upstream
=======
using Microsoft.EntityFrameworkCore;
using static AirlineTicketing.Web.Infrastructure.UiLanguage;
>>>>>>> Stashed changes

namespace AirlineTicketing.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    [HttpGet("/admin")]
<<<<<<< Updated upstream
    public IActionResult Index() => RedirectToAction("Index", "Dashboard");
=======
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
            StatusMeta = T($"Cập nhật {now.ToOffset(Vn):HH:mm}, {now.ToOffset(Vn):dd/MM/yyyy} (dữ liệu thực)", $"Updated at {now.ToOffset(Vn):HH:mm} on {now.ToOffset(Vn):dd/MM/yyyy} (live data)"),
            PeriodText = $"{monthStart:MM/yyyy}",
            Kpis = new[]
            {
                new AdminKpi(T("Doanh thu gộp (tháng)", "Gross revenue (month)"), Money.Vnd(gross), T("Thanh toán thành công (DB)", "Successful payments in database"), "success", "chart"),
                new AdminKpi(T("Vé đã xuất (tháng)", "Tickets issued (month)"), tickets.ToString("N0"), T("Theo NgayPhatHanh", "By issue date"), "success", "ticket"),
                new AdminKpi(T("Tài khoản hoạt động", "Active accounts"), activeAccounts.ToString("N0"), T("Từ bảng TaiKhoans", "From account records"), "info", "users"),
                new AdminKpi(T("Chuyến bay trong hệ thống", "Flights in system"), (await db.ChuyenBays.CountAsync(ct)).ToString("N0"), T("Tổng bản ghi", "Total records"), "success", "activity")
            },
            Activity = recent.Select(n => new AdminActivityRow(
                n.ThoiGian.ToOffset(Vn).ToString("HH:mm"),
                n.TenNguoiDung, $"{n.HanhDong} · {n.DoiTuong}",
                n.KetQua.ToString(), n.KetQua == KetQuaNhatKy.ThanhCong ? "success" : "danger")).ToList(),
            Attention = new[]
            {
                new AdminAttentionItem("alert", T("Dữ liệu đang lấy từ cơ sở dữ liệu", "Database connection"), T("Các trang admin hiển thị số liệu thực từ PostgreSQL.", "The admin dashboard uses live PostgreSQL data."), "info", T("Xem nhật ký", "View log"), "/admin/nhat-ky")
            },
            Health = new[]
            {
                new AdminHealthRow(T("Cơ sở dữ liệu", "Database"), T("Đang kết nối", "Connected")),
                new AdminHealthRow(T("Tài khoản seed", "Demo accounts"), activeAccounts > 0 ? T("Có dữ liệu", "Available") : T("Chưa có dữ liệu", "No data")),
                new AdminHealthRow(T("Nhật ký", "Log"), recent.Count > 0 ? T("Có bản ghi", "Entries available") : T("Chưa có bản ghi", "No entries"))
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
            a.TrangThai == TrangThaiTaiKhoan.HoatDong ? T("Hoạt động", "Active") : T("Đã khóa", "Locked"),
            a.TrangThai == TrangThaiTaiKhoan.HoatDong ? "success" : "danger",
            a.LanDangNhapCuoi?.ToOffset(Vn).ToString("dd/MM/yyyy HH:mm") ?? "—",
            T("Xem", "View"), $"/admin/tai-khoan?id={a.Id}", selected != null && a.Id == selected.Id)).ToList();

        var detail = selected == null ? new AdminAccountDetailViewModel() : new AdminAccountDetailViewModel
        {
            Id = selected.Id,
            Title = T($"Chi tiết · {selected.HoTen}", $"Details · {selected.HoTen}"),
            Subtitle = $"{selected.MaNhanSu ?? selected.TenDangNhap} · {selected.Email}",
            Fields = new[]
            {
                new AdminDetailField(T("Mã tài khoản", "Account ID"), selected.Id.ToString()),
                new AdminDetailField(T("Vai trò", "Role"), selected.VaiTro.ToString()),
                new AdminDetailField(T("Trạng thái", "Status"), selected.TrangThai == TrangThaiTaiKhoan.HoatDong ? T("Hoạt động", "Active") : T("Đã khóa", "Locked")),
                new AdminDetailField(T("Tên đăng nhập", "Username"), selected.TenDangNhap),
                new AdminDetailField(T("Đăng nhập cuối", "Last sign-in"), selected.LanDangNhapCuoi?.ToOffset(Vn).ToString("dd/MM/yyyy HH:mm") ?? "—")
            },
            PrimaryActionHref = $"/admin/tai-khoan/sua",
            DangerActionText = selected.TrangThai == TrangThaiTaiKhoan.HoatDong ? T("Khóa tài khoản", "Lock account") : T("Mở khóa tài khoản", "Unlock account"),
            DangerActionHref = $"/admin/tai-khoan/khoa?id={selected.Id}"
        };

        var lockDialog = new AdminLockDialogViewModel();
        if (showLock && selected != null)
        {
            var isLocking = selected.TrangThai == TrangThaiTaiKhoan.HoatDong;
            lockDialog = new AdminLockDialogViewModel
            {
                IsLocking = isLocking,
                AccountName = selected.HoTen,
                Email = selected.Email,
                RoleStatus = $"{selected.VaiTro} · {(isLocking ? T("Hoạt động", "Active") : T("Đã khóa", "Locked"))}",
                Reason = isLocking ? "Tạm ngừng quyền truy cập theo yêu cầu quản trị." : "Mở lại quyền truy cập theo xác nhận.",
                SubmitHref = "/admin/tai-khoan/khoa",
                CancelHref = $"/admin/tai-khoan?id={selected.Id}"
            };
        }

        return new AdminAccountsViewModel
        {
            Stats = new[]
            {
                new AdminKpi(T("Tất cả tài khoản", "All accounts"), list.Count.ToString("N0"), T("Theo DB", "Database"), "info", "users"),
                new AdminKpi(T("Hoạt động", "Active"), list.Count(a => a.TrangThai == TrangThaiTaiKhoan.HoatDong).ToString("N0"), "", "success", "check-circle"),
                new AdminKpi(T("Đã khóa", "Locked"), list.Count(a => a.TrangThai != TrangThaiTaiKhoan.HoatDong).ToString("N0"), "", "warning", "lock"),
                new AdminKpi(T("Nội bộ", "Staff"), list.Count(a => a.VaiTro != VaiTro.KhachHang).ToString("N0"), "Admin/Manager", "info", "id-card")
            },
            Accounts = rows,
            ResultText = T($"Hiển thị {rows.Count} tài khoản (dữ liệu thực)", $"Showing {rows.Count} live account records"),
            Detail = detail,
            ShowLockDialog = showLock,
            LockDialog = lockDialog,
            ShowFormDialog = showForm,
            Form = form ?? new AdminAccountFormViewModel(),
            Message = msg ?? (TempData["msg"] as string),
            MessageTone = string.IsNullOrWhiteSpace(msg) ? (TempData["tone"] as string ?? tone) : tone
        };
    }
>>>>>>> Stashed changes

    [HttpGet("/admin/tai-khoan")]
    public IActionResult Accounts() => RedirectToAction("Index", "QuanLyTaiKhoan");

    [HttpGet("/admin/tai-khoan/khoa")]
    public IActionResult LockAccount() => View("Accounts", DemoCatalog.AdminAccounts(showLockDialog: true));

<<<<<<< Updated upstream
    [HttpPost("/admin/tai-khoan/khoa")]
    [ValidateAntiForgeryToken]
    public IActionResult LockAccount(IFormCollection form) => RedirectToAction(nameof(Accounts));

    [HttpGet("/admin/vai-tro")]
    public IActionResult Roles() => RedirectToAction("Index", "QuanLyTaiKhoan");
=======
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
            TempData["msg"] = T("Không thể tự khóa tài khoản đang đăng nhập.", "You cannot lock the account you are signed in to.");
            TempData["tone"] = "danger";
            return RedirectToAction(nameof(Accounts), new { id = account.Id });
        }

        if (account.TrangThai == TrangThaiTaiKhoan.HoatDong && account.VaiTro == VaiTro.Admin)
        {
            var activeAdmins = await db.TaiKhoans.CountAsync(a => a.VaiTro == VaiTro.Admin && a.TrangThai == TrangThaiTaiKhoan.HoatDong && a.Id != account.Id, ct);
            if (activeAdmins <= 0)
            {
                TempData["msg"] = T("Không thể khóa Admin hoạt động cuối cùng.", "The last active administrator cannot be locked.");
                TempData["tone"] = "danger";
                return RedirectToAction(nameof(Accounts), new { id = account.Id });
            }
        }

        var locking = account.TrangThai == TrangThaiTaiKhoan.HoatDong;
        account.TrangThai = locking ? TrangThaiTaiKhoan.BiKhoa : TrangThaiTaiKhoan.HoatDong;
        account.GhiChu = string.IsNullOrWhiteSpace(reason) ? account.GhiChu : (account.GhiChu == null ? reason : account.GhiChu + " | " + reason);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(locking ? "Khóa tài khoản" : "Mở khóa tài khoản", $"{account.HoTen} ({account.TenDangNhap})", "Thành công", reason, ct);
        TempData["msg"] = locking ? T("Đã khóa tài khoản.", "Account locked.") : T("Đã mở khóa tài khoản.", "Account unlocked.");
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
            Title = T($"Chỉnh sửa tài khoản · {a.HoTen}", $"Edit account · {a.HoTen}"),
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
            return View("Accounts", await BuildAccountsVmAsync(null, ct, showForm: true, form: vm with { Error = T("Vui lòng nhập đầy đủ họ tên, tên đăng nhập, email và mật khẩu.", "Enter a full name, username, email and password.") }));
        var email = vm.Email.Trim().ToLowerInvariant();
        if (await db.TaiKhoans.AnyAsync(a => a.TenDangNhap == vm.TenDangNhap || a.Email.ToLower() == email, ct))
            return View("Accounts", await BuildAccountsVmAsync(null, ct, showForm: true, form: vm with { Error = T("Tên đăng nhập hoặc email đã tồn tại.", "The username or email is already in use.") }));
        if (!Enum.TryParse<VaiTro>(vm.VaiTro, true, out var vaiTro)) vaiTro = VaiTro.Manager;
        if (!Enum.TryParse<TrangThaiTaiKhoan>(vm.TrangThai, true, out var trangThai)) trangThai = TrangThaiTaiKhoan.HoatDong;
        var account = new TaiKhoan
        {
            TenDangNhap = vm.TenDangNhap.Trim(),
            MatKhauHash = PasswordHelper.Hash(form["matKhau"].ToString()!),
            HoTen = vm.HoTen.Trim(),
            Email = email,
            SoDienThoai = NullIfEmpty(vm.SoDienThoai),
            MaNhanSu = NullIfEmpty(vm.MaNhanSu),
            VaiTro = vaiTro,
            TrangThai = trangThai,
            NgayTao = DateTimeOffset.UtcNow
        };
        db.TaiKhoans.Add(account);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync("Tạo tài khoản", $"{account.HoTen} ({account.TenDangNhap})", "Thành công", $"Vai trò {account.VaiTro}", ct);
        TempData["msg"] = T("Đã tạo tài khoản.", "Account created.");
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
            return View("Accounts", await BuildAccountsVmAsync(vm.Id, ct, showForm: true, form: vm with { Title = T($"Chỉnh sửa tài khoản · {account.HoTen}", $"Edit account · {account.HoTen}"), SubmitHref = "/admin/tai-khoan/sua", CancelHref = $"/admin/tai-khoan?id={vm.Id}", Error = T("Vui lòng nhập họ tên và email.", "Enter a full name and email.") }));
        var email = vm.Email.Trim().ToLowerInvariant();
        if (await db.TaiKhoans.AnyAsync(a => a.Email.ToLower() == email && a.Id != vm.Id, ct))
            return View("Accounts", await BuildAccountsVmAsync(vm.Id, ct, showForm: true, form: vm with { Title = T($"Chỉnh sửa tài khoản · {account.HoTen}", $"Edit account · {account.HoTen}"), SubmitHref = "/admin/tai-khoan/sua", CancelHref = $"/admin/tai-khoan?id={vm.Id}", Error = T("Email đã được sử dụng.", "The email is already in use.") }));
        if (!Enum.TryParse<VaiTro>(vm.VaiTro, true, out var vaiTro)) vaiTro = account.VaiTro;
        if (!Enum.TryParse<TrangThaiTaiKhoan>(vm.TrangThai, true, out var trangThai)) trangThai = account.TrangThai;
        // protect last active admin from demotion/lock via edit
        var demotingLastAdmin = account.VaiTro == VaiTro.Admin && account.TrangThai == TrangThaiTaiKhoan.HoatDong && (vaiTro != VaiTro.Admin || trangThai != TrangThaiTaiKhoan.HoatDong)
            && await db.TaiKhoans.CountAsync(a => a.VaiTro == VaiTro.Admin && a.TrangThai == TrangThaiTaiKhoan.HoatDong && a.Id != account.Id, ct) <= 0;
        if (demotingLastAdmin)
            return View("Accounts", await BuildAccountsVmAsync(vm.Id, ct, showForm: true, form: vm with { Title = T($"Chỉnh sửa tài khoản · {account.HoTen}", $"Edit account · {account.HoTen}"), SubmitHref = "/admin/tai-khoan/sua", CancelHref = $"/admin/tai-khoan?id={vm.Id}", Error = T("Không thể hạ quyền hoặc khóa Admin hoạt động cuối cùng.", "The last active administrator must retain administrator access.") }));
        account.HoTen = vm.HoTen.Trim();
        account.Email = email;
        account.SoDienThoai = NullIfEmpty(vm.SoDienThoai);
        account.MaNhanSu = NullIfEmpty(vm.MaNhanSu);
        account.VaiTro = vaiTro;
        account.TrangThai = trangThai;
        var newPw = form["matKhau"].ToString();
        if (!string.IsNullOrWhiteSpace(newPw)) account.MatKhauHash = PasswordHelper.Hash(newPw);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync("Cập nhật tài khoản", $"{account.HoTen} ({account.TenDangNhap})", "Thành công", "Admin chỉnh sửa thông tin/vai trò/trạng thái", ct);
        TempData["msg"] = T("Đã cập nhật tài khoản.", "Account updated.");
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
                new AdminRoleCard("Admin", T($"{C(VaiTro.Admin)} tài khoản", $"{C(VaiTro.Admin)} accounts"), T("Toàn quyền quản trị", "Full administrator access")),
                new AdminRoleCard("Manager", T($"{C(VaiTro.Manager)} tài khoản", $"{C(VaiTro.Manager)} accounts"), T("Vận hành nghiệp vụ", "Manage operations")),
                new AdminRoleCard(T("Khách hàng", "Customer"), T($"{C(VaiTro.KhachHang)} tài khoản", $"{C(VaiTro.KhachHang)} accounts"), T("Đặt vé và tra cứu", "Book and look up flights"))
            },
            Permissions = new[]
            {
                new AdminPermissionRow(T("Tài khoản & phân quyền", "Accounts & permissions"), "X · T · S · D", "—", T("X (cá nhân)", "X (personal)")),
                new AdminPermissionRow(T("Danh mục & lịch bay", "Catalog & schedules"), "X · T · S · D", "X · T · S", "X"),
                new AdminPermissionRow(T("Đặt chỗ & vé", "Bookings & tickets"), "X · T · S · D", "X · T · S", T("X · T (của mình)", "X · T (own)")),
                new AdminPermissionRow(T("Đổi / hoàn", "Changes / refunds"), "X · T · S · D", "X · S · D", T("X · T (của mình)", "X · T (own)")),
                new AdminPermissionRow(T("Báo cáo & nhật ký", "Reports & logs"), "X", T("X (phạm vi)", "X (assigned scope)"), "—")
            },
            ControlUpdated = DateTimeOffset.UtcNow.ToOffset(Vn).ToString("dd/MM/yyyy · HH:mm")
        });
    }
>>>>>>> Stashed changes

    [HttpPost("/admin/vai-tro")]
    [ValidateAntiForgeryToken]
    public IActionResult Roles(IFormCollection form) => RedirectToAction("Index", "QuanLyTaiKhoan");

    [HttpGet("/admin/danh-muc")]
    public IActionResult Catalog(string? tab) => View(DemoCatalog.AdminCatalog(tab));

<<<<<<< Updated upstream
    [HttpPost("/admin/danh-muc")]
    [ValidateAntiForgeryToken]
    public IActionResult Catalog(IFormCollection form) => RedirectToAction(nameof(Catalog));

    [HttpGet("/admin/cau-hinh")]
    public IActionResult Settings() => View(DemoCatalog.AdminSettings());

    [HttpPost("/admin/cau-hinh")]
    [ValidateAntiForgeryToken]
    public IActionResult Settings(IFormCollection form) => RedirectToAction(nameof(Settings));
=======
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
            new AdminCatalogTab("airports", T("Sân bay", "Airports"), airports.Count.ToString(), key == "airports", "/admin/danh-muc"),
            new AdminCatalogTab("airlines", T("Hãng bay", "Airlines"), airlines.Count.ToString(), key == "airlines", "/admin/danh-muc?tab=hang-bay"),
            new AdminCatalogTab("routes", T("Tuyến bay", "Route"), routes.Count.ToString(), key == "routes", "/admin/danh-muc?tab=tuyen-bay"),
            new AdminCatalogTab("aircraft", T("Tàu bay", "Aircraft"), aircraft.Count.ToString(), key == "aircraft", "/admin/danh-muc?tab=tau-bay"),
            new AdminCatalogTab("cabin", T("Hạng ghế", "Cabin"), cabins.Count.ToString(), key == "cabin", "/admin/danh-muc?tab=hang-ghe")
        };

        bool Has(string? hay, string? needle) => string.IsNullOrWhiteSpace(needle) || (hay ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase);
        var editId = id.GetValueOrDefault();
        AdminCatalogViewModel vm;
        if (key == "airlines")
        {
            var filtered = airlines.Where(a => Has(a.MaHang, q) || Has(a.TenHang, q) || Has(a.QuocGia, q)).ToList();
            var sel = editId > 0 ? airlines.FirstOrDefault(a => a.Id == editId) : null;
            vm = new AdminCatalogViewModel { Tabs = tabs, SectionTitle = T("Hãng hàng không", "Airline"), SectionSubtitle = T($"{filtered.Count}/{airlines.Count} hãng bay", $"{filtered.Count} of {airlines.Count} airlines"), SearchLabel = T("Tìm", "Search"), SearchPlaceholder = T("Tên hoặc mã", "Name or code"), Columns = new AdminCatalogColumns(T("Mã", "Code"), T("Hãng bay / Quốc gia", "Airline / Country"), "ICAO", T("Trạng thái", "Status")),
                Rows = filtered.Select(a => new AdminCatalogRow(a.MaHang, a.TenHang, a.QuocGia, a.MaIcao ?? "—", a.DangHoatDong ? "Hoạt động" : "Tạm dừng", sel?.Id == a.Id, a.Id, EditHref(key, a.Id))).ToList(),
                ResultText = T($"Hiển thị {filtered.Count} hãng bay", $"Showing {filtered.Count} airlines"), EditorTitle = sel == null ? T("Thêm hãng bay", "Add airline") : T($"Sửa hãng bay · {sel.MaHang}", $"Edit airline · {sel.MaHang}"), EditorSubtitle = sel == null ? T("Tạo mới", "Create new") : T("Dữ liệu thực", "Live data"), EditorId = sel?.Id ?? 0, EditorTabKey = "airlines", AddText = T("Thêm hãng bay", "Add airline"),
                EditorFields = new[] { new AdminEditorField(T("Mã hãng", "Airline code"), sel?.MaHang ?? "", null, null, true, "MaHang"), new AdminEditorField(T("Tên hãng", "Airline name"), sel?.TenHang ?? "", null, null, true, "TenHang"), new AdminEditorField(T("Quốc gia", "Country"), sel?.QuocGia ?? "", null, null, false, "QuocGia"), new AdminEditorField("ICAO", sel?.MaIcao ?? "", null, null, false, "MaIcao"), new AdminEditorField(T("Trạng thái", "Status"), (sel?.DangHoatDong ?? true) ? "Hoạt động" : "Tạm dừng", null, null, false, "TrangThai", new[] { "Hoạt động", "Tạm dừng" }) } };
        }
        else if (key == "routes")
        {
            var filtered = routes.Where(r => Has(r.SanBayDi.MaSanBay + r.SanBayDen.MaSanBay, q) || Has(r.SanBayDi.ThanhPho, q)).ToList();
            var sel = editId > 0 ? routes.FirstOrDefault(r => r.Id == editId) : null;
            var apOpts = airports.Select(a => $"{a.Id}|{a.MaSanBay} - {a.TenSanBay}").ToList();
            string ApVal(int? aid) { var a = airports.FirstOrDefault(x => x.Id == aid); return a == null ? (apOpts.FirstOrDefault() ?? "") : $"{a.Id}|{a.MaSanBay} - {a.TenSanBay}"; }
            vm = new AdminCatalogViewModel { Tabs = tabs, SectionTitle = T("Tuyến bay", "Route"), SectionSubtitle = T($"{filtered.Count}/{routes.Count} tuyến bay", $"{filtered.Count} of {routes.Count} routes"), SearchLabel = T("Tìm", "Search"), SearchPlaceholder = T("Mã tuyến", "Route code"), Columns = new AdminCatalogColumns(T("Mã tuyến", "Route code"), T("Tuyến bay", "Route"), T("Thành phố", "City"), T("Trạng thái", "Status")),
                Rows = filtered.Select(r => new AdminCatalogRow($"{r.SanBayDi.MaSanBay}-{r.SanBayDen.MaSanBay}", $"{r.SanBayDi.MaSanBay} → {r.SanBayDen.MaSanBay}", $"{r.SanBayDi.ThanhPho} → {r.SanBayDen.ThanhPho}", r.DangKhaiThac ? "Đang khai thác" : "Ngừng", r.DangKhaiThac ? "Đang khai thác" : "Ngừng", sel?.Id == r.Id, r.Id, EditHref(key, r.Id))).ToList(),
                ResultText = T($"Hiển thị {filtered.Count} tuyến bay", $"Showing {filtered.Count} routes"), EditorTitle = sel == null ? T("Thêm tuyến bay", "Add route") : T($"Sửa tuyến bay · {sel.SanBayDi.MaSanBay}-{sel.SanBayDen.MaSanBay}", $"Edit route · {sel.SanBayDi.MaSanBay}-{sel.SanBayDen.MaSanBay}"), EditorSubtitle = sel == null ? T("Tạo mới", "Create new") : T("Dữ liệu thực", "Live data"), EditorId = sel?.Id ?? 0, EditorTabKey = "routes", AddText = T("Thêm tuyến bay", "Add route"),
                EditorFields = new[] { new AdminEditorField(T("Sân bay đi", "Origin airport"), ApVal(sel?.SanBayDiId), null, null, true, "SanBayDiId", apOpts), new AdminEditorField(T("Sân bay đến", "Destination airport"), ApVal(sel?.SanBayDenId), null, null, true, "SanBayDenId", apOpts), new AdminEditorField(T("Thời lượng dự kiến (phút)", "Scheduled duration (minutes)"), sel?.ThoiLuongDuKienPhut?.ToString() ?? "", null, null, false, "ThoiLuongDuKienPhut"), new AdminEditorField(T("Trạng thái", "Status"), (sel?.DangKhaiThac ?? true) ? "Đang khai thác" : "Ngừng", null, null, false, "TrangThai", new[] { "Đang khai thác", "Ngừng" }) } };
        }
        else if (key == "aircraft")
        {
            var filtered = aircraft.Where(a => Has(a.MaMayBay, q) || Has(a.TenMayBay, q) || Has(a.HangSanXuat, q)).ToList();
            var sel = editId > 0 ? aircraft.FirstOrDefault(a => a.Id == editId) : null;
            vm = new AdminCatalogViewModel { Tabs = tabs, SectionTitle = T("Đội tàu bay", "Aircraft fleet"), SectionSubtitle = T($"{filtered.Count}/{aircraft.Count} tàu bay", $"{filtered.Count} of {aircraft.Count} aircraft"), SearchLabel = T("Tìm", "Search"), SearchPlaceholder = T("Mã hoặc dòng tàu", "Code or model"), Columns = new AdminCatalogColumns(T("Mã tàu", "Aircraft code"), T("Tàu bay", "Aircraft"), T("Số ghế", "Seats"), T("Trạng thái", "Status")),
                Rows = filtered.Select(a => new AdminCatalogRow(a.MaMayBay, a.TenMayBay, a.HangSanXuat, T($"{a.SoGhe} ghế", $"{a.SoGhe} seats"), a.DangHoatDong ? "Hoạt động" : "Bảo trì", sel?.Id == a.Id, a.Id, EditHref(key, a.Id))).ToList(),
                ResultText = T($"Hiển thị {filtered.Count} tàu bay", $"Showing {filtered.Count} aircraft"), EditorTitle = sel == null ? T("Thêm tàu bay", "Add aircraft") : T($"Sửa tàu bay · {sel.MaMayBay}", $"Edit aircraft · {sel.MaMayBay}"), EditorSubtitle = sel == null ? T("Tạo mới", "Create new") : T("Dữ liệu thực", "Live data"), EditorId = sel?.Id ?? 0, EditorTabKey = "aircraft", AddText = T("Thêm tàu bay", "Add aircraft"),
                EditorFields = new[] { new AdminEditorField(T("Mã tàu bay", "Aircraft code"), sel?.MaMayBay ?? "", null, null, true, "MaMayBay"), new AdminEditorField(T("Tên tàu bay", "Aircraft name"), sel?.TenMayBay ?? "", null, null, true, "TenMayBay"), new AdminEditorField(T("Hãng sản xuất", "Manufacturer"), sel?.HangSanXuat ?? "", null, null, false, "HangSanXuat"), new AdminEditorField(T("Số ghế", "Seats"), sel?.SoGhe.ToString() ?? "", null, null, true, "SoGhe"), new AdminEditorField(T("Năm khai thác", "Entry into service year"), sel?.NamKhaiThac?.ToString() ?? "", null, null, false, "NamKhaiThac"), new AdminEditorField(T("Trạng thái", "Status"), (sel?.DangHoatDong ?? true) ? "Hoạt động" : "Bảo trì", null, null, false, "TrangThai", new[] { "Hoạt động", "Bảo trì" }) } };
        }
        else if (key == "cabin")
        {
            var filtered = cabins.Where(c => Has(c.MaHang, q) || Has(c.TenHang, q)).ToList();
            vm = new AdminCatalogViewModel { Tabs = tabs, SectionTitle = T("Hạng ghế", "Cabin"), SectionSubtitle = T($"{filtered.Count} nhóm hạng ghế (từ dữ liệu chuyến bay)", $"{filtered.Count} cabin groups from flight data"), SearchLabel = T("Tìm", "Search"), SearchPlaceholder = T("Mã hoặc tên hạng", "Cabin code or name"), Columns = new AdminCatalogColumns(T("Mã hạng", "Cabin code"), T("Hạng ghế / Nhóm", "Cabin / Group"), T("Hành lý", "Baggage"), T("Trạng thái", "Status")),
                Rows = filtered.Select(c => new AdminCatalogRow(c.MaHang, c.TenHang, c.NhomHang, T($"{c.HanhLyKyGuiKg} kg ký gửi", $"{c.HanhLyKyGuiKg} kg checked baggage"), T($"Áp dụng {c.Count} chuyến", $"Applies to {c.Count} flights"), false)).ToList(),
                ResultText = T($"Hiển thị {filtered.Count} nhóm hạng ghế", $"Showing {filtered.Count} cabin groups"), EditorTitle = T("Hạng ghế", "Cabin"), EditorSubtitle = T("Hạng ghế được quản lý theo chuyến bay (dữ liệu đọc từ HangVe).", "Cabins are managed per flight."), EditorFields = Array.Empty<AdminEditorField>(), AddText = T("Thêm hạng ghế", "Add cabin") };
        }
        else
        {
            var filtered = airports.Where(a => Has(a.MaSanBay, q) || Has(a.TenSanBay, q) || Has(a.ThanhPho, q)).ToList();
            var sel = editId > 0 ? airports.FirstOrDefault(a => a.Id == editId) : null;
            vm = new AdminCatalogViewModel { Tabs = tabs, SectionTitle = T("Sân bay", "Airports"), SectionSubtitle = T($"{filtered.Count}/{airports.Count} sân bay", $"{filtered.Count} of {airports.Count} airports"), SearchLabel = T("Tìm", "Search"), SearchPlaceholder = T("Tên hoặc IATA", "Name or IATA code"), Columns = new AdminCatalogColumns("IATA", T("Sân bay / Thành phố", "Airport / City"), T("Quốc gia", "Country"), T("Trạng thái", "Status")),
                Rows = filtered.Select(a => new AdminCatalogRow(a.MaSanBay, a.TenSanBay, a.ThanhPho, a.QuocGia, a.DangHoatDong ? "Hoạt động" : "Tạm dừng", sel?.Id == a.Id, a.Id, EditHref(key, a.Id))).ToList(),
                ResultText = T($"Hiển thị {filtered.Count} sân bay", $"Showing {filtered.Count} airports"), EditorTitle = sel == null ? T("Thêm sân bay", "Add airport") : T($"Sửa sân bay · {sel.MaSanBay}", $"Edit airport · {sel.MaSanBay}"), EditorSubtitle = sel == null ? T("Tạo mới", "Create new") : T("Dữ liệu thực", "Live data"), EditorId = sel?.Id ?? 0, EditorTabKey = "airports", AddText = T("Thêm sân bay", "Add airport"),
                EditorFields = new[] { new AdminEditorField(T("Mã IATA", "IATA code"), sel?.MaSanBay ?? "", null, null, true, "MaSanBay"), new AdminEditorField(T("Tên sân bay", "Airport name"), sel?.TenSanBay ?? "", null, null, true, "TenSanBay"), new AdminEditorField(T("Thành phố", "City"), sel?.ThanhPho ?? "", null, null, false, "ThanhPho"), new AdminEditorField(T("Quốc gia", "Country"), sel?.QuocGia ?? "", null, null, false, "QuocGia"), new AdminEditorField(T("Múi giờ", "Time zone"), sel?.MuiGio ?? "UTC+7", null, null, false, "MuiGio"), new AdminEditorField(T("Trạng thái", "Status"), (sel?.DangHoatDong ?? true) ? "Hoạt động" : "Tạm dừng", null, null, false, "TrangThai", new[] { "Hoạt động", "Tạm dừng" }) } };
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
        var hotline = Get("contact.hotline", ChinhSachDatVe.DefaultHotline);
        var email = Get("contact.email", ChinhSachDatVe.DefaultSupportEmail);
        var sender = Get("notification.sender_email", Get("contact.sender_email", "no-reply@aeroviet.vn"));
        return View(new AdminSettingsViewModel
        {
            VersionText = T("Dữ liệu thực từ CauHinhHeThong", "Live system settings"),
            DirtyText = T($"{list.Count} cấu hình", $"{list.Count} settings"),
            UpdatedText = $"Cập nhật: {list.OrderByDescending(c => c.NgayCapNhat).FirstOrDefault()?.NgayCapNhat.ToOffset(Vn).ToString("dd/MM/yyyy HH:mm") ?? "—"}",
            BookingFields = new[]
            {
                new AdminSettingField(T("Thời gian giữ chỗ (phút)", "Hold duration (minutes)"), hold + T(" phút", " min"), T("Hết hạn tự hủy đơn chưa thanh toán.", "Unpaid bookings are cancelled when the hold expires."), Name: "booking.hold_minutes", FormValue: hold),
                new AdminSettingField(T("Phí dịch vụ (VNĐ)", "Service fee (VND)"), Money.Vnd(ParseDec(serviceFee, 70000m)), null, Name: "booking.service_fee", FormValue: serviceFee),
                new AdminSettingField(T("Thuế & phí mặc định (VNĐ)", "Default taxes & fees (VND)"), Money.Vnd(ParseDec(tax, 380000m)), null, Name: "booking.tax_default", FormValue: tax)
            },
            PolicyFields = new[]
            {
                new AdminSettingField(T("Phí đổi (VNĐ)", "Change fee (VND)"), Money.Vnd(ParseDec(changeFee, 360000m)) + T(" + chênh lệch", " + fare difference"), null, Name: "policy.change_fee", FormValue: changeFee),
                new AdminSettingField(T("Phí hoàn (VNĐ)", "Refund fee (VND)"), Money.Vnd(ParseDec(refundFee, 500000m)), null, Name: "policy.refund_fee", FormValue: refundFee),
                new AdminSettingField("Hotline", hotline, null, Name: "contact.hotline", FormValue: hotline)
            },
            NotificationFields = new[] { new AdminSettingField(T("Email hỗ trợ", "Support email"), email, null, Name: "contact.email", FormValue: email) },
            PolicyChecks = list.Select(c => $"{c.Khoa} = {c.GiaTri}").Take(8).ToList(),
            EmailSender = new AdminSettingField(T("Email gửi hệ thống", "System sender email"), sender, null, Name: "notification.sender_email", FormValue: sender)
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
            TempData["msg"] = T($"Đã lưu {changed} cấu hình nghiệp vụ.", $"Saved {changed} business settings.");
            TempData["tone"] = "success";
        }
        else
        {
            TempData["msg"] = T("Không có thay đổi cấu hình.", "No setting changes.");
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
>>>>>>> Stashed changes

    [HttpGet("/admin/nhat-ky")]
    public IActionResult Audit() => View(DemoCatalog.AdminAudit());

    [HttpPost("/admin/nhat-ky")]
    [ValidateAntiForgeryToken]
    public IActionResult Audit(IFormCollection form) => RedirectToAction(nameof(Audit));
<<<<<<< Updated upstream
=======

    private async Task WriteAuditAsync(string hanhDong, string doiTuong, string ketQuaText, string? chiTiet, CancellationToken ct)
    {
        var ketQua = ketQuaText.Contains("Thất bại", StringComparison.OrdinalIgnoreCase) || ketQuaText.Contains("Lỗi", StringComparison.OrdinalIgnoreCase)
            ? KetQuaNhatKy.ThatBai : KetQuaNhatKy.ThanhCong;
        db.NhatKyHoatDongs.Add(new NhatKyHoatDong
        {
            MaNhatKy = NhatKyHoatDong.TaoMa(),
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
>>>>>>> Stashed changes
}
