using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.Infrastructure.Payments;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Tests;

[Collection(PostgresCollection.Name)]
public sealed class VnpayFlowTests
{
    private const string Provider = VnpayOptions.Provider;
    private const string Merchant = "TESTPAY1";
    private const string TestKey = "synthetic-vnpay-key-only-for-integration-tests";
    private const string Checkout = "/dat-ve/thanh-toan";
    private const string Operations = "/quan-ly/thanh-toan-vnpay";
    private const string Ipn = "/dat-ve/vnpay/ipn";

    [PostgresFact]
    public async Task RedirectAndIpn_OnlySignedAmountIssuesTickets_AndDuplicateDoesNotIssueAgain()
    {
        await using var host = await PostgresTestHost.CreateAsync(vnpay: Options());
        await using var db = Database();
        using var owner = host.CreateClient();
        using var webhook = host.CreateClient();
        var booking = await HoldAsync(host, db, owner);
        var url = await StartAsync(owner, booking);
        var repeated = await StartAsync(owner, booking);
        Assert.Equal(url, repeated);
        Assert.Equal("sandbox.vnpayment.vn", url.Host);
        var query = QueryHelpers.ParseQuery(url.Query);
        var urlFields = query.ToDictionary(x => x.Key, x => x.Value.ToString());
        Assert.Equal(Sign(QueryData(urlFields)), urlFields["vnp_SecureHash"]);
        Assert.Equal("https://merchant.example.test/dat-ve/vnpay/ket-qua", urlFields["vnp_ReturnUrl"]);
        var payment = await db.ThanhToans.SingleAsync(x => x.DatVeId == booking.Id);
        Assert.Equal(TrangThaiThanhToan.ChoXuLy, payment.TrangThai);

        var transactionNo = TestTransactionNo(payment);
        var notice = Notice(payment, transactionNo);
        var back = await owner.GetStringAsync(SignedUrl("/dat-ve/vnpay/ket-qua", notice));
        Assert.Contains("chờ máy chủ VNPAY xác nhận", WebUtility.HtmlDecode(back));
        Assert.Equal(TrangThaiDatVe.ChoThanhToan, (await db.DatVes.AsNoTracking().SingleAsync(x => x.Id == booking.Id)).TrangThai);
        using var transfer = await SubmitAsync(owner, Checkout, Checkout, new() { ["payment"] = "transfer" });
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        Assert.Single(await db.ThanhToans.Where(x => x.DatVeId == booking.Id).ToArrayAsync());

        Assert.Equal("97", await AckAsync(webhook, SignedUrl(Ipn, notice) + "&vnp_Amount=1"));
        var wrongAmount = new Dictionary<string, string>(notice) { ["vnp_Amount"] = "100" };
        Assert.Equal("04", await AckAsync(webhook, SignedUrl(Ipn, wrongAmount)));
        var wrongMerchant = new Dictionary<string, string>(notice) { ["vnp_TmnCode"] = "TESTPAY2" };
        Assert.Equal("97", await AckAsync(webhook, SignedUrl(Ipn, wrongMerchant)));
        Assert.Equal(TrangThaiVe.TamGiu, (await db.Ves.AsNoTracking().SingleAsync(x => x.DatVeId == booking.Id)).TrangThai);

        var replies = await Task.WhenAll(AckAsync(webhook, SignedUrl(Ipn, notice)), AckAsync(webhook, SignedUrl(Ipn, notice)));
        Assert.Equal(new[] { "00", "02" }, replies.Order());
        await db.Entry(payment).ReloadAsync();
        Assert.Equal(transactionNo, payment.MaGiaoDichNhaCungCap);
        Assert.Equal(TrangThaiThanhToan.ThanhCong, payment.TrangThai);
        Assert.Equal(TrangThaiVe.DaPhatHanh, (await db.Ves.AsNoTracking().SingleAsync(x => x.DatVeId == booking.Id)).TrangThai);
        Assert.Equal(1, await db.EmailDeliveryJobs.CountAsync(x => x.BookingId == booking.Id));
        using var status = await owner.GetAsync("/dat-ve/vnpay/trang-thai");
        Assert.StartsWith("/dat-ve/thanh-cong", status.Headers.Location?.OriginalString);
    }

    [PostgresFact]
    public async Task LateSuccessQueuesOneRefund_AndAmbiguousGatewayResponseIsNeverResent()
    {
        var external = new FakeApi { BadRefundSignature = true };
        await using var host = await PostgresTestHost.CreateAsync(vnpay: Options(), vnpayHandler: external);
        await using var db = Database();
        using var owner = host.CreateClient();
        using var manager = host.CreateClient(AppRoles.Manager);
        using var webhook = host.CreateClient();
        var booking = await HoldAsync(host, db, owner);
        await StartAsync(owner, booking);
        var payment = await db.ThanhToans.SingleAsync(x => x.DatVeId == booking.Id);
        booking.HetHanGiuCho = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        var callback = SignedUrl(Ipn, Notice(payment, TestTransactionNo(payment)));
        Assert.Equal("00", await AckAsync(webhook, callback));
        Assert.Equal("02", await AckAsync(webhook, callback));
        var refund = await db.HoanTiens.SingleAsync(x => x.ThanhToanId == payment.Id);
        Assert.Equal(payment.SoTien, refund.SoTien);
        Assert.False(await db.Ves.AnyAsync(x => x.DatVeId == booking.Id && x.TrangThai == TrangThaiVe.DaPhatHanh));

        using var first = await StaffAsync(manager, "hoan", refund.Id, refund.MaGiaoDich);
        using var second = await StaffAsync(manager, "hoan", refund.Id, refund.MaGiaoDich);
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Equal(1, external.RefundCalls);
        await db.Entry(refund).ReloadAsync();
        Assert.Equal(TrangThaiHoanTien.ChoXuLy, refund.TrangThai);
        Assert.Equal("ChuaXacMinh", refund.KetQuaNhaCungCap);
        Assert.NotNull(refund.NgayGuiCongThanhToan);
        var reference = refund.MaGiaoDich;
        var rejectionEvidence = "MERCHANT-REJECT-" + refund.Id;
        var refundEvidence = "MERCHANT-REFUND-" + refund.Id;
        using var blocked = await StaffAsync(manager, "lap-lai-lenh-hoan", refund.Id, reference);
        await db.Entry(refund).ReloadAsync();
        Assert.Equal(reference, refund.MaGiaoDich);

        using var wrongAmount = await RecordRefundAsync(manager, refund, "trace-incorrect", refund.SoTien - 1, "failed");
        Assert.False(await db.ChungTuDoiSoats.AnyAsync(e => e.HoanTienId == refund.Id));
        using var verified = await RecordRefundAsync(manager, refund, rejectionEvidence, refund.SoTien, "failed");
        await db.Entry(refund).ReloadAsync();
        Assert.Equal(TrangThaiHoanTien.ThatBai, refund.TrangThai);
        Assert.Equal("DoiSoatThatBai", refund.KetQuaNhaCungCap);
        using var prepare = await StaffAsync(manager, "lap-lai-lenh-hoan", refund.Id, reference);
        await db.Entry(refund).ReloadAsync();
        Assert.NotEqual(reference, refund.MaGiaoDich);
        Assert.Null(refund.NgayGuiCongThanhToan);
        Assert.Equal(reference, (await db.ChungTuDoiSoats.SingleAsync(e => e.HoanTienId == refund.Id)).MaLenh);

        using var stale = await StaffAsync(manager, "hoan", refund.Id, reference);
        Assert.Equal(1, external.RefundCalls);
        external.BadRefundSignature = false;
        external.RefundStatus = "05";
        using var pending = await StaffAsync(manager, "hoan", refund.Id, refund.MaGiaoDich);
        await db.Entry(refund).ReloadAsync();
        Assert.Equal(2, external.RefundCalls);
        Assert.Equal(TrangThaiHoanTien.ChoXuLy, refund.TrangThai);
        Assert.Equal("00/05", refund.KetQuaNhaCungCap);
        using var originalNumber = await RecordRefundAsync(manager, refund, "trace-original-no", refund.SoTien,
            "success", TestTransactionNo(payment));
        await db.Entry(refund).ReloadAsync();
        Assert.Equal(TrangThaiHoanTien.ChoXuLy, refund.TrangThai);
        using var manualSuccess = await RecordRefundAsync(manager, refund, refundEvidence, refund.SoTien,
            "success", $"93{payment.Id}");
        using var repeated = await RecordRefundAsync(manager, refund, refundEvidence, refund.SoTien,
            "success", $"93{payment.Id}");
        await db.Entry(refund).ReloadAsync();
        Assert.Equal(TrangThaiHoanTien.ThanhCong, refund.TrangThai);
        Assert.Equal($"93{payment.Id}", refund.MaGiaoDichNhaCungCap);
        Assert.Equal(2, await db.ChungTuDoiSoats.CountAsync(e => e.HoanTienId == refund.Id));
        Assert.False(await db.Ves.AnyAsync(x => x.DatVeId == booking.Id && x.TrangThai == TrangThaiVe.DaPhatHanh));
        var ledger = WebUtility.HtmlDecode(await manager.GetStringAsync($"{Operations}?tab=hoan&kenh=vnpay&q={rejectionEvidence}"));
        Assert.Contains(rejectionEvidence, ledger);
        Assert.Contains(refundEvidence, ledger);
        Assert.Contains("Đã hoàn", ledger);
    }

    [PostgresFact]
    public async Task PriorPaymentArrivingAfterRetryOnlyQueuesRefund_ActiveRetryIssuesTicket()
    {
        await using var host = await PostgresTestHost.CreateAsync(vnpay: Options());
        await using var db = Database();
        using var owner = host.CreateClient();
        using var webhook = host.CreateClient();
        var booking = await HoldAsync(host, db, owner);
        await StartAsync(owner, booking);
        var original = await db.ThanhToans.SingleAsync(x => x.DatVeId == booking.Id);
        var decline = Notice(original, "0");
        decline["vnp_ResponseCode"] = "24";
        decline["vnp_TransactionStatus"] = "02";
        Assert.Equal("00", await AckAsync(webhook, SignedUrl(Ipn, decline)));
        await StartAsync(owner, booking);
        var retry = await db.ThanhToans.SingleAsync(x => x.DatVeId == booking.Id && x.Id != original.Id);
        Assert.Equal("00", await AckAsync(webhook, SignedUrl(Ipn, Notice(original, TestTransactionNo(original)))));
        Assert.True(await db.HoanTiens.AnyAsync(x => x.ThanhToanId == original.Id));
        Assert.False(await db.Ves.AnyAsync(x => x.DatVeId == booking.Id && x.TrangThai == TrangThaiVe.DaPhatHanh));
        Assert.Equal("00", await AckAsync(webhook, SignedUrl(Ipn, Notice(retry, TestTransactionNo(retry)))));
        Assert.True(await db.Ves.AnyAsync(x => x.DatVeId == booking.Id && x.TrangThai == TrangThaiVe.DaPhatHanh));
        var confirmation = await owner.GetStringAsync($"/dat-ve/thanh-cong?pnr={booking.MaDatVe}");
        Assert.Contains(retry.MaGiaoDich!, confirmation);
        Assert.DoesNotContain(original.MaGiaoDich!, confirmation);
    }

    [PostgresFact]
    public async Task StaffQueryAndApprovedPartialRefund_UpdatePaymentBookingAndRequest()
    {
        var external = new FakeApi();
        await using var host = await PostgresTestHost.CreateAsync(vnpay: Options(), vnpayHandler: external);
        await using var db = Database();
        using var owner = host.CreateClient();
        using var manager = await ManagerAsync(host, db);
        var booking = await HoldAsync(host, db, owner);
        await StartAsync(owner, booking);
        var payment = await db.ThanhToans.SingleAsync(x => x.DatVeId == booking.Id);
        external.PaidAmount = (long)(payment.SoTien * 100);
        external.PaidTransactionNo = TestTransactionNo(payment);
        using var denied = await owner.GetAsync(Operations);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        using var queried = await StaffAsync(manager, "truy-van", payment.Id);
        Assert.Equal(HttpStatusCode.Redirect, queried.StatusCode);
        await db.Entry(payment).ReloadAsync();
        Assert.Equal(TrangThaiThanhToan.ThanhCong, payment.TrangThai);
        using var customer = host.CreateClient(AppRoles.KhachHang, booking.EmailLienHe, booking.KhachHangId);
        using var requested = await SubmitAsync(customer, "/tra-cuu", "/ve-cua-toi/yeu-cau-hoan",
            new() { ["pnr"] = booking.MaDatVe, ["reason"] = "VNPAY sandbox demo" });
        Assert.Equal(HttpStatusCode.Redirect, requested.StatusCode);
        var request = await db.YeuCauDoiHoans.SingleAsync(x => x.DatVeId == booking.Id);
        using var approved = await SubmitAsync(manager, "/tra-cuu", "/quan-ly/doi-hoan/duyet",
            new() { ["yeu-cau-id"] = request.Id.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, approved.StatusCode);
        var refund = await db.HoanTiens.SingleAsync(x => x.YeuCauDoiHoanId == request.Id);
        Assert.InRange(refund.SoTien, 1m, payment.SoTien - 1);
        using var mockCompletion = await SubmitAsync(manager, "/tra-cuu", "/quan-ly/doi-hoan/hoan-tat-demo",
            new() { ["id"] = request.Id.ToString(), ["confirmed"] = "true" });
        await db.Entry(refund).ReloadAsync();
        Assert.Equal(TrangThaiHoanTien.ChoXuLy, refund.TrangThai);
        using var sent = await StaffAsync(manager, "hoan", refund.Id, refund.MaGiaoDich);
        Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
        Assert.Equal("03", external.LastRefundType);
        await db.Entry(refund).ReloadAsync();
        await db.Entry(request).ReloadAsync();
        await db.Entry(booking).ReloadAsync();
        Assert.Equal(TrangThaiHoanTien.ThanhCong, refund.TrangThai);
        Assert.Equal(TrangThaiYeuCau.HoanTat, request.TrangThai);
        Assert.Equal(TrangThaiDatVe.HoanTien, booking.TrangThai);
        Assert.Equal(TrangThaiVe.DaHoanTien, (await db.Ves.AsNoTracking().SingleAsync(x => x.DatVeId == booking.Id)).TrangThai);
    }

    [PostgresFact]
    public async Task VietQrRequiresMatchingUniqueEvidence_AndRefundsLateAndApprovedPayments()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        using var owner = host.CreateClient();
        using var secondOwner = host.CreateClient();
        using var manager = await ManagerAsync(host, db);
        var booking = await HoldAsync(host, db, owner);
        var lateBooking = await HoldAsync(host, db, secondOwner, "3B");
        using var firstTransfer = await SubmitAsync(owner, Checkout, Checkout, new() { ["payment"] = "transfer" });
        using var secondTransfer = await SubmitAsync(secondOwner, Checkout, Checkout, new() { ["payment"] = "transfer" });
        var firstPayment = await db.ThanhToans.SingleAsync(p => p.DatVeId == booking.Id);
        var latePayment = await db.ThanhToans.SingleAsync(p => p.DatVeId == lateBooking.Id);
        var receipt = $"QR-COLLECT-{firstPayment.Id}";
        var lateReturnReceipt = $"QR-RETURN-{latePayment.Id}";
        var returnReceipt = $"QR-RETURN-{firstPayment.Id}";
        Assert.Equal("VietQR", firstPayment.NhaCungCap);
        lateBooking.HetHanGiuCho = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        using var legacy = await SubmitAsync(manager, Operations, "/quan-ly/dat-ve/xac-nhan-thanh-toan",
            new() { ["pnr"] = booking.MaDatVe });
        Assert.Contains("kenh=qr", legacy.Headers.Location?.OriginalString);
        var createdDay = firstPayment.NgayTao.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var filters = $"?q={booking.MaDatVe}&kenh=qr&trangThai=waiting&ngay={createdDay}";
        using var mismatch = await SubmitAsync(manager, Operations + filters, $"{Operations}/doi-soat-thu{filters}", new()
        {
            ["id"] = firstPayment.Id.ToString(), ["reference"] = firstPayment.MaGiaoDich!, ["receipt"] = receipt,
            ["amount"] = (firstPayment.SoTien + 1).ToString(CultureInfo.InvariantCulture)
        });
        Assert.Equal(HttpStatusCode.Redirect, mismatch.StatusCode);
        var location = mismatch.Headers.Location!.OriginalString;
        Assert.Contains($"q={booking.MaDatVe}", location);
        Assert.Contains("kenh=qr", location);
        Assert.Contains("trangThai=waiting", location);
        Assert.Contains($"ngay={createdDay}", location);
        Assert.EndsWith($"#doi-soat-thu-{firstPayment.Id}", location);
        var formAfterError = WebUtility.HtmlDecode(await manager.GetStringAsync(location));
        Assert.Contains("Số tiền trên chứng từ phải khớp", formAfterError);
        Assert.Contains($"value=\"{receipt}\"", formAfterError);
        Assert.Contains("aria-invalid=\"true\"", formAfterError);
        Assert.Equal(TrangThaiThanhToan.ChoXuLy, (await db.ThanhToans.AsNoTracking().SingleAsync(p => p.Id == firstPayment.Id)).TrangThai);

        using var matched = await RecordTransferAsync(manager, firstPayment, receipt, firstPayment.SoTien);
        using var repeated = await RecordTransferAsync(manager, firstPayment, receipt, firstPayment.SoTien);
        Assert.Equal(TrangThaiDatVe.DaXacNhan, (await db.DatVes.AsNoTracking().SingleAsync(b => b.Id == booking.Id)).TrangThai);
        Assert.Equal(1, await db.EmailDeliveryJobs.CountAsync(e => e.BookingId == booking.Id));
        using var reused = await RecordTransferAsync(manager, latePayment, $" {receipt.ToLowerInvariant()} ", latePayment.SoTien);
        Assert.Equal(TrangThaiThanhToan.ChoXuLy, (await db.ThanhToans.AsNoTracking().SingleAsync(p => p.Id == latePayment.Id)).TrangThai);

        using var late = await RecordTransferAsync(manager, latePayment, $"QR-LATE-{latePayment.Id}", latePayment.SoTien);
        var unusedRefund = await db.HoanTiens.SingleAsync(r => r.ThanhToanId == latePayment.Id);
        Assert.Equal(latePayment.SoTien, unusedRefund.SoTien);
        Assert.Null(unusedRefund.YeuCauDoiHoanId);
        using var reusedForRefund = await RecordRefundAsync(manager, unusedRefund, receipt, unusedRefund.SoTien, "success");
        Assert.False(await db.ChungTuDoiSoats.AnyAsync(e => e.HoanTienId == unusedRefund.Id));
        using var refunded = await RecordRefundAsync(manager, unusedRefund, lateReturnReceipt, unusedRefund.SoTien, "success");
        await db.Entry(unusedRefund).ReloadAsync();
        Assert.Equal(TrangThaiHoanTien.ThanhCong, unusedRefund.TrangThai);
        Assert.False(await db.Ves.AnyAsync(v => v.DatVeId == lateBooking.Id && v.TrangThai == TrangThaiVe.DaPhatHanh));

        using var customer = host.CreateClient(AppRoles.KhachHang, booking.EmailLienHe, booking.KhachHangId);
        using var requested = await SubmitAsync(customer, "/tra-cuu", "/ve-cua-toi/yeu-cau-hoan",
            new() { ["pnr"] = booking.MaDatVe, ["reason"] = "Hoàn VietQR demo" });
        var request = await db.YeuCauDoiHoans.SingleAsync(r => r.DatVeId == booking.Id);
        using var approved = await SubmitAsync(manager, "/tra-cuu", "/quan-ly/doi-hoan/duyet",
            new() { ["yeu-cau-id"] = request.Id.ToString() });
        var approvedRefund = await db.HoanTiens.SingleAsync(r => r.YeuCauDoiHoanId == request.Id);
        using var paid = await RecordRefundAsync(manager, approvedRefund, returnReceipt, approvedRefund.SoTien, "success");
        await db.Entry(request).ReloadAsync();
        Assert.Equal(TrangThaiYeuCau.HoanTat, request.TrangThai);
        Assert.Equal(TrangThaiVe.DaHoanTien, (await db.Ves.AsNoTracking().SingleAsync(v => v.DatVeId == booking.Id)).TrangThai);
        Assert.Equal(4, await db.ChungTuDoiSoats.CountAsync(e => e.ThanhToanId == firstPayment.Id || e.ThanhToanId == latePayment.Id));
        await db.Entry(approvedRefund).ReloadAsync();
        var day = approvedRefund.NgayHoanTat!.Value.ToOffset(TimeSpan.FromHours(7));
        var ledger = WebUtility.HtmlDecode(await manager.GetStringAsync(
            $"{Operations}?tab=hoan&kenh=qr&q={returnReceipt}&trangThai=done&ngay={day:yyyy-MM-dd}"));
        Assert.Contains(booking.MaDatVe, ledger);
        Assert.Contains(returnReceipt, ledger);
        Assert.DoesNotContain(lateReturnReceipt, ledger);
        var previousDay = await manager.GetStringAsync($"{Operations}?tab=hoan&kenh=qr&q={returnReceipt}&ngay={day.AddDays(-1):yyyy-MM-dd}");
        Assert.DoesNotContain($"{returnReceipt}</strong>", previousDay);
    }

    private static Task<HttpResponseMessage> StaffAsync(HttpClient staff, string action, int id, string? reference = null) =>
        SubmitAsync(staff, Operations, $"{Operations}/{action}", new()
        {
            ["id"] = id.ToString(), ["reference"] = reference ?? ""
        });

    private static async Task<HttpClient> ManagerAsync(PostgresTestHost host, AppDbContext db)
    {
        var login = "staff-" + Guid.NewGuid().ToString("N");
        var staff = new TaiKhoan
        {
            TenDangNhap = login, HoTen = "Test Manager", Email = login + "@example.test",
            VaiTro = VaiTro.Manager, MatKhauHash = "unused-in-test", NgayTao = DateTimeOffset.UtcNow
        };
        db.TaiKhoans.Add(staff);
        await db.SaveChangesAsync();
        return host.CreateClient(AppRoles.Manager, taiKhoanId: staff.Id);
    }

    private static Task<HttpResponseMessage> RecordTransferAsync(HttpClient staff, ThanhToan payment, string evidence, decimal amount) =>
        SubmitAsync(staff, Operations, $"{Operations}/doi-soat-thu", new()
        {
            ["id"] = payment.Id.ToString(), ["reference"] = payment.MaGiaoDich!, ["receipt"] = evidence,
            ["amount"] = amount.ToString(CultureInfo.InvariantCulture)
        });

    private static Task<HttpResponseMessage> RecordRefundAsync(HttpClient staff, HoanTien refund, string evidence, decimal amount,
        string result, string? refundNumber = null) =>
        SubmitAsync(staff, "/tra-cuu", $"{Operations}/doi-soat-hoan", new()
        {
            ["id"] = refund.Id.ToString(), ["reference"] = refund.MaGiaoDich!, ["receipt"] = evidence,
            ["amount"] = amount.ToString(CultureInfo.InvariantCulture), ["confirmed"] = "true",
            ["result"] = result, ["refundNumber"] = refundNumber ?? ""
        });

    private static async Task<DatVe> HoldAsync(PostgresTestHost host, AppDbContext db, HttpClient owner, string seat = "3A")
    {
        var flightId = await db.Ves.Where(x => x.DatVe.MaDatVe == host.Data.PnrA).Select(x => x.ChuyenBayId).SingleAsync();
        var identity = Guid.NewGuid().ToString("N")[..16];
        var email = $"vnpay-{identity}@example.test";
        const string path = "/dat-ve/hanh-khach";
        using var held = await SubmitAsync(owner, $"{path}?flightId={flightId}", path, new()
        {
            ["flightId"] = flightId.ToString(), ["seat"] = seat, ["baggage"] = "none", ["lastName"] = "NGUYEN",
            ["firstName"] = "DEMO", ["gender"] = "Nam", ["dateOfBirth"] = "01/01/1990",
            ["nationality"] = "Việt Nam", ["identityNumber"] = identity, ["email"] = email, ["phone"] = "0900000000"
        });
        Assert.Equal(Checkout, held.Headers.Location?.OriginalString);
        return await db.DatVes.SingleAsync(x => x.EmailLienHe == email);
    }

    private static async Task<Uri> StartAsync(HttpClient client, DatVe booking)
    {
        using var response = await SubmitAsync(client, Checkout, Checkout,
            new() { ["payment"] = "vnpay", ["expectedTotal"] = booking.TongTien.ToString(CultureInfo.InvariantCulture) });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return Assert.IsType<Uri>(response.Headers.Location);
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, string form, string target, Dictionary<string, string> fields)
    {
        var page = await client.GetStringAsync(form);
        fields["__RequestVerificationToken"] = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(fields["__RequestVerificationToken"]);
        return await client.PostAsync(target, new FormUrlEncodedContent(fields));
    }

    private static async Task<string?> AckAsync(HttpClient client, string url)
    {
        using var json = JsonDocument.Parse(await client.GetStringAsync(url));
        return json.RootElement.GetProperty("RspCode").GetString();
    }

    private static Dictionary<string, string> Notice(ThanhToan payment, string transactionNumber) => new()
    {
        ["vnp_TmnCode"] = Merchant, ["vnp_TxnRef"] = payment.MaGiaoDich!,
        ["vnp_Amount"] = ((long)(payment.SoTien * 100)).ToString(CultureInfo.InvariantCulture),
        ["vnp_TransactionNo"] = transactionNumber, ["vnp_ResponseCode"] = "00", ["vnp_TransactionStatus"] = "00",
        ["vnp_OrderInfo"] = "AeroViet demo"
    };
    private static string SignedUrl(string route, Dictionary<string, string> fields) =>
        $"{route}?{QueryData(fields)}&vnp_SecureHash={Sign(QueryData(fields))}";
    private static string TestTransactionNo(ThanhToan payment) => $"81{payment.Id}";
    private static string QueryData(Dictionary<string, string> fields) =>
        string.Join("&", fields.Where(x => x.Key != "vnp_SecureHash").OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));
    private static string Sign(string data) =>
        Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(TestKey), Encoding.UTF8.GetBytes(data)));
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(PostgresTestConnection.Require()).Options);
    private static VnpayOptions Options() => new()
    {
        Enabled = true, MerchantCode = Merchant, HashSecret = TestKey, PublicBaseUrl = "https://merchant.example.test"
    };

    private sealed class FakeApi : HttpMessageHandler
    {
        public bool BadRefundSignature { get; set; }
        public string RefundStatus { get; set; } = "00";
        public int RefundCalls { get; private set; }
        public long PaidAmount { get; set; }
        public string? PaidTransactionNo { get; set; }
        public string? LastRefundType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("https://sandbox.vnpayment.vn/merchant_webapi/api/transaction", request.RequestUri?.AbsoluteUri);
            var fields = (await request.Content!.ReadFromJsonAsync<Dictionary<string, string>>(ct))!;
            var isRefund = fields["vnp_Command"] == "refund";
            var requestOrder = isRefund
                ? "RequestId Version Command TmnCode TransactionType TxnRef Amount TransactionNo TransactionDate CreateBy CreateDate IpAddr OrderInfo"
                : "RequestId Version Command TmnCode TxnRef TransactionDate CreateDate IpAddr OrderInfo";
            Assert.Equal(Sign(SignedFields(fields, requestOrder)), fields["vnp_SecureHash"]);
            if (isRefund)
            {
                RefundCalls++;
                LastRefundType = fields["vnp_TransactionType"];
                Assert.Matches("^[a-f0-9]{32}$", fields["vnp_RequestId"]);
            }
            var result = new Dictionary<string, string>
            {
                ["vnp_ResponseId"] = Guid.NewGuid().ToString("N"), ["vnp_Command"] = fields["vnp_Command"],
                ["vnp_ResponseCode"] = "00", ["vnp_Message"] = "Test Signed Response", ["vnp_TmnCode"] = Merchant,
                ["vnp_TxnRef"] = fields["vnp_TxnRef"], ["vnp_Amount"] = isRefund ? fields["vnp_Amount"] : PaidAmount.ToString(),
                ["vnp_BankCode"] = "NCB", ["vnp_PayDate"] = "20261120161000",
                ["vnp_TransactionNo"] = isRefund ? $"91{fields["vnp_TransactionNo"]}" : PaidTransactionNo!,
                ["vnp_TransactionType"] = isRefund ? fields["vnp_TransactionType"] : "01",
                ["vnp_TransactionStatus"] = isRefund ? RefundStatus : "00", ["vnp_OrderInfo"] = fields["vnp_OrderInfo"]
            };
            var order = "ResponseId Command ResponseCode Message TmnCode TxnRef Amount BankCode PayDate TransactionNo TransactionType TransactionStatus OrderInfo";
            if (!isRefund) order += " PromotionCode PromotionAmount";
            result["vnp_SecureHash"] = BadRefundSignature && isRefund ? new string('0', 128) : Sign(SignedFields(result, order));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
        }
        private static string SignedFields(Dictionary<string, string> fields, string names) =>
            string.Join("|", names.Split(' ').Select(name => fields.GetValueOrDefault($"vnp_{name}") ?? ""));
    }
}
