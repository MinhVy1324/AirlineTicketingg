using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Controllers;

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
public class ManagerController(AppDbContext db) : Controller
{
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);

    [HttpGet("/quan-ly")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var vnToday = now.ToOffset(Vn).Date;
        var todayStartVn = new DateTimeOffset(vnToday, Vn);
        var todayStart = todayStartVn.ToUniversalTime();
        var todayEnd = todayStartVn.AddDays(1).ToUniversalTime();

        var todayFlights = await db.ChuyenBays.AsNoTracking().Include(c => c.TuyenBay).ThenInclude(t => t.SanBayDi)
            .Include(c => c.TuyenBay).ThenInclude(t => t.SanBayDen)
            .Where(c => c.GioKhoiHanh >= todayStart && c.GioKhoiHanh < todayEnd)
            .OrderBy(c => c.GioKhoiHanh).Take(8).ToListAsync(ct);
        var totalToday = await db.ChuyenBays.CountAsync(c => c.GioKhoiHanh >= todayStart && c.GioKhoiHanh < todayEnd, ct);
        var doneToday = await db.ChuyenBays.CountAsync(c => c.GioKhoiHanh >= todayStart && c.GioKhoiHanh < todayEnd && c.TrangThai == TrangThaiChuyenBay.HoanTat, ct);
        var pendingPay = await db.DatVes.CountAsync(d => d.TrangThai == TrangThaiDatVe.ChoThanhToan, ct);
        var reqCount = await db.YeuCauDoiHoans.CountAsync(r => r.TrangThai == TrangThaiYeuCau.ChoDuyet || r.TrangThai == TrangThaiYeuCau.DaDuyet, ct);
        var reqChange = await db.YeuCauDoiHoans.CountAsync(r => r.LoaiYeuCau == LoaiYeuCau.DoiVe && (r.TrangThai == TrangThaiYeuCau.ChoDuyet || r.TrangThai == TrangThaiYeuCau.DaDuyet), ct);
        var reqRefund = await db.YeuCauDoiHoans.CountAsync(r => r.LoaiYeuCau == LoaiYeuCau.HoanVe && (r.TrangThai == TrangThaiYeuCau.ChoDuyet || r.TrangThai == TrangThaiYeuCau.DaDuyet), ct);

        var sold = await db.Ves.CountAsync(v => v.TrangThai == TrangThaiVe.DaPhatHanh || v.TrangThai == TrangThaiVe.TamGiu, ct);
        var cap = await db.HangVes.SumAsync(h => (int?)h.SoGhe, ct) ?? 1;
        var fill = cap <= 0 ? 0m : Math.Round((decimal)sold / cap * 100m, 1);

        var holds = await db.DatVes.AsNoTracking().Include(d => d.KhachHang)
            .Where(d => d.TrangThai == TrangThaiDatVe.ChoThanhToan)
            .OrderBy(d => d.HanGiuCho).Take(3).ToListAsync(ct);

        var vm = new ManagerOverviewViewModel
        {
            Subtitle = $"{vnToday:dd/MM/yyyy} · Theo dõi chuyến bay, đặt chỗ và yêu cầu (dữ liệu thực).",
            UpdatedText = $"Cập nhật lúc {now.ToOffset(Vn):HH:mm} · Giờ Việt Nam (UTC+7)",
            Kpis = new[]
            {
                new ManagerKpi("Chuyến bay hôm nay", totalToday.ToString(), $"{doneToday} hoàn tất · {Math.Max(0,totalToday-doneToday)} chưa khởi hành", "info", "plane"),
                new ManagerKpi("Đặt chỗ chờ thanh toán", pendingPay.ToString(), "Cần theo dõi hạn giữ chỗ", "warning", "clock"),
                new ManagerKpi("Tỷ lệ lấp đầy (ước tính)", fill + "%", $"{sold:N0} vé / {cap:N0} ghế hạng vé", "success", "users"),
                new ManagerKpi("Yêu cầu cần xử lý", reqCount.ToString(), $"{reqChange} đổi vé · {reqRefund} hoàn vé", "warning", "inbox")
            },
            AttentionTitle = "Theo dõi vận hành",
            AttentionText = "Dữ liệu hiển thị từ cơ sở dữ liệu. Kiểm tra chuyến đổi giờ và đơn chờ thanh toán.",
            Flights = todayFlights.Select(c => new ManagerFlightRow(
                $"{c.SoHieu} · {c.TuyenBay.SanBayDi.MaSanBay} → {c.TuyenBay.SanBayDen.MaSanBay}",
                $"{c.GioKhoiHanh.ToOffset(Vn):HH:mm}–{c.GioHaCanh.ToOffset(Vn):HH:mm}",
                Money.Vnd(c.GiaCoBan),
                c.TrangThai.ToString(), c.TrangThai == TrangThaiChuyenBay.BiHuy ? "warning" : "success")).ToList(),
            Holds = holds.Select(h => new ManagerHoldRow(h.MaDatVe, h.KhachHang?.HoTen ?? "Khách", Money.Vnd(h.TongTien), h.HanGiuCho?.ToOffset(Vn).ToString("HH:mm dd/MM") ?? "—")).ToList(),
            Workload = new[]
            {
                new ManagerWorkloadRow("Đổi vé chờ xử lý", $"{reqChange} yêu cầu", Math.Min(100, reqChange*10), "warning"),
                new ManagerWorkloadRow("Hoàn vé chờ xử lý", $"{reqRefund} yêu cầu", Math.Min(100, reqRefund*10), "success")
            },
            Checklist = new[]
            {
                new ManagerChecklistItem("Dữ liệu đồng bộ từ cơ sở dữ liệu", true),
                new ManagerChecklistItem("Kiểm tra lịch khai thác", true),
                new ManagerChecklistItem("Xử lý yêu cầu đổi / hoàn", reqCount == 0),
                new ManagerChecklistItem("Theo dõi đơn chờ thanh toán", pendingPay == 0)
            }
        };
        return View(vm);
    }

    [HttpGet("/quan-ly/lich-bay")]
    public async Task<IActionResult> Schedules([FromQuery(Name = "hop-thoai")] string? hopThoai, int? id, [FromQuery(Name = "chon")] int? chon, CancellationToken ct)
    {
        var selectedId = id ?? chon;
        var flights = await db.ChuyenBays.AsNoTracking()
            .Include(c => c.TuyenBay).ThenInclude(t => t.SanBayDi)
            .Include(c => c.TuyenBay).ThenInclude(t => t.SanBayDen)
            .Include(c => c.HangVes)
            .OrderBy(c => c.GioKhoiHanh).Take(120).ToListAsync(ct);

        var selected = selectedId.HasValue ? flights.FirstOrDefault(f => f.Id == selectedId.Value) : flights.FirstOrDefault();
        var rows = flights.Select(f => new ManagerScheduleRow(
            $"{f.SoHieu}", $"{f.TuyenBay.SanBayDi.MaSanBay} → {f.TuyenBay.SanBayDen.MaSanBay}",
            $"{f.GioKhoiHanh.ToOffset(Vn):dd/MM HH:mm}–{f.GioHaCanh.ToOffset(Vn):HH:mm}",
            f.TrangThai.ToString(), f.TrangThai == TrangThaiChuyenBay.BiHuy ? "warning" : "success",
            f.HangVes.Sum(h => h.SoGhe).ToString(), Money.Vnd(f.GiaCoBan),
            selected != null && f.Id == selected.Id, f.Id, $"/quan-ly/lich-bay?id={f.Id}")).ToList();

        var fares = selected?.HangVes.Select(h => new ManagerFareRow(h.NhomHang, h.SoGhe.ToString(), h.SoGheDaBan.ToString(), Money.Vnd(h.GiaCoBan))).ToList()
            ?? new List<ManagerFareRow> { new("Phổ thông", "—", "—", "—") };

        return View(new ManagerSchedulesViewModel
        {
            Rows = rows,
            ResultText = $"Hiển thị {rows.Count} chuyến (dữ liệu thực)",
            SelectedFlightId = selected?.Id ?? 0,
            EditorTitle = selected == null ? "Chọn chuyến bay" : $"Chuyến {selected.SoHieu}",
            EditorSubtitle = selected == null ? "—" : $"{selected.TuyenBay.SanBayDi.MaSanBay} → {selected.TuyenBay.SanBayDen.MaSanBay} · {selected.GioKhoiHanh.ToOffset(Vn):dd/MM/yyyy HH:mm}",
            EditorFlightNumber = selected?.SoHieu ?? "",
            EditorDateValue = selected?.GioKhoiHanh.ToOffset(Vn).ToString("dd/MM/yyyy") ?? "—",
            EditorTimeValue = selected == null ? "—" : $"{selected.GioKhoiHanh.ToOffset(Vn):HH:mm}/{selected.GioHaCanh.ToOffset(Vn):HH:mm}",
            EditorStatusValue = selected?.TrangThai.ToString() ?? "—",
            EconomyPriceValue = selected == null ? "—" : Money.Vnd(selected.GiaCoBan),
            BusinessPriceValue = selected == null ? "—" : Money.Vnd(selected.GiaCoBan + 800000m),
            Fares = fares,
            EditorNote = selected == null ? "Chọn một chuyến trong danh sách để xem chi tiết hạng vé." : $"Hạng vé thực tế: {fares.Count}. Giá hiển thị từ cơ sở dữ liệu.",
            ShowCancelDialog = string.Equals(hopThoai, "huy-chuyen", StringComparison.OrdinalIgnoreCase),
            ShowPriceDialog = string.Equals(hopThoai, "doi-gia", StringComparison.OrdinalIgnoreCase)
        });
    }

    [HttpPost("/quan-ly/lich-bay")][ValidateAntiForgeryToken]
    public IActionResult Schedules(IFormCollection form) => RedirectToAction(nameof(Schedules));
    [HttpPost("/quan-ly/lich-bay/huy-chuyen")][ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelFlight(IFormCollection form, CancellationToken ct)
    {
        var id = ParseId(form["chuyen-bay-id"]);
        var reason = (form["ly-do"].ToString() ?? "").Trim();
        var flight = id.HasValue ? await db.ChuyenBays.FirstOrDefaultAsync(c => c.Id == id.Value, ct) : null;
        if (flight != null && flight.TrangThai != TrangThaiChuyenBay.HoanTat)
        {
            flight.TrangThai = TrangThaiChuyenBay.BiHuy;
            await db.SaveChangesAsync(ct);
            await WriteAuditAsync("Hủy chuyến bay", $"{flight.SoHieu} ({flight.Id})", "Đã hủy", reason, ct);
        }
        return RedirectToAction(nameof(Schedules), new { id = id });
    }

    [HttpPost("/quan-ly/lich-bay/doi-gia")][ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePrice(IFormCollection form, CancellationToken ct)
    {
        var id = ParseId(form["chuyen-bay-id"]);
        var reason = (form["ly-do"].ToString() ?? "").Trim();
        if (!decimal.TryParse((form["gia-moi"].ToString() ?? "").Replace(".", "").Replace(",", ""), out var newPrice) || newPrice < 0)
            return RedirectToAction(nameof(Schedules), new { id });

        var flight = id.HasValue ? await db.ChuyenBays.Include(c => c.HangVes).FirstOrDefaultAsync(c => c.Id == id.Value, ct) : null;
        if (flight != null)
        {
            var old = flight.GiaCoBan;
            flight.GiaCoBan = newPrice;
            foreach (var hv in flight.HangVes.Where(h => h.NhomHang.Contains("Phổ thông", StringComparison.OrdinalIgnoreCase) || h.NhomHang.Contains("Pho thong", StringComparison.OrdinalIgnoreCase) || h.MaHang.StartsWith("ECO", StringComparison.OrdinalIgnoreCase)))
                hv.GiaCoBan = newPrice;
            if (!flight.HangVes.Any()) { /* only base price updated */ }
            await db.SaveChangesAsync(ct);
            await WriteAuditAsync("Đổi giá chuyến bay", $"{flight.SoHieu} ({flight.Id})", $"{Money.Vnd(old)} → {Money.Vnd(newPrice)}", reason, ct);
        }
        return RedirectToAction(nameof(Schedules), new { id });
    }

    [HttpGet("/quan-ly/dat-ve")]
    public async Task<IActionResult> Bookings(string? q, CancellationToken ct)
    {
        IQueryable<DatVe> query = db.DatVes.AsNoTracking().Include(d => d.KhachHang)
            .Include(d => d.Ves).ThenInclude(v => v.ChuyenBay).ThenInclude(c => c.TuyenBay)
            .OrderByDescending(d => d.NgayDat);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToUpper();
            query = query.Where(d => d.MaDatVe.ToUpper().Contains(term) || d.KhachHang.HoTen.ToUpper().Contains(term)).OrderByDescending(d => d.NgayDat);
        }
        var list = await query.Take(80).ToListAsync(ct);
        var rows = list.Select(d =>
        {
            var ve = d.Ves.FirstOrDefault();
            var flight = ve?.ChuyenBay == null ? "—" : $"{ve.ChuyenBay.SoHieu} · {ve.ChuyenBay.GioKhoiHanh.ToOffset(Vn):dd/MM/yyyy}";
            var (hs, ht) = HoldStatus(d);
            var (ps, pt) = PaymentStatus(d);
            var (ts, tt) = TicketStatus(d);
            return new ManagerBookingRow(d.MaDatVe, d.KhachHang?.HoTen ?? "—", flight, hs, ht, ps, pt, ts, tt, Money.Vnd(d.TongTien), false);
        }).ToList();

        var pending = list.Count(d => d.TrangThai == TrangThaiDatVe.ChoThanhToan);
        return View(new ManagerBookingsViewModel
        {
            Rows = rows,
            ResultText = $"Hiển thị {rows.Count} đặt chỗ (dữ liệu thực)",
            ListMeta = $"{pending} đơn chờ thanh toán",
            SearchPlaceholder = "AVxxxx hoặc tên khách hàng"
        });
    }

    [HttpPost("/quan-ly/dat-ve")][ValidateAntiForgeryToken]
    public IActionResult Bookings(IFormCollection form) => RedirectToAction(nameof(Bookings));

    [HttpGet("/quan-ly/doi-hoan")]
    public async Task<IActionResult> ChangeRefunds(string? tab, int? id, [FromQuery(Name = "hop-thoai")] string? hopThoai, CancellationToken ct)
    {
        var active = (tab ?? "all").Trim().ToLowerInvariant();
        IQueryable<YeuCauDoiHoan> q = db.YeuCauDoiHoans.AsNoTracking()
            .Include(r => r.DatVe).ThenInclude(d => d.KhachHang)
            .Include(r => r.DatVe).ThenInclude(d => d.Ves).ThenInclude(v => v.ChuyenBay).ThenInclude(c => c.TuyenBay)
            .OrderByDescending(r => r.NgayTao);
        q = active switch
        {
            "cho-duyet" => q.Where(r => r.TrangThai == TrangThaiYeuCau.ChoDuyet),
            "da-duyet" => q.Where(r => r.TrangThai == TrangThaiYeuCau.DaDuyet),
            "hoan-tat" => q.Where(r => r.TrangThai == TrangThaiYeuCau.HoanTat || r.TrangThai == TrangThaiYeuCau.TuChoi),
            _ => q
        };
        var list = await q.Take(80).ToListAsync(ct);
        var selected = id.HasValue ? list.FirstOrDefault(r => r.Id == id.Value) : list.FirstOrDefault();
        ViewData["SelectedRequestId"] = selected?.Id ?? 0;

        var rows = list.Select(r => new ManagerRequestRow(
            r.MaYeuCau,
            r.DatVe?.KhachHang?.HoTen ?? "—",
            r.LoaiYeuCau == LoaiYeuCau.HoanVe ? "Hoàn vé" : "Đổi vé",
            r.NgayTao.ToOffset(Vn).ToString("dd/MM/yyyy HH:mm"),
            StatusLabel(r.TrangThai), ReqTone(r.TrangThai),
            selected != null && r.Id == selected.Id, r.Id,
            $"/quan-ly/doi-hoan?tab={active}&id={r.Id}")).ToList();

        var total = await db.YeuCauDoiHoans.CountAsync(ct);
        var pending = await db.YeuCauDoiHoans.CountAsync(r => r.TrangThai == TrangThaiYeuCau.ChoDuyet, ct);
        var approved = await db.YeuCauDoiHoans.CountAsync(r => r.TrangThai == TrangThaiYeuCau.DaDuyet, ct);
        var done = await db.YeuCauDoiHoans.CountAsync(r => r.TrangThai == TrangThaiYeuCau.HoanTat || r.TrangThai == TrangThaiYeuCau.TuChoi, ct);

        var ve = selected?.DatVe?.Ves.FirstOrDefault();
        var flight = ve?.ChuyenBay;
        var paid = selected?.DatVe?.TongTien ?? 0m;
        var refundFee = selected?.PhiHoan ?? 0m;
        var serviceFee = (selected?.PhiDichVuKhongHoan ?? 0m);
        var baggageFee = (selected?.PhiHanhLyKhongHoan ?? 0m);
        var refundTotal = selected?.SoTienHoan > 0 ? selected.SoTienHoan : Math.Max(0m, paid - refundFee - serviceFee - baggageFee);

        var detail = selected == null
            ? new ManagerChangeRefundsViewModel()
            : new ManagerChangeRefundsViewModel
            {
                DetailTitle = $"Chi tiết yêu cầu · {selected.MaYeuCau}",
                DetailSubtitle = $"{selected.DatVe?.KhachHang?.HoTen ?? "—"} · {selected.DatVe?.MaDatVe ?? "—"}",
                DetailFields = new[]
                {
                    new AdminDetailField("Loại yêu cầu", selected.LoaiYeuCau == LoaiYeuCau.HoanVe ? "Hoàn vé" : "Đổi vé"),
                    new AdminDetailField("Trạng thái", StatusLabel(selected.TrangThai)),
                    new AdminDetailField("Chuyến bay", flight == null ? "—" : $"{flight.SoHieu} · {flight.GioKhoiHanh.ToOffset(Vn):dd/MM/yyyy HH:mm}"),
                    new AdminDetailField("Lý do", string.IsNullOrWhiteSpace(selected.LyDo) ? "—" : selected.LyDo),
                    new AdminDetailField("Thông tin bổ sung", string.IsNullOrWhiteSpace(selected.ThongTinBoSung) ? "—" : selected.ThongTinBoSung)
                },
                EligibleTitle = selected.TrangThai == TrangThaiYeuCau.ChoDuyet ? "Yêu cầu đang chờ duyệt" : "Yêu cầu đã được xử lý",
                EligibleText = selected.GhiChuXuLy ?? "Kiểm tra điều kiện vé và mức phí trước khi phê duyệt.",
                ConditionText = selected.LoaiYeuCau == LoaiYeuCau.HoanVe
                    ? $"Phí hoàn dự kiến {Money.Vnd(refundFee)}; phí không hoàn {Money.Vnd(serviceFee + baggageFee)}."
                    : $"Phí đổi dự kiến {Money.Vnd(selected.PhiDoi)}; chênh lệch giá xác nhận khi xử lý.",
                ReasonValue = selected.GhiChuXuLy ?? string.Empty,
                RefundTitle = "Tính hoàn/chi phí dự kiến",
                RefundRows = new[]
                {
                    new ManagerRefundRow("Tổng đã thanh toán", Money.Vnd(paid)),
                    new ManagerRefundRow(selected.LoaiYeuCau == LoaiYeuCau.HoanVe ? "Phí hoàn vé" : "Phí đổi vé", "−" + Money.Vnd(selected.LoaiYeuCau == LoaiYeuCau.HoanVe ? refundFee : selected.PhiDoi)),
                    new ManagerRefundRow("Phí dịch vụ không hoàn", "−" + Money.Vnd(serviceFee)),
                    new ManagerRefundRow("Hành lý thêm không hoàn", "−" + Money.Vnd(baggageFee)),
                    new ManagerRefundRow(selected.LoaiYeuCau == LoaiYeuCau.HoanVe ? "Dự kiến hoàn" : "Cần xác nhận thêm", Money.Vnd(refundTotal), true)
                },
                RefundMethodValue = selected.PhuongThucHoan ?? "Theo nguồn thanh toán",
                RefundTimeValue = "7–15 ngày làm việc (sau xác nhận)",
                ApproveDialog = new ManagerApproveRefundDialogViewModel
                {
                    Title = selected.LoaiYeuCau == LoaiYeuCau.HoanVe ? "Duyệt và tạo lệnh hoàn vé?" : "Duyệt yêu cầu đổi vé?",
                    Subtitle = $"{selected.MaYeuCau} · {selected.DatVe?.MaDatVe ?? ""} · {StatusLabel(selected.TrangThai)}",
                    FlightValue = flight == null ? "—" : $"{flight.SoHieu} · {flight.GioKhoiHanh.ToOffset(Vn):dd/MM/yyyy HH:mm}",
                    TicketValue = ve?.MaVe ?? "—",
                    EligibleText = "Kiểm tra điều kiện vé, phí và trạng thái thanh toán trước khi xác nhận.",
                    TotalValue = Money.Vnd(paid),
                    FeeValue = "−" + Money.Vnd(selected.LoaiYeuCau == LoaiYeuCau.HoanVe ? refundFee : selected.PhiDoi),
                    ServiceValue = "−" + Money.Vnd(serviceFee),
                    BaggageValue = "−" + Money.Vnd(baggageFee),
                    AmountValue = Money.Vnd(refundTotal),
                    MethodValue = selected.PhuongThucHoan ?? "Nguồn thanh toán gốc",
                    ReasonValue = selected.GhiChuXuLy ?? "",
                    NoteText = selected.LoaiYeuCau == LoaiYeuCau.HoanVe
                        ? "Xác nhận sẽ chuyển yêu cầu sang Đã duyệt. Chỉ ghi nhận hoàn tất khi cổng thanh toán xác nhận."
                        : "Xác nhận sẽ chuyển yêu cầu đổi vé sang Đã duyệt để xử lý bước tiếp theo.",
                    SubmitText = selected.LoaiYeuCau == LoaiYeuCau.HoanVe ? "Duyệt yêu cầu hoàn" : "Duyệt yêu cầu đổi",
                    ConfirmCheckText = $"Tôi đã kiểm tra thông tin yêu cầu {selected.MaYeuCau}.",
                    CancelHref = $"/quan-ly/doi-hoan?tab={active}&id={selected.Id}"
                }
            };

        return View(new ManagerChangeRefundsViewModel
        {
            Tabs = detail.Tabs.Count > 0 ? detail.Tabs : new[]
            {
                new ManagerQueueTab($"Tất cả ({total})", "/quan-ly/doi-hoan", active == "all"),
                new ManagerQueueTab($"Chờ duyệt ({pending})", "/quan-ly/doi-hoan?tab=cho-duyet", active == "cho-duyet"),
                new ManagerQueueTab($"Đã duyệt ({approved})", "/quan-ly/doi-hoan?tab=da-duyet", active == "da-duyet"),
                new ManagerQueueTab($"Hoàn tất ({done})", "/quan-ly/doi-hoan?tab=hoan-tat", active == "hoan-tat")
            },
            QueueTitle = detail.QueueTitle,
            QueueSubtitle = $"Chờ duyệt: {pending} · Đã duyệt: {approved} · Hoàn tất/Từ chối: {done}",
            Rows = rows,
            ResultText = $"Hiển thị {rows.Count} yêu cầu (dữ liệu thực)",
            DetailTitle = detail.DetailTitle,
            DetailSubtitle = detail.DetailSubtitle,
            DetailFields = detail.DetailFields,
            EligibleTitle = detail.EligibleTitle,
            EligibleText = detail.EligibleText,
            ConditionText = detail.ConditionText,
            ReasonValue = detail.ReasonValue,
            RefundTitle = detail.RefundTitle,
            RefundRows = detail.RefundRows,
            RefundMethodValue = detail.RefundMethodValue,
            RefundTimeValue = detail.RefundTimeValue,
            WarningTitle = detail.WarningTitle,
            WarningText = detail.WarningText,
            ApproveDialog = detail.ApproveDialog,
            ShowApproveDialog = string.Equals(hopThoai, "duyet-hoan", StringComparison.OrdinalIgnoreCase) && selected != null
        });
    }

    [HttpPost("/quan-ly/doi-hoan/duyet")][ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRefund(IFormCollection form, CancellationToken ct)
    {
        var id = ParseId(form["yeu-cau-id"]);
        var note = (form["ly-do"].ToString() ?? "").Trim();
        var req = id.HasValue ? await db.YeuCauDoiHoans.Include(r => r.DatVe).FirstOrDefaultAsync(r => r.Id == id.Value, ct) : null;
        if (req == null) return RedirectToAction(nameof(ChangeRefunds));

        if (req.TrangThai == TrangThaiYeuCau.ChoDuyet)
        {
            req.TrangThai = TrangThaiYeuCau.DaDuyet;
            req.NgayXuLy = DateTimeOffset.UtcNow;
            req.NguoiXuLyId = User.TaiKhoanId();
            req.GhiChuXuLy = string.IsNullOrWhiteSpace(note) ? req.GhiChuXuLy : note;
            if (req.SoTienHoan <= 0 && req.LoaiYeuCau == LoaiYeuCau.HoanVe)
                req.SoTienHoan = Math.Max(0m, (req.DatVe?.TongTien ?? 0m) - req.PhiHoan - req.PhiDichVuKhongHoan - req.PhiHanhLyKhongHoan);
            await db.SaveChangesAsync(ct);
            await WriteAuditAsync("Duyệt yêu cầu đổi/hoàn", $"{req.MaYeuCau} ({req.DatVe?.MaDatVe})", "Đã duyệt", note, ct);
        }
        return RedirectToAction(nameof(ChangeRefunds), new { id = req.Id });
    }

    [HttpPost("/quan-ly/doi-hoan/tu-choi")][ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRefund(IFormCollection form, CancellationToken ct)
    {
        var id = ParseId(form["yeu-cau-id"]);
        var note = (form["ly-do"].ToString() ?? "").Trim();
        var req = id.HasValue ? await db.YeuCauDoiHoans.Include(r => r.DatVe).FirstOrDefaultAsync(r => r.Id == id.Value, ct) : null;
        if (req == null) return RedirectToAction(nameof(ChangeRefunds));

        if (req.TrangThai == TrangThaiYeuCau.ChoDuyet || req.TrangThai == TrangThaiYeuCau.DaDuyet)
        {
            req.TrangThai = TrangThaiYeuCau.TuChoi;
            req.NgayXuLy = DateTimeOffset.UtcNow;
            req.NguoiXuLyId = User.TaiKhoanId();
            req.GhiChuXuLy = string.IsNullOrWhiteSpace(note) ? "Từ chối yêu cầu." : note;
            await db.SaveChangesAsync(ct);
            await WriteAuditAsync("Từ chối yêu cầu đổi/hoàn", $"{req.MaYeuCau} ({req.DatVe?.MaDatVe})", "Từ chối", note, ct);
        }
        return RedirectToAction(nameof(ChangeRefunds), new { id = req.Id });
    }

    [HttpGet("/quan-ly/bao-cao")]
    public async Task<IActionResult> Reports(CancellationToken ct)
    {
        var gross = await db.ThanhToans.Where(t => t.TrangThai == TrangThaiThanhToan.ThanhCong).SumAsync(t => (decimal?)t.SoTien, ct) ?? 0m;
        var tickets = await db.Ves.CountAsync(v => v.TrangThai == TrangThaiVe.DaPhatHanh, ct);
        var bookings = await db.DatVes.CountAsync(ct);
        var cancelled = await db.DatVes.CountAsync(d => d.TrangThai == TrangThaiDatVe.DaHuy || d.TrangThai == TrangThaiDatVe.HetHan, ct);
        var refunded = await db.Ves.CountAsync(v => v.TrangThai == TrangThaiVe.DaHoanTien, ct);
        var cancelRate = bookings == 0 ? 0m : Math.Round((decimal)cancelled / bookings * 100m, 1);
        var refundRate = tickets == 0 ? 0m : Math.Round((decimal)refunded / Math.Max(1, tickets) * 100m, 1);

        var byAirline = await db.Ves.AsNoTracking()
            .Where(v => v.TrangThai == TrangThaiVe.DaPhatHanh)
            .Include(v => v.ChuyenBay).ThenInclude(c => c.HangHangKhong)
            .GroupBy(v => v.ChuyenBay.HangHangKhong.TenHang)
            .Select(g => new { Name = g.Key, Total = g.Sum(x => x.GiaVe) })
            .OrderByDescending(x => x.Total).Take(5).ToListAsync(ct);

        var routes = await db.Ves.AsNoTracking()
            .Where(v => v.TrangThai == TrangThaiVe.DaPhatHanh)
            .Include(v => v.ChuyenBay).ThenInclude(c => c.TuyenBay).ThenInclude(t => t.SanBayDi)
            .Include(v => v.ChuyenBay).ThenInclude(c => c.TuyenBay).ThenInclude(t => t.SanBayDen)
            .GroupBy(v => new { Di = v.ChuyenBay.TuyenBay.SanBayDi.MaSanBay, Den = v.ChuyenBay.TuyenBay.SanBayDen.MaSanBay })
            .Select(g => new { g.Key.Di, g.Key.Den, Count = g.Count(), Total = g.Sum(x => x.GiaVe) })
            .OrderByDescending(x => x.Total).Take(6).ToListAsync(ct);

        return View(new ManagerReportsViewModel
        {
            Kpis = new[]
            {
                new ManagerKpi("Doanh thu gộp", Money.Vnd(gross), "Từ thanh toán thành công (DB)", "success", "chart"),
                new ManagerKpi("Vé đã xuất", tickets.ToString("N0"), $"Tổng {bookings:N0} đặt chỗ", "success", "ticket"),
                new ManagerKpi("Tỷ lệ hủy đặt chỗ", cancelRate + "%", $"{cancelled:N0} / {bookings:N0} đặt chỗ", "warning", "x"),
                new ManagerKpi("Tỷ lệ vé hoàn", refundRate + "%", $"{refunded:N0} vé hoàn", "warning", "undo")
            },
            Airlines = byAirline.Select(a => new ManagerAirlineRevenue(a.Name, Money.Vnd(a.Total), gross <= 0 ? "—" : $"{Math.Round(a.Total / gross * 100m, 1)}% tổng", gross <= 0 ? 0 : (int)Math.Round(a.Total / gross * 100m), "primary")).ToList(),
            Routes = routes.Select(r => new ManagerRouteRevenue($"{r.Di} ↔ {r.Den}", r.Count.ToString("N0"), Money.Vnd(r.Total), "—", Money.Vnd(r.Total))).ToList(),
            Bars = Array.Empty<ManagerRevenueBar>()
        });
    }

    [HttpPost("/quan-ly/bao-cao")][ValidateAntiForgeryToken]
    public IActionResult Reports(IFormCollection form) => RedirectToAction(nameof(Reports));
    [HttpPost("/quan-ly/bao-cao/xuat")][ValidateAntiForgeryToken]
    public IActionResult ExportReport(IFormCollection form) => RedirectToAction(nameof(Reports));

    private static (string, string) HoldStatus(DatVe d) => d.TrangThai == TrangThaiDatVe.ChoThanhToan ? ("Chờ thanh toán", "warning") : ("—", "neutral");
    private static (string, string) PaymentStatus(DatVe d) => d.TrangThai == TrangThaiDatVe.DaXacNhan ? ("Đã thanh toán", "success") : d.TrangThai == TrangThaiDatVe.ChoThanhToan ? ("Chưa thanh toán", "warning") : (d.TrangThai.ToString(), "neutral");
    private static (string, string) TicketStatus(DatVe d) => d.Ves.Any(v => v.TrangThai == TrangThaiVe.DaPhatHanh) ? ("Đã xuất vé", "success") : d.TrangThai == TrangThaiDatVe.DaXacNhan ? ("Chờ xuất", "warning") : ("Chưa xuất", "warning");
    private static string StatusLabel(TrangThaiYeuCau s) => s switch { TrangThaiYeuCau.ChoDuyet => "Chờ duyệt", TrangThaiYeuCau.DaDuyet => "Đã duyệt", TrangThaiYeuCau.TuChoi => "Từ chối", TrangThaiYeuCau.HoanTat => "Hoàn tất", _ => s.ToString() };
    private static string ReqTone(TrangThaiYeuCau s) => s switch { TrangThaiYeuCau.ChoDuyet => "warning", TrangThaiYeuCau.DaDuyet => "info", TrangThaiYeuCau.HoanTat => "success", TrangThaiYeuCau.TuChoi => "danger", _ => "neutral" };

    private static int? ParseId(string? raw) => int.TryParse(raw, out var id) && id > 0 ? id : null;

    private async Task WriteAuditAsync(string hanhDong, string doiTuong, string ketQuaText, string? chiTiet, CancellationToken ct)
    {
        db.NhatKyHoatDongs.Add(new NhatKyHoatDong
        {
            MaNhatKy = $"LOG-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Random.Shared.Next(100,999)}",
            TaiKhoanId = User.TaiKhoanId(),
            TenNguoiDung = User.DisplayName(),
            VaiTro = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Manager",
            HanhDong = hanhDong,
            DoiTuong = doiTuong,
            KetQua = string.Equals(ketQuaText, "Từ chối", StringComparison.OrdinalIgnoreCase) || ketQuaText.Contains("lỗi", StringComparison.OrdinalIgnoreCase) ? KetQuaNhatKy.ThatBai : KetQuaNhatKy.ThanhCong,
            ChiTiet = string.IsNullOrWhiteSpace(chiTiet) ? ketQuaText : $"{ketQuaText}. {chiTiet}",
            DiaChiIp = HttpContext?.Connection?.RemoteIpAddress?.ToString(),
            ThoiGian = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
