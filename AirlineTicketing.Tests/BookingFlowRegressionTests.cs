using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Tests;

[Collection(PostgresCollection.Name)]
public sealed class BookingFlowRegressionTests
{
    [PostgresFact]
    public async Task ThongTinTrong_KhongQuaThanhToan_KhongTaoDatCho()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var client = host.CreateClient();
        var flight = await FlightAsync(db, host);
        var count = await db.DatVes.CountAsync();
        var token = await TokenAsync(client, $"/dat-ve/hanh-khach?flightId={flight.Id}");
        using var response = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["flightId"] = flight.Id.ToString(),
            ["seat"] = "3A", ["baggage"] = "none"
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-validation-summary", html);
        Assert.Contains("#email", html);
        Assert.Equal(count, await db.DatVes.CountAsync());
    }

    [PostgresFact]
    public async Task SuaPhiHanhLy_KhongDuocTaoDatCho()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var client = host.CreateClient();
        var flight = await FlightAsync(db, host);
        var form = PassengerForm(flight.Id, "3A", "1");
        form["__RequestVerificationToken"] = await TokenAsync(client, $"/dat-ve/hanh-khach?flightId={flight.Id}");
        using var response = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("#baggage", await response.Content.ReadAsStringAsync());
        Assert.False(await db.DatVes.AnyAsync(x => x.EmailLienHe == form["email"]));
    }

    [PostgresFact]
    public async Task GiuGheTruocThanhToan_KhongGiaHanKhiTaiLai_VaChanPhienKhac()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var owner = host.CreateClient();
        using var other = host.CreateClient();
        var flight = await FlightAsync(db, host);
        var form = PassengerForm(flight.Id, "3A", "extra10");
        await HoldAsync(owner, form);
        var hold = await db.DatVes.Include(x => x.Ves).SingleAsync(x => x.EmailLienHe == form["email"]);
        Assert.Equal(TrangThaiDatVe.ChoThanhToan, hold.TrangThai);
        Assert.Equal(TrangThaiVe.TamGiu, Assert.Single(hold.Ves).TrangThai);
        Assert.Equal(12, hold.Ves.Single().HanhLyXachTayKg);
        Assert.Equal(33, hold.Ves.Single().HanhLyKyGuiKg);
        Assert.Equal(flight.GiaCoBan + 380_000m + 70_000m + 250_000m, hold.TongTien);
        var deadline = hold.HetHanGiuCho;
        using var page = await owner.GetAsync("/dat-ve/thanh-toan");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var competing = PassengerForm(flight.Id, "3A", "none");
        competing["__RequestVerificationToken"] = await TokenAsync(other, $"/dat-ve/hanh-khach?flightId={flight.Id}");
        using var denied = await other.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(competing));
        Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
        Assert.Contains("#seat", await denied.Content.ReadAsStringAsync());
        await db.Entry(hold).ReloadAsync();
        Assert.Equal(deadline, hold.HetHanGiuCho);
        Assert.False(await db.DatVes.AnyAsync(x => x.EmailLienHe == competing["email"]));
    }

    [PostgresFact]
    public async Task ThanhToanLap_KhongThuTienHaiLan_VaKhongLoDatChoQuaPnr()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var owner = host.CreateClient();
        using var other = host.CreateClient();
        var flight = await FlightAsync(db, host);
        var passenger = PassengerForm(flight.Id, "3A", "none");
        await HoldAsync(owner, passenger);
        var card = CardForm(await TokenAsync(owner, "/dat-ve/thanh-toan"));
        using var first = await owner.PostAsync("/dat-ve/thanh-toan", new FormUrlEncodedContent(card));
        using var repeated = await owner.PostAsync("/dat-ve/thanh-toan", new FormUrlEncodedContent(card));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(first.Headers.Location, repeated.Headers.Location);
        var booking = await db.DatVes.Include(x => x.ThanhToans).Include(x => x.Ves)
            .SingleAsync(x => x.EmailLienHe == passenger["email"]);
        Assert.Equal(TrangThaiDatVe.DaXacNhan, booking.TrangThai);
        Assert.Equal(TrangThaiVe.DaPhatHanh, booking.Ves.Single().TrangThai);
        Assert.Single(booking.ThanhToans);
        using var ownPdf = await owner.GetAsync($"/ve/{booking.MaDatVe}/pdf");
        Assert.Equal(HttpStatusCode.OK, ownPdf.StatusCode);
        Assert.Equal("application/pdf", ownPdf.Content.Headers.ContentType?.MediaType);
        await HoldAsync(other, PassengerForm(flight.Id, "3B", "none"));
        foreach (var url in new[] { $"/ve/{booking.MaDatVe}/pdf", $"/dat-ve/thanh-cong?pnr={booking.MaDatVe}", $"/dat-ve/chuyen-khoan/{booking.MaDatVe}" })
        {
            using var denied = await other.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
    }

    [PostgresFact]
    public async Task GiaDaChot_KhongDoiKhiCauHinhDoi_VaTongGiaTimKiemGomPhi()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var client = host.CreateClient();
        var flight = await FlightAsync(db, host);
        var passenger = PassengerForm(flight.Id, "3A", "none");
        await HoldAsync(client, passenger);
        var hold = await db.DatVes.SingleAsync(x => x.EmailLienHe == passenger["email"]);
        var tax = await db.CauHinhHeThongs.SingleOrDefaultAsync(x => x.Khoa == "booking.tax_default");
        var oldValue = tax?.GiaTri;
        if (tax is null)
        {
            tax = new CauHinhHeThong { Khoa = "booking.tax_default", GiaTri = "900000", MoTa = "Thuế kiểm thử", Nhom = "Test" };
            db.CauHinhHeThongs.Add(tax);
        }
        else tax.GiaTri = "900000";
        await db.SaveChangesAsync();
        try
        {
            var after = await client.GetStringAsync("/dat-ve/thanh-toan");
            Assert.Contains(WebUtility.HtmlEncode(hold.TongTien.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))) + " ₫", after);
            Assert.Equal(hold.TongTien, (await db.DatVes.AsNoTracking().SingleAsync(x => x.Id == hold.Id)).TongTien);
            var date = flight.GioKhoiHanh.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd");
            var search = await client.GetStringAsync($"/chuyen-bay?from={flight.TuyenBay.SanBayDi.MaSanBay}&to={flight.TuyenBay.SanBayDen.MaSanBay}&depart={date}");
            Assert.Contains((flight.GiaCoBan + 900_000m + 70_000m).ToString("N0", CultureInfo.GetCultureInfo("vi-VN")), WebUtility.HtmlDecode(search));
        }
        finally
        {
            if (oldValue is null) db.CauHinhHeThongs.Remove(tax); else tax.GiaTri = oldValue;
            await db.SaveChangesAsync();
        }
    }

    [PostgresFact]
    public async Task HetHan_KhongDuocThanhToan_VaJobTraGhe()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var client = host.CreateClient();
        var flight = await FlightAsync(db, host);
        var form = PassengerForm(flight.Id, "3A", "none");
        await HoldAsync(client, form);
        var token = await TokenAsync(client, "/dat-ve/thanh-toan");
        var hold = await db.DatVes.SingleAsync(x => x.EmailLienHe == form["email"]);
        hold.HetHanGiuCho = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        using var response = await client.PostAsync("/dat-ve/thanh-toan", new FormUrlEncodedContent(CardForm(token)));
        Assert.Equal("/dat-ve/het-han", response.Headers.Location?.OriginalString);
        var bus = new DatVeBUS(db, TimeProvider.System, new DatVeOptions());
        await bus.HetHanCacDatVeAsync();
        await db.Entry(hold).ReloadAsync();
        Assert.Equal(TrangThaiDatVe.HetHan, hold.TrangThai);
        Assert.False(await db.ThanhToans.AnyAsync(x => x.DatVeId == hold.Id));
        Assert.Equal(TrangThaiVe.DaHuy, (await db.Ves.SingleAsync(x => x.DatVeId == hold.Id)).TrangThai);
    }

    [PostgresFact]
    public async Task ChuyenKhoan_BaoDaChuyenKhongTuXuatVe_VaKhongNhanBaoTuPhienKhac()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var owner = host.CreateClient();
        using var other = host.CreateClient();
        var flight = await FlightAsync(db, host);
        var passenger = PassengerForm(flight.Id, "3A", "none");
        await HoldAsync(owner, passenger);
        var token = await TokenAsync(owner, "/dat-ve/thanh-toan");
        var form = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["payment"] = "transfer" };
        using var first = await owner.PostAsync("/dat-ve/thanh-toan", new FormUrlEncodedContent(form));
        using var repeated = await owner.PostAsync("/dat-ve/thanh-toan", new FormUrlEncodedContent(form));
        Assert.Equal(first.Headers.Location, repeated.Headers.Location);
        var booking = await db.DatVes.Include(x => x.ThanhToans).SingleAsync(x => x.EmailLienHe == passenger["email"]);
        var reportUrl = $"/dat-ve/chuyen-khoan/{booking.MaDatVe}/xac-nhan";
        var otherToken = await TokenAsync(other, "/tra-cuu");
        using var denied = await other.PostAsync(reportUrl, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = otherToken }));
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var ownerToken = await TokenAsync(owner, first.Headers.Location!.OriginalString);
        using var reported = await owner.PostAsync(reportUrl, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = ownerToken }));
        Assert.Equal(HttpStatusCode.Redirect, reported.StatusCode);
        await db.Entry(booking).ReloadAsync();
        Assert.Equal(TrangThaiDatVe.ChoThanhToan, booking.TrangThai);
        Assert.Single(booking.ThanhToans);
        Assert.Equal(TrangThaiThanhToan.ChoXuLy, booking.ThanhToans.Single().TrangThai);
    }

    [PostgresFact]
    public async Task HaiPhienGiuDongThoi_CungMotGhe_ChiMotPhienThanhCong()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var first = host.CreateClient();
        using var second = host.CreateClient();
        var flight = await FlightAsync(db, host);
        var formA = PassengerForm(flight.Id, "3A", "none");
        var formB = PassengerForm(flight.Id, "3A", "none");
        formA["__RequestVerificationToken"] = await TokenAsync(first, $"/dat-ve/hanh-khach?flightId={flight.Id}");
        formB["__RequestVerificationToken"] = await TokenAsync(second, $"/dat-ve/hanh-khach?flightId={flight.Id}");
        var responses = await Task.WhenAll(
            first.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(formA)),
            second.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(formB)));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Redirect);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Equal(1, await db.Ves.CountAsync(x => x.ChuyenBayId == flight.Id && x.SoGhe == "3A"
                && x.TrangThai == TrangThaiVe.TamGiu));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [PostgresFact]
    public async Task GuiLaiVaSuaDatCho_KhongGiaHan_KhongDeHaiGheBiGiu()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var client = host.CreateClient();
        var flight = await FlightAsync(db, host);
        var form = PassengerForm(flight.Id, "3A", "extra10");
        await HoldAsync(client, form);
        var original = await db.DatVes.SingleAsync(x => x.EmailLienHe == form["email"]);
        await HoldAsync(client, form);
        Assert.Equal(1, await db.DatVes.CountAsync(x => x.EmailLienHe == form["email"]));
        form["seat"] = "1B";
        form["baggage"] = "extra20";
        await HoldAsync(client, form);
        await db.Entry(original).ReloadAsync();
        Assert.Equal(TrangThaiDatVe.DaHuy, original.TrangThai);
        var edited = await db.DatVes.Include(x => x.Ves).SingleAsync(x => x.EmailLienHe == form["email"]
            && x.TrangThai == TrangThaiDatVe.ChoThanhToan);
        Assert.Equal(original.HetHanGiuCho, edited.HetHanGiuCho);
        Assert.Equal(43, edited.Ves.Single().HanhLyKyGuiKg);
        Assert.Equal("1B", edited.Ves.Single().SoGhe);
        Assert.Equal(flight.GiaCoBan + 380_000m + 70_000m + 480_000m + 300_000m, edited.TongTien);
        Assert.Equal(1, await db.Ves.CountAsync(x => x.ChuyenBayId == flight.Id && x.HanhKhach.SoHoChieu == form["identityNumber"]
            && x.TrangThai == TrangThaiVe.TamGiu));
    }

    [PostgresFact]
    public async Task TheKhongThuocSandbox_BiTuChoi_KhongGhiThanhToan()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var client = host.CreateClient();
        var flight = await FlightAsync(db, host);
        var passenger = PassengerForm(flight.Id, "3A", "none");
        await HoldAsync(client, passenger);
        var card = CardForm(await TokenAsync(client, "/dat-ve/thanh-toan"));
        card["cardNumber"] = "4111111111111111";
        using var response = await client.PostAsync("/dat-ve/thanh-toan", new FormUrlEncodedContent(card));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("4242", await response.Content.ReadAsStringAsync());
        var booking = await db.DatVes.SingleAsync(x => x.EmailLienHe == passenger["email"]);
        Assert.Equal(TrangThaiDatVe.ChoThanhToan, booking.TrangThai);
        Assert.False(await db.ThanhToans.AnyAsync(x => x.DatVeId == booking.Id));
    }

    [PostgresFact]
    public async Task Checkout_AcceptsPresetDemoCardWithoutCardDetails()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var client = host.CreateClient();
        var flight = await FlightAsync(db, host);
        var passenger = PassengerForm(flight.Id, "3A", "none");
        await HoldAsync(client, passenger);
        const string checkout = "/dat-ve/thanh-toan";
        var html = await client.GetStringAsync(checkout);
        Assert.Contains("name=\"demoCard\"", html);
        Assert.DoesNotContain("name=\"cardNumber\"", html);
        Assert.DoesNotContain("name=\"cardCvv\"", html);
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TokenAsync(client, checkout),
            ["payment"] = "card",
            ["demoCard"] = "mastercard"
        };
        using var response = await client.PostAsync(checkout, new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/dat-ve/thanh-cong", response.Headers.Location?.OriginalString);
        var payment = await db.ThanhToans.SingleAsync(p => p.DatVe.EmailLienHe == passenger["email"]);
        Assert.Equal(TrangThaiThanhToan.ThanhCong, payment.TrangThai);
        Assert.Equal("Sandbox Mastercard •••• 4444", payment.NhaCungCap);
    }

    [PostgresFact]
    public async Task Promotion_ValidatesThreshold_ReplayAndRemovalKeepTheHeldQuoteConsistent()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var client = host.CreateClient();
        var flight = await FlightAsync(db, host);
        var form = PassengerForm(flight.Id, "3A", "none");
        await HoldAsync(client, form);
        var booking = await db.DatVes.SingleAsync(x => x.EmailLienHe == form["email"]);
        var token = await TokenAsync(client, "/dat-ve/thanh-toan");
        var promotion = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["code"] = "AERONEW" };
        using var ineligible = await client.PostAsync("/dat-ve/khuyen-mai", new FormUrlEncodedContent(promotion));
        Assert.Equal(HttpStatusCode.Redirect, ineligible.StatusCode);
        Assert.Contains("3.000.000", await client.GetStringAsync("/dat-ve/thanh-toan"));
        await db.Entry(booking).ReloadAsync();
        Assert.Equal(0m, booking.GiamGia);

        flight.GiaCoBan = 3_000_000m;
        await db.SaveChangesAsync();
        form["seat"] = "3B";
        await HoldAsync(client, form);
        var held = await db.DatVes.SingleAsync(x => x.EmailLienHe == form["email"] && x.TrangThai == TrangThaiDatVe.ChoThanhToan);
        var gross = held.TongTien;
        var deadline = held.HetHanGiuCho;
        promotion["code"] = "  aeronew ";
        using var applied = await client.PostAsync("/dat-ve/khuyen-mai", new FormUrlEncodedContent(promotion));
        using var repeated = await client.PostAsync("/dat-ve/khuyen-mai", new FormUrlEncodedContent(promotion));
        Assert.Equal(HttpStatusCode.Redirect, applied.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, repeated.StatusCode);
        await db.Entry(held).ReloadAsync();
        Assert.Equal(gross - held.PhiDichVu, held.TongTien);
        Assert.Equal(deadline, held.HetHanGiuCho);
        Assert.Equal("AERONEW", held.MaKhuyenMai);
        var staleCard = CardForm(token);
        staleCard["expectedTotal"] = gross.ToString(CultureInfo.InvariantCulture);
        using var stale = await client.PostAsync("/dat-ve/thanh-toan", new FormUrlEncodedContent(staleCard));
        Assert.Equal("/dat-ve/thanh-toan", stale.Headers.Location?.OriginalString);
        Assert.False(await db.ThanhToans.AnyAsync(x => x.DatVeId == held.Id));

        promotion["remove"] = "true";
        using var removed = await client.PostAsync("/dat-ve/khuyen-mai", new FormUrlEncodedContent(promotion));
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        await db.Entry(held).ReloadAsync();
        Assert.Equal(gross, held.TongTien);
        Assert.Equal(0m, held.GiamGia);
        Assert.Null(held.MaKhuyenMai);
    }

    [PostgresFact]
    public async Task DemoAfterSales_ChangesTicketAndRefundsBothCapturesExactlyOnce()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var checkout = host.CreateClient();
        var original = await FlightAsync(db, host);
        var target = new ChuyenBay
        {
            SoHieu = "AV7997", TuyenBayId = original.TuyenBayId, MayBayId = original.MayBayId,
            HangHangKhongId = original.HangHangKhongId, GioKhoiHanh = original.GioKhoiHanh.AddDays(2),
            GioHaCanh = original.GioHaCanh.AddDays(2), GiaCoBan = original.GiaCoBan + 1_800_000m
        };
        var login = $"change-{Guid.NewGuid():N}";
        var staff = new TaiKhoan
        {
            TenDangNhap = login, HoTen = "Demo Manager", Email = login + "@example.test",
            VaiTro = VaiTro.Manager, MatKhauHash = "unused-in-test"
        };
        db.AddRange(target, staff);
        await db.SaveChangesAsync();

        const string seat = "3A";
        const string changePath = "/ve-cua-toi/yeu-cau-doi";
        const string refundPath = "/ve-cua-toi/yeu-cau-hoan";
        const string tracking = "/ve-cua-toi/yeu-cau";
        const string operations = "/quan-ly/doi-hoan";
        var form = PassengerForm(original.Id, seat, "none");
        await HoldAsync(checkout, form);
        using var paid = await checkout.PostAsync("/dat-ve/thanh-toan",
            new FormUrlEncodedContent(CardForm(await TokenAsync(checkout, "/dat-ve/thanh-toan"))));
        Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode);
        var booking = await db.DatVes.Include(b => b.Ves).Include(b => b.ThanhToans)
            .SingleAsync(b => b.EmailLienHe == form["email"]);
        var pnr = booking.MaDatVe;
        var originalPaid = booking.TongTien;
        var ticket = Assert.Single(booking.Ves);
        var oldNumber = ticket.MaVe;
        using var customer = host.CreateClient(AppRoles.KhachHang, booking.EmailLienHe, booking.KhachHangId);
        using var outsider = host.CreateClient(AppRoles.KhachHang, khachHangId: host.Data.CustomerBId);
        using var manager = host.CreateClient(AppRoles.Manager, taiKhoanId: staff.Id);

        var changeForm = new Dictionary<string, string> { ["pnr"] = pnr, ["newFlightId"] = target.Id.ToString() };
        using var requested = await SubmitAsync(customer, $"{changePath}?pnr={pnr}&newFlightId={target.Id}", changePath, changeForm);
        Assert.Equal(HttpStatusCode.Redirect, requested.StatusCode);
        var change = await db.YeuCauDoiHoans.SingleAsync(r => r.DatVeId == booking.Id);
        Assert.Equal(target.Id, change.ChuyenBayMoiId);
        var approve = new Dictionary<string, string> { ["yeu-cau-id"] = change.Id.ToString() };
        using var approved = await SubmitAsync(manager, $"{operations}?id={change.Id}", $"{operations}/duyet", approve);
        Assert.Equal(HttpStatusCode.Redirect, approved.StatusCode);
        await db.Entry(change).ReloadAsync();
        Assert.Equal(TrangThaiYeuCau.DaDuyet, change.TrangThai);
        Assert.Equal(target.GiaCoBan, change.GiaVeMoi);
        var charge = target.GiaCoBan - ticket.GiaVe + change.PhiDoi;
        var confirm = new Dictionary<string, string> { ["expectedCharge"] = "1", ["accepted"] = "true" };
        var changeDetail = $"{tracking}?id={change.MaYeuCau}";
        var confirmPath = $"{changePath}/{change.MaYeuCau}/xac-nhan";
        Assert.Contains(confirmPath, await customer.GetStringAsync(changeDetail));
        using var stale = await SubmitAsync(customer, changeDetail, confirmPath, confirm);
        confirm["expectedCharge"] = charge.ToString(CultureInfo.InvariantCulture);
        using var denied = await SubmitAsync(outsider, "/tra-cuu", confirmPath, confirm);
        using var blockedRefund = await SubmitAsync(customer, $"{refundPath}?pnr={pnr}", refundPath, new() { ["pnr"] = pnr });
        Assert.Equal(changeDetail, blockedRefund.Headers.Location?.OriginalString);
        target.TrangThai = TrangThaiChuyenBay.BiHuy;
        await db.SaveChangesAsync();
        using var unavailable = await SubmitAsync(customer, changeDetail, confirmPath, confirm);
        await db.Entry(ticket).ReloadAsync();
        Assert.Equal(original.Id, ticket.ChuyenBayId);
        Assert.Equal(oldNumber, ticket.MaVe);
        Assert.Equal(1, await db.ThanhToans.CountAsync(p => p.DatVeId == booking.Id));

        target.TrangThai = TrangThaiChuyenBay.DuKien;
        target.GiaCoBan += 200_000m;
        await db.SaveChangesAsync();
        using var confirmed = await SubmitAsync(customer, changeDetail, confirmPath, confirm);
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        Assert.Contains("Đã đổi chuyến và phát hành vé mới.",
            WebUtility.HtmlDecode(await customer.GetStringAsync(changeDetail)));
        using var repeatedChange = await SubmitAsync(customer, "/tra-cuu", confirmPath, confirm);
        await db.Entry(change).ReloadAsync();
        await db.Entry(ticket).ReloadAsync();
        await db.Entry(booking).ReloadAsync();
        Assert.Equal(TrangThaiYeuCau.HoanTat, change.TrangThai);
        Assert.Equal(target.Id, ticket.ChuyenBayId);
        Assert.NotEqual(oldNumber, ticket.MaVe);
        Assert.Equal(change.GiaVeMoi, ticket.GiaVe);
        Assert.Equal(change.PhiDoi, booking.PhiDoiDaThu);
        Assert.Equal(originalPaid + charge, booking.TongTien);
        Assert.Equal(2, await db.ThanhToans.CountAsync(p => p.DatVeId == booking.Id));
        var jobs = await db.EmailDeliveryJobs.Where(j => j.BookingId == booking.Id).ToListAsync();
        Assert.Equal(2, jobs.Count);
        Assert.Single(jobs, j => j.CompletedAt is null);
        var flightDal = new AirlineTicketing.DAL.Repositories.ChuyenBayDAL(db);
        Assert.False((await flightDal.GetSeatStatesAsync(original.Id)).ContainsKey(seat));
        var newSeat = Assert.IsType<string>(ticket.SoGhe);
        Assert.True((await flightDal.GetSeatStatesAsync(target.Id)).ContainsKey(newSeat));
        using var pdf = await customer.GetAsync($"/ve/{pnr}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);

        using var refundRequested = await SubmitAsync(customer, $"{refundPath}?pnr={pnr}", refundPath, new() { ["pnr"] = pnr });
        Assert.Equal(HttpStatusCode.Redirect, refundRequested.StatusCode);
        var refund = await db.YeuCauDoiHoans.SingleAsync(r => r.DatVeId == booking.Id && r.LoaiYeuCau == LoaiYeuCau.HoanVe);
        var expectedRefund = booking.TongTien - change.PhiDoi - refund.PhiHoan - refund.PhiDichVuKhongHoan - refund.PhiHanhLyKhongHoan;
        Assert.Equal(expectedRefund, refund.SoTienHoan);
        using var refundApproved = await SubmitAsync(manager, $"{operations}?id={refund.Id}", $"{operations}/duyet",
            new() { ["yeu-cau-id"] = refund.Id.ToString() });
        var instructions = new Dictionary<string, string> { ["id"] = refund.Id.ToString(), ["confirmed"] = "true" };
        var refundDetail = $"{operations}?tab=da-duyet&id={refund.Id}";
        var refundConfirm = $"{operations}/hoan-tat-demo";
        Assert.Contains(refundConfirm, await manager.GetStringAsync(refundDetail));
        var refunds = await db.HoanTiens.Where(r => r.YeuCauDoiHoanId == refund.Id).ToListAsync();
        Assert.Equal(2, refunds.Count);
        Assert.Equal(expectedRefund, refunds.Sum(r => r.SoTien));
        Assert.All(refunds, r => Assert.Equal(TrangThaiHoanTien.ChoXuLy, r.TrangThai));
        using var staffOnly = await SubmitAsync(customer, "/tra-cuu", refundConfirm, instructions);
        await db.Entry(booking).ReloadAsync();
        Assert.Equal(TrangThaiDatVe.DaXacNhan, booking.TrangThai);
        using var completed = await SubmitAsync(manager, refundDetail, refundConfirm, instructions);
        using var repeatedRefund = await SubmitAsync(manager, "/tra-cuu", refundConfirm, instructions);
        await db.Entry(refund).ReloadAsync();
        await db.Entry(booking).ReloadAsync();
        await db.Entry(ticket).ReloadAsync();
        Assert.Equal(TrangThaiYeuCau.HoanTat, refund.TrangThai);
        Assert.Equal(TrangThaiDatVe.HoanTien, booking.TrangThai);
        Assert.Equal(TrangThaiVe.DaHoanTien, ticket.TrangThai);
        Assert.False((await flightDal.GetSeatStatesAsync(target.Id)).ContainsKey(newSeat));
        foreach (var part in refunds)
        {
            await db.Entry(part).ReloadAsync();
            Assert.Equal(TrangThaiHoanTien.ThanhCong, part.TrangThai);
        }
        Assert.Equal(2, await db.HoanTiens.CountAsync(r => r.YeuCauDoiHoanId == refund.Id));
        Assert.Equal(1, await db.LichSuDatVes.CountAsync(row => row.DatVeId == booking.Id && row.HanhDong == "HoanVeDemoHoanTat"));
        using var returnedPdf = await customer.GetAsync($"/ve/{pnr}/pdf");
        Assert.Equal(HttpStatusCode.NotFound, returnedPdf.StatusCode);
    }

    [PostgresFact]
    public async Task FlightCancellation_StopsPdfAndCheckout_NotifiesOnce_AndRefundsFullDemoPayment()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var flight = await FlightAsync(db, host);
        using var checkout = host.CreateClient();
        using var holding = host.CreateClient();
        const string schedule = "/quan-ly/lich-bay";
        const string desk = "/quan-ly/bien-dong-chuyen-bay";
        var passenger = PassengerForm(flight.Id, "3A", "extra10");
        await HoldAsync(checkout, passenger);
        using var paid = await checkout.PostAsync("/dat-ve/thanh-toan",
            new FormUrlEncodedContent(CardForm(await TokenAsync(checkout, "/dat-ve/thanh-toan"))));
        Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode);
        var heldPassenger = PassengerForm(flight.Id, "3B", "none");
        await HoldAsync(holding, heldPassenger);
        var booking = await db.DatVes.SingleAsync(x => x.EmailLienHe == passenger["email"]);
        var held = await db.DatVes.SingleAsync(x => x.EmailLienHe == heldPassenger["email"]);
        var heldCard = CardForm(await TokenAsync(holding, "/dat-ve/thanh-toan"));
        var initialTicket = await db.EmailDeliveryJobs.SingleAsync(x => x.BookingId == booking.Id);
        var staff = new TaiKhoan
        {
            TenDangNhap = $"operations-{Guid.NewGuid():N}", HoTen = "Demo Manager",
            Email = $"operations-{Guid.NewGuid():N}@example.test", VaiTro = VaiTro.Manager, MatKhauHash = "unused"
        };
        db.TaiKhoans.Add(staff);
        await db.SaveChangesAsync();
        using var manager = host.CreateClient(AppRoles.Manager, taiKhoanId: staff.Id);
        using var owner = host.CreateClient(AppRoles.KhachHang, booking.EmailLienHe, booking.KhachHangId);
        var noticesBefore = await db.EmailDeliveryJobs.CountAsync(x => x.BookingId == null && x.ProtectedMessage != null);
        var cancel = new Dictionary<string, string> { ["chuyen-bay-id"] = flight.Id.ToString() };
        using var cancelled = await SubmitAsync(manager, "/tra-cuu", schedule + "/huy-chuyen", cancel);
        using var repeat = await SubmitAsync(manager, "/tra-cuu", schedule + "/huy-chuyen", cancel);
        Assert.Equal(HttpStatusCode.Redirect, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, repeat.StatusCode);
        await db.Entry(flight).ReloadAsync();
        Assert.Equal(TrangThaiChuyenBay.BiHuy, flight.TrangThai);
        var disruption = await db.BienDongDatVes.SingleAsync(x => x.DatVeId == booking.Id);
        Assert.Null(disruption.NgayXuLy);
        Assert.True(disruption.BiHuy);
        Assert.NotNull((await db.BienDongDatVes.SingleAsync(x => x.DatVeId == held.Id)).NgayXuLy);
        await db.Entry(held).ReloadAsync();
        Assert.Equal(TrangThaiDatVe.DaHuy, held.TrangThai);
        Assert.Equal(TrangThaiVe.DaHuy, await db.Ves.Where(x => x.DatVeId == held.Id).Select(x => x.TrangThai).SingleAsync());
        using var blockedPayment = await holding.PostAsync("/dat-ve/thanh-toan", new FormUrlEncodedContent(heldCard));
        Assert.False(await db.ThanhToans.AnyAsync(x => x.DatVeId == held.Id));
        await db.Entry(initialTicket).ReloadAsync();
        Assert.NotNull(initialTicket.CompletedAt);
        Assert.Equal(noticesBefore + 4, await db.EmailDeliveryJobs.CountAsync(x => x.BookingId == null && x.ProtectedMessage != null));
        var ticketPage = $"/ve-cua-toi?pnr={booking.MaDatVe}";
        Assert.Contains("Chuyến bị hủy", WebUtility.HtmlDecode(await owner.GetStringAsync(ticketPage)));
        using var pdf = await owner.GetAsync($"/ve/{booking.MaDatVe}/pdf");
        Assert.Equal(HttpStatusCode.NotFound, pdf.StatusCode);
        var lookup = new Dictionary<string, string> { ["Pnr"] = booking.MaDatVe, ["Email"] = passenger["email"] };
        using var found = await SubmitAsync(checkout, "/tra-cuu", "/tra-cuu", lookup);
        Assert.Contains("Cần xử lý lịch bay", WebUtility.HtmlDecode(await found.Content.ReadAsStringAsync()));
        Assert.Contains("Hành trình cần xử lý", WebUtility.HtmlDecode(await checkout.GetStringAsync(paid.Headers.Location!)));
        using var send = await SubmitAsync(owner, "/tra-cuu", $"/ve/{booking.MaDatVe}/gui-lai-email", new());
        Assert.False(await db.EmailDeliveryJobs.AnyAsync(x => x.BookingId == booking.Id && x.CompletedAt == null));

        var refundPath = $"{desk}/{disruption.Id}/hoan-demo";
        var details = $"{desk}?id={disruption.Id}";
        Assert.Contains(refundPath, await manager.GetStringAsync(details));
        using var denied = await SubmitAsync(owner, "/tra-cuu", refundPath, new() { ["confirmed"] = "true" });
        Assert.Equal("/dang-nhap?tu-choi=1", denied.Headers.Location?.OriginalString);
        using var refunded = await SubmitAsync(manager, details, refundPath, new() { ["confirmed"] = "true" });
        using var replayed = await SubmitAsync(manager, "/tra-cuu", refundPath, new() { ["confirmed"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, refunded.StatusCode);
        await db.Entry(disruption).ReloadAsync();
        await db.Entry(booking).ReloadAsync();
        Assert.NotNull(disruption.NgayXuLy);
        Assert.Equal(TrangThaiDatVe.HoanTien, booking.TrangThai);
        var refund = await db.HoanTiens.SingleAsync(x => x.ThanhToan.DatVeId == booking.Id);
        Assert.Equal(booking.TongTien, refund.SoTien);
        Assert.Equal(TrangThaiHoanTien.ThanhCong, refund.TrangThai);
        Assert.Equal(TrangThaiVe.DaHoanTien, await db.Ves.Where(x => x.DatVeId == booking.Id).Select(x => x.TrangThai).SingleAsync());
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, string form, string path, Dictionary<string, string> payload)
    {
        payload["__RequestVerificationToken"] = await TokenAsync(client, form);
        return await client.PostAsync(path, new FormUrlEncodedContent(payload));
    }

    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(PostgresTestConnection.Require()).Options);
    private static Task<ChuyenBay> FlightAsync(AppDbContext db, PostgresTestHost host) => db.ChuyenBays
        .Where(x => x.Ves.Any(v => v.DatVe.MaDatVe == host.Data.PnrA))
        .Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
        .Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDen).SingleAsync();
    private static Dictionary<string, string> PassengerForm(int flightId, string seat, string baggage)
    {
        var id = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
        return new()
        {
            ["flightId"] = flightId.ToString(), ["seat"] = seat, ["baggage"] = baggage,
            ["lastName"] = "NGUYEN", ["firstName"] = "AUDIT TEST", ["gender"] = "Nam",
            ["dateOfBirth"] = "01/01/2000", ["nationality"] = "Việt Nam", ["identityNumber"] = id,
            ["email"] = $"audit-{id.ToLowerInvariant()}@example.test", ["phone"] = "0900000000"
        };
    }
    private static async Task HoldAsync(HttpClient client, Dictionary<string, string> form)
    {
        form["__RequestVerificationToken"] = await TokenAsync(client, $"/dat-ve/hanh-khach?flightId={form["flightId"]}");
        using var response = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dat-ve/thanh-toan", response.Headers.Location?.OriginalString);
    }
    private static async Task<string> TokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return token;
    }
    private static Dictionary<string, string> CardForm(string token) => new()
    {
        ["__RequestVerificationToken"] = token, ["payment"] = "card", ["cardNumber"] = "4242424242424242",
        ["cardHolder"] = "AUDIT TEST", ["cardExpiry"] = "12/28", ["cardCvv"] = "123"
    };
}
