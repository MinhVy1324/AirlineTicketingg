using System.Net;
using System.Text.RegularExpressions;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.Infrastructure.Tickets;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Tests;

public sealed class JourneyRulesTests
{
    [Fact]
    public void SamePersonCannotUseTwoDateFormatsToBypassDuplicateValidation()
    {
        var selection = new AirlineTicketing.Web.ViewModels.JourneySelection(1, null, new(2), "PhoThong");
        var form = new AirlineTicketing.Web.ViewModels.JourneyBookingForm { ContactName = "TEST", Email = "test@example.test", Phone = "0900000000" };
        foreach (var birth in new[] { "01/01/1990", "1990-01-01" })
            form.People.Add(new() { Person = new() { LastName = "NGUYEN", FirstName = "AN", DateOfBirth = birth,
                IdentityNumber = Guid.NewGuid().ToString("N")[..16] }, Seats = ["3A"], Baggage = ["none"] });
        var flight = new ChuyenBay { Id = 1, GioKhoiHanh = DateTimeOffset.UtcNow.AddDays(1), MayBay = new() { DangHoatDong = true }, TuyenBay = new() { DangKhaiThac = true } };
        JourneyValidation.Normalize(form);
        Assert.Contains("person-1-dateOfBirth", JourneyValidation.Validate(selection, form, [flight], DateTimeOffset.UtcNow).Keys);
    }

    [Fact]
    public void RevenueAllocationUsesBothLegsAndPreservesRoundingRemainder()
    {
        var booking = new DatVe { Ves = [new Ve { Id = 1, ChuyenBayId = 1, GiaVe = 2 }, new Ve { Id = 2, ChuyenBayId = 2, GiaVe = 1 }] };
        var parts = JourneyAmounts.Allocate(booking, 100);
        Assert.Equal(66.67m, parts[0].Amount); Assert.Equal(33.33m, parts[1].Amount);
        Assert.Equal(100m, parts.Sum(x => x.Amount));
    }

    [Theory]
    [InlineData(0, 0, 0)] [InlineData(1, 0, 2)] [InlineData(9, 1, 0)]
    [InlineData(1, -1, 0)] [InlineData(int.MaxValue, int.MaxValue, 0)]
    public void InvalidPartyIsRejected(int adults, int children, int infants) =>
        Assert.Throws<ArgumentException>(() => new PassengerParty(adults, children, infants).Validate());

    [Theory]
    [InlineData("PhoThong", 2_750_000)] [InlineData("ThuongGia", 4_230_000)]
    public void FamilyPriceIncludesFeesOnlyForSeatedPassengers(string cabin, int expected) =>
        Assert.Equal(expected, JourneyRules.PartyTotal(1_000_000, new ChinhSachDatVe { Tax = 380_000, ServiceFee = 70_000 },
            JourneyRules.Cabin(cabin), new PassengerParty(1, 1, 1)));

    [Theory]
    [InlineData("2014-11-20", LoaiHanhKhach.NguoiLon, true)]
    [InlineData("2014-11-21", LoaiHanhKhach.NguoiLon, false)]
    [InlineData("2014-11-20", LoaiHanhKhach.TreEm, false)]
    [InlineData("2024-11-20", LoaiHanhKhach.TreEm, true)]
    [InlineData("2024-11-21", LoaiHanhKhach.TreEm, false)]
    [InlineData("2024-11-20", LoaiHanhKhach.EmBe, false)]
    [InlineData("2026-11-13", LoaiHanhKhach.EmBe, true)]
    [InlineData("2026-11-14", LoaiHanhKhach.EmBe, false)]
    [InlineData("9999-01-01", LoaiHanhKhach.EmBe, false)]
    public void AgeBoundaries(string birth, LoaiHanhKhach type, bool valid) =>
        Assert.Equal(valid, JourneyRules.ValidateAge(DateOnly.Parse(birth), new(2026, 11, 20), type) is null);
}

[Collection(PostgresCollection.Name)]
public sealed class JourneyBookingTests
{
    [PostgresFact]
    public async Task FamilyRoundTrip_HoldsAtomically_PricesByType_AndIssuesAllTicketsOnce()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var (outbound, inbound) = await FlightsAsync(db, host);
        using var client = host.CreateClient();
        var form = Family(outbound);
        form["People[0].Baggage[0]"] = "extra10";
        var passengerPage = await client.GetStringAsync(Url(outbound, inbound));
        Assert.Equal(2, Regex.Matches(passengerPage, "\\bdata-seat-grid\\b").Count);
        Assert.Contains("name=\"People[0].Seats[0]\"", passengerPage);
        Assert.Contains("name=\"People[1].Seats[1]\"", passengerPage);
        Assert.DoesNotContain("<select name=\"People[2].Seats[0]\"", passengerPage);
        var token = await Token(client, Url(outbound, inbound));
        form["__RequestVerificationToken"] = token;
        using var hold = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
        Assert.True(hold.StatusCode == HttpStatusCode.Redirect, Errors(await hold.Content.ReadAsStringAsync()));
        var booking = await db.DatVes.Include(x => x.Ves).SingleAsync(x => x.EmailLienHe == form["Email"]);
        var deadline = booking.HetHanGiuCho;
        Assert.Equal(6, booking.Ves.Count);
        Assert.Equal(4, booking.Ves.Count(x => x.SoGhe != ""));
        Assert.Equal(2, booking.Ves.Count(x => x.LoaiHanhKhach == LoaiHanhKhach.EmBe && x.NguoiDiKemId.HasValue && x.SoGhe == "" && x.ThuePhi == 0));
        Assert.All(booking.Ves, x => Assert.Equal("ThuongGia", x.HangVe));
        var policy = new ChinhSachDatVe();
        var expected = JourneyRules.PartyTotal(outbound.GiaCoBan, policy, JourneyRules.Business, new(1, 1, 1))
            + JourneyRules.PartyTotal(inbound.GiaCoBan, policy, JourneyRules.Business, new(1, 1, 1)) + 250_000;
        Assert.Equal(expected, booking.TongTien);
        using var repeatedHold = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, repeatedHold.StatusCode);
        Assert.Equal(1, await db.DatVes.CountAsync(x => x.EmailLienHe == form["Email"]));
        await db.Entry(booking).ReloadAsync();
        Assert.Equal(deadline, booking.HetHanGiuCho);
        var checkout = WebUtility.HtmlDecode(await client.GetStringAsync("/dat-ve/thanh-toan"));
        Assert.Contains(outbound.SoHieu, checkout); Assert.Contains(inbound.SoHieu, checkout); Assert.Contains("Em bé", checkout);
        var promotion = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client, "/dat-ve/thanh-toan"), ["code"] = "AERONEW"
        };
        using var applied = await client.PostAsync("/dat-ve/khuyen-mai", new FormUrlEncodedContent(promotion));
        Assert.Equal(HttpStatusCode.Redirect, applied.StatusCode);
        Assert.Contains("Ưu đãi AERONEW", WebUtility.HtmlDecode(await client.GetStringAsync("/dat-ve/thanh-toan")));
        var card = new Dictionary<string, string> { ["__RequestVerificationToken"] = await Token(client, "/dat-ve/thanh-toan"),
            ["payment"] = "card", ["cardNumber"] = "4242424242424242", ["cardHolder"] = "DEMO", ["cardExpiry"] = "12/28", ["cardCvv"] = "123",
            ["expectedTotal"] = (expected - 100_000m).ToString(System.Globalization.CultureInfo.InvariantCulture) };
        using var paid = await client.PostAsync("/dat-ve/thanh-toan", new FormUrlEncodedContent(card));
        using var paidAgain = await client.PostAsync("/dat-ve/thanh-toan", new FormUrlEncodedContent(card));
        Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode); Assert.Equal(paid.Headers.Location, paidAgain.Headers.Location);
        db.ChangeTracker.Clear();
        booking = await db.DatVes.Include(x => x.KhachHang).Include(x => x.ThanhToans).Include(x => x.Ves).ThenInclude(x => x.HanhKhach)
            .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .SingleAsync(x => x.Id == booking.Id);
        Assert.Equal(expected - 100_000m, Assert.Single(booking.ThanhToans).SoTien);
        Assert.Equal(100_000m, booking.GiamGia);
        Assert.Equal(6, booking.Ves.Select(x => x.MaVe).Distinct().Count());
        Assert.All(booking.Ves, x => Assert.Equal(TrangThaiVe.DaPhatHanh, x.TrangThai));
        var success = await client.GetStringAsync(paid.Headers.Location!);
        Assert.Contains(inbound.SoHieu, success);
        using var pdf = await client.GetAsync($"/ve/{booking.MaDatVe}/pdf"); Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Contains(inbound.SoHieu, VeEmailContent.TaoVanBan(booking, policy));
        using var stranger = host.CreateClient();
        using var denied = await stranger.GetAsync($"/ve/{booking.MaDatVe}/pdf"); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var account = host.CreateClient("Customer", form["Email"], booking.KhachHangId);
        Assert.Contains(inbound.SoHieu, await account.GetStringAsync("/ve-cua-toi?pnr=" + booking.MaDatVe));
        using var change = await account.GetAsync("/ve-cua-toi/yeu-cau-doi?pnr=" + booking.MaDatVe);
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);
        var lookup = new Dictionary<string, string> { ["__RequestVerificationToken"] = await Token(stranger, "/tra-cuu"), ["Pnr"] = booking.MaDatVe, ["Email"] = form["Email"] };
        using var result = await stranger.PostAsync("/tra-cuu", new FormUrlEncodedContent(lookup));
        Assert.Contains(inbound.SoHieu, await result.Content.ReadAsStringAsync());

        const string refunds = "/quan-ly/doi-hoan";
        const string requestPath = "/ve-cua-toi/yeu-cau-hoan";
        var staff = new TaiKhoan
        {
            TenDangNhap = $"group-refund-{Guid.NewGuid():N}", HoTen = "Demo Manager", VaiTro = VaiTro.Manager,
            Email = $"group-refund-{Guid.NewGuid():N}@example.test", MatKhauHash = "unused-in-test"
        };
        db.TaiKhoans.Add(staff);
        await db.SaveChangesAsync();
        using var manager = host.CreateClient(AppRoles.Manager, taiKhoanId: staff.Id);
        using var requested = await account.PostAsync(requestPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(account, $"{requestPath}?pnr={booking.MaDatVe}"),
            ["pnr"] = booking.MaDatVe
        }));
        Assert.Equal(HttpStatusCode.Redirect, requested.StatusCode);
        var request = await db.YeuCauDoiHoans.SingleAsync(r => r.DatVeId == booking.Id);
        Assert.Equal(booking.TongTien - policy.RefundFee - booking.PhiDichVu + booking.GiamGia - booking.PhiHanhLy, request.SoTienHoan);
        var managerToken = await Token(manager, $"{refunds}?id={request.Id}");
        using var approved = await manager.PostAsync($"{refunds}/duyet", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = managerToken, ["yeu-cau-id"] = request.Id.ToString()
        }));
        Assert.Equal(HttpStatusCode.Redirect, approved.StatusCode);
        using var completed = await manager.PostAsync($"{refunds}/hoan-tat-demo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = managerToken, ["id"] = request.Id.ToString(), ["confirmed"] = "true"
        }));
        Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
        var statuses = await db.Ves.Where(v => v.DatVeId == booking.Id).Select(v => v.TrangThai).ToListAsync();
        Assert.Equal(6, statuses.Count);
        Assert.All(statuses, status => Assert.Equal(TrangThaiVe.DaHoanTien, status));
        var seats = new AirlineTicketing.DAL.Repositories.ChuyenBayDAL(db);
        foreach (var ticket in booking.Ves.Where(v => v.SoGhe != ""))
            Assert.False((await seats.GetSeatStatesAsync(ticket.ChuyenBayId)).ContainsKey(ticket.SoGhe));
    }

    [PostgresFact]
    public async Task BadReturnSeat_RollsBackWholeGroup_ThenValidSeatSucceeds()
    {
        await using var host = await PostgresTestHost.CreateAsync(); await using var db = Database();
        var (outbound, inbound) = await FlightsAsync(db, host); using var client = host.CreateClient();
        var form = Family(outbound); form["People[1].Seats[1]"] = "3A";
        form["__RequestVerificationToken"] = await Token(client, Url(outbound, inbound));
        using var invalid = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.False(await db.DatVes.AnyAsync(x => x.EmailLienHe == form["Email"]));
        Assert.False(await db.Ves.AnyAsync(x => x.ChuyenBayId == inbound.Id));
        form["People[1].Seats[1]"] = "3B";
        using var valid = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
        Assert.True(valid.StatusCode == HttpStatusCode.Redirect, Errors(await valid.Content.ReadAsStringAsync()));
    }

    [PostgresFact]
    public async Task OneWayGroupAndSingleBusiness_DoNotFallBackToSingleEconomy()
    {
        await using var host = await PostgresTestHost.CreateAsync(); await using var db = Database();
        var (outbound, _) = await FlightsAsync(db, host);
        foreach (var adults in new[] { 2, 1 })
        {
            using var client = host.CreateClient();
            var form = Family(outbound);
            foreach (var key in form.Keys.Where(x => x.StartsWith("People[2]") || x.Contains(".Seats[1]") || x.Contains(".Baggage[1]" ) || adults == 1 && x.StartsWith("People[1]")).ToArray()) form.Remove(key);
            if (adults == 2) form["People[1].Person.DateOfBirth"] = form["People[0].Person.DateOfBirth"];
            form["People[1].Person.IdentityNumber"] = Guid.NewGuid().ToString("N")[..16];
            if (adults == 1) form.Remove("People[1].Person.IdentityNumber");
            form["People[0].Seats[0]"] = adults == 1 ? "4A" : "3A";
            var cabin = adults == 1 ? "ThuongGia" : "PhoThong";
            form["__RequestVerificationToken"] = await Token(client, $"/dat-ve/hanh-khach?flightId={outbound.Id}&adults={adults}&cabin={cabin}");
            using var response = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
            Assert.True(response.StatusCode == HttpStatusCode.Redirect, Errors(await response.Content.ReadAsStringAsync()));
            var booking = await db.DatVes.Include(x => x.Ves).SingleAsync(x => x.EmailLienHe == form["Email"]);
            Assert.Equal(adults, booking.Ves.Count); Assert.All(booking.Ves, ticket => Assert.Equal(cabin, ticket.HangVe));
        }
    }

    [PostgresFact]
    public async Task InvalidItineraryAndTamperedServicesNeverCreatePartialHold()
    {
        await using var host = await PostgresTestHost.CreateAsync(); await using var db = Database();
        var (outbound, inbound) = await FlightsAsync(db, host); using var client = host.CreateClient();
        var form = Family(outbound);
        form["__RequestVerificationToken"] = await Token(client, Url(outbound, outbound));
        using var route = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
        Assert.Contains("#journey", await route.Content.ReadAsStringAsync());
        form["__RequestVerificationToken"] = await Token(client, Url(outbound, inbound));
        form["People[0].Baggage[1]"] = "-999999";
        using var baggage = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
        Assert.Contains("#baggage-0-1", await baggage.Content.ReadAsStringAsync());
        form["People[0].Baggage[1]"] = "none"; form["People[2].Seats[0]"] = "3C";
        using var infant = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
        Assert.Contains("#seat-2-0", await infant.Content.ReadAsStringAsync());
        Assert.False(await db.DatVes.AnyAsync(x => x.EmailLienHe == form["Email"]));
        using var invalidCabin = await client.GetAsync($"/dat-ve/hanh-khach?flightId={outbound.Id}&cabin=admin");
        Assert.Equal(HttpStatusCode.BadRequest, invalidCabin.StatusCode);
    }

    [PostgresFact]
    public async Task RejectsAgeChangeOnReturn_AndUnderageInfantCompanion()
    {
        await using var host = await PostgresTestHost.CreateAsync(); await using var db = Database();
        var (outbound, inbound) = await FlightsAsync(db, host); using var client = host.CreateClient();
        var form = Family(outbound);
        var birthday = DateOnly.FromDateTime(FlightMapping.ToVietnamTime(inbound.GioKhoiHanh).DateTime).AddYears(-2);
        form["People[2].Person.DateOfBirth"] = birthday.ToString("dd/MM/yyyy");
        form["__RequestVerificationToken"] = await Token(client, Url(outbound, inbound));
        using var invalid = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
        Assert.Contains("#person-2-dateOfBirth", await invalid.Content.ReadAsStringAsync());
        form["People[2].Person.DateOfBirth"] = birthday.AddYears(1).ToString("dd/MM/yyyy");
        form["People[0].Person.DateOfBirth"] = birthday.AddYears(-15).ToString("dd/MM/yyyy");
        using var underage = await client.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
        Assert.Contains("#companion-2", await underage.Content.ReadAsStringAsync());
        Assert.False(await db.DatVes.AnyAsync(x => x.EmailLienHe == form["Email"]));
    }

    [PostgresFact]
    public async Task RoundTripSearch_PreservesPartyClassAndDates_AndReversesRoute()
    {
        await using var host = await PostgresTestHost.CreateAsync(); await using var db = Database();
        var (outbound, inbound) = await FlightsAsync(db, host); using var client = host.CreateClient();
        var date = FlightMapping.ToVietnamTime(outbound.GioKhoiHanh).ToString("yyyy-MM-dd");
        var back = FlightMapping.ToVietnamTime(inbound.GioKhoiHanh).ToString("yyyy-MM-dd");
        var url = $"/chuyen-bay?from={outbound.TuyenBay.SanBayDi.MaSanBay}&to={outbound.TuyenBay.SanBayDen.MaSanBay}&depart={date}&returnDate={back}&trip=roundtrip&adults=1&children=1&infants=1&cabin=ThuongGia";
        var outboundPage = WebUtility.HtmlDecode(await client.GetStringAsync(url));
        Assert.Contains("Chọn chiều đi", outboundPage); Assert.Contains("outboundId=" + outbound.Id, outboundPage);
        var returnPage = WebUtility.HtmlDecode(await client.GetStringAsync(url + "&outboundId=" + outbound.Id));
        Assert.Contains(inbound.SoHieu, returnPage); Assert.Contains("returnFlightId=" + inbound.Id, returnPage);
        Assert.Contains("children=1", returnPage); Assert.Contains("infants=1", returnPage);
        var bad = WebUtility.HtmlDecode(await client.GetStringAsync(url.Replace("adults=1", "adults=0")));
        Assert.Contains("Thông tin tìm chuyến chưa hợp lệ", bad);
    }

    [PostgresFact]
    public async Task FlightDisruption_ProtectsScheduleConfirmation_AndRebooksOnlyAffectedLeg()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var (outbound, inbound) = await FlightsAsync(db, host);
        using var checkout = host.CreateClient();
        var form = Family(outbound);
        form["__RequestVerificationToken"] = await Token(checkout, Url(outbound, inbound));
        using var hold = await checkout.PostAsync("/dat-ve/hanh-khach", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, hold.StatusCode);
        using var paid = await PostAsync(checkout, "/dat-ve/thanh-toan", "/dat-ve/thanh-toan", new()
        {
            ["payment"] = "card", ["cardNumber"] = "4242424242424242",
            ["cardHolder"] = "DEMO", ["cardExpiry"] = "12/28", ["cardCvv"] = "123"
        });
        Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode);
        var booking = await db.DatVes.Include(b => b.Ves).SingleAsync(x => x.EmailLienHe == form["Email"]);
        var originals = booking.Ves.ToDictionary(v => v.Id, v => (v.ChuyenBayId, v.MaVe, v.SoGhe));
        var originalDepart = outbound.GioKhoiHanh;
        var target = new ChuyenBay
        {
            SoHieu = $"RV{Guid.NewGuid():N}"[..10], TuyenBayId = outbound.TuyenBayId,
            MayBayId = outbound.MayBayId, HangHangKhongId = outbound.HangHangKhongId,
            GioKhoiHanh = outbound.GioKhoiHanh.AddDays(2), GioHaCanh = outbound.GioHaCanh.AddDays(2),
            GiaCoBan = outbound.GiaCoBan + 1_000_000m
        };
        db.ChuyenBays.Add(target);
        await db.SaveChangesAsync();
        using var owner = host.CreateClient(AppRoles.KhachHang, booking.EmailLienHe, booking.KhachHangId);
        using var outsider = host.CreateClient(AppRoles.KhachHang, khachHangId: host.Data.CustomerBId);
        using var manager = host.CreateClient(AppRoles.Manager);
        const string schedule = "/quan-ly/lich-bay";
        const string desk = "/quan-ly/bien-dong-chuyen-bay";
        var ticketsPage = $"/ve-cua-toi?pnr={booking.MaDatVe}";
        var pdfUrl = $"/ve/{booking.MaDatVe}/pdf";
        var originalTotal = booking.TongTien;

        Dictionary<string, string> ScheduleAt(DateTimeOffset local) => new()
        {
            ["chuyen-bay-id"] = outbound.Id.ToString(),
            ["ngay-bay"] = local.ToString("dd/MM/yyyy"),
            ["gio-bay"] = $"{local:HH:mm} / {local.AddMinutes(130):HH:mm}",
            ["trang-thai"] = "Mở bán",
            ["gia-pho-thong"] = outbound.GiaCoBan.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var late = FlightMapping.ToVietnamTime(inbound.GioKhoiHanh.AddDays(1));
        using var lateSchedule = await PostAsync(manager, "/tra-cuu", schedule, ScheduleAt(late));
        var invalid = await db.BienDongDatVes.SingleAsync(x => x.DatVeId == booking.Id);
        Assert.Equal(originalDepart, invalid.KhoiHanhCu);
        Assert.NotNull(invalid.KhoiHanhMoi);
        var invalidPath = $"/ve-cua-toi/bien-dong/{invalid.Id}/chap-nhan";
        using var cannotAccept = await PostAsync(owner, "/tra-cuu", invalidPath, new() { ["pnr"] = booking.MaDatVe });
        Assert.Null((await db.BienDongDatVes.AsNoTracking().SingleAsync(x => x.Id == invalid.Id)).NgayXuLy);
        using var blockedPdf = await owner.GetAsync(pdfUrl);
        Assert.Equal(HttpStatusCode.NotFound, blockedPdf.StatusCode);

        var valid = ScheduleAt(FlightMapping.ToVietnamTime(originalDepart.AddDays(1)));
        using var corrected = await PostAsync(manager, "/tra-cuu", schedule, valid);
        using var duplicate = await PostAsync(manager, "/tra-cuu", schedule, valid);
        Assert.Equal(2, await db.BienDongDatVes.CountAsync(x => x.DatVeId == booking.Id));
        var current = await db.BienDongDatVes.SingleAsync(x => x.DatVeId == booking.Id && x.NgayXuLy == null);
        var currentPath = $"/ve-cua-toi/bien-dong/{current.Id}/chap-nhan";
        Assert.Contains(currentPath, await owner.GetStringAsync(ticketsPage));
        using var stale = await PostAsync(owner, "/tra-cuu", invalidPath, new() { ["pnr"] = booking.MaDatVe });
        using var foreign = await PostAsync(outsider, "/tra-cuu", currentPath, new());
        Assert.Null((await db.BienDongDatVes.AsNoTracking().SingleAsync(x => x.Id == current.Id)).NgayXuLy);
        using var accepted = await PostAsync(owner, ticketsPage, currentPath, new() { ["pnr"] = booking.MaDatVe });
        await db.Entry(current).ReloadAsync();
        Assert.NotNull(current.NgayXuLy);
        using var scheduledPdf = await owner.GetAsync(pdfUrl);
        Assert.Equal(HttpStatusCode.OK, scheduledPdf.StatusCode);

        using var cancel = await PostAsync(manager, "/tra-cuu", schedule + "/huy-chuyen",
            new() { ["chuyen-bay-id"] = outbound.Id.ToString() });
        var cancellation = await db.BienDongDatVes.SingleAsync(x => x.DatVeId == booking.Id && x.NgayXuLy == null);
        var detail = $"{desk}?id={cancellation.Id}";
        var rebook = $"{desk}/{cancellation.Id}/chuyen-thay-the";
        Assert.Contains(rebook, await manager.GetStringAsync(detail));
        using var wrongRoute = await PostAsync(manager, detail, rebook, new()
        {
            ["flightId"] = inbound.Id.ToString(), ["confirmed"] = "true"
        });
        Assert.Equal(3, await db.Ves.CountAsync(x => x.DatVeId == booking.Id && x.ChuyenBayId == outbound.Id));
        using var moved = await PostAsync(manager, detail, rebook, new()
        {
            ["flightId"] = target.Id.ToString(), ["confirmed"] = "true"
        });
        await db.Entry(cancellation).ReloadAsync();
        Assert.NotNull(cancellation.NgayXuLy);
        var after = await db.Ves.AsNoTracking().Where(x => x.DatVeId == booking.Id).ToListAsync();
        var reissued = after.Where(x => originals[x.Id].ChuyenBayId == outbound.Id).ToList();
        Assert.Equal(3, reissued.Count);
        Assert.All(reissued, v => { Assert.Equal(target.Id, v.ChuyenBayId); Assert.NotEqual(originals[v.Id].MaVe, v.MaVe); });
        Assert.Equal(2, reissued.Where(v => v.SoGhe != "").Select(v => v.SoGhe).Distinct().Count());
        Assert.Equal("", Assert.Single(reissued, v => v.LoaiHanhKhach == LoaiHanhKhach.EmBe).SoGhe);
        Assert.All(after.Where(x => originals[x.Id].ChuyenBayId == inbound.Id),
            v => Assert.Equal(originals[v.Id], (v.ChuyenBayId, v.MaVe, v.SoGhe)));
        Assert.Equal(originalTotal, await db.DatVes.Where(x => x.Id == booking.Id).Select(x => x.TongTien).SingleAsync());
        Assert.Equal(1, await db.ThanhToans.CountAsync(x => x.DatVeId == booking.Id));
        using var newPdf = await owner.GetAsync(pdfUrl);
        Assert.Equal(HttpStatusCode.OK, newPdf.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string page, string path, Dictionary<string, string> form)
    {
        form["__RequestVerificationToken"] = await Token(client, page);
        return await client.PostAsync(path, new FormUrlEncodedContent(form));
    }

    private static string Errors(string html) => string.Join(" | ", Regex.Matches(WebUtility.HtmlDecode(html), "<a class=\"av-link\" href=\"#([^\"]+)\">([^<]+)</a>".Replace("\"", "\"")).Select(x => x.Value));
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(PostgresTestConnection.Require()).Options);
    private static async Task<(ChuyenBay Out, ChuyenBay Back)> FlightsAsync(AppDbContext db, PostgresTestHost host)
    {
        var outbound = await db.ChuyenBays.Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDi).Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .SingleAsync(x => x.Ves.Any(v => v.DatVe.MaDatVe == host.Data.PnrA));
        var inbound = new ChuyenBay { SoHieu = "RT" + Guid.NewGuid().ToString("N")[..6],
            TuyenBay = new TuyenBay { SanBayDiId = outbound.TuyenBay.SanBayDenId, SanBayDenId = outbound.TuyenBay.SanBayDiId, ThoiLuongDuKienPhut = 130 },
            MayBayId = outbound.MayBayId, HangHangKhongId = outbound.HangHangKhongId, GioKhoiHanh = outbound.GioKhoiHanh.AddDays(5),
            GioHaCanh = outbound.GioHaCanh.AddDays(5), GiaCoBan = 1_100_000 };
        db.ChuyenBays.Add(inbound); await db.SaveChangesAsync(); return (outbound, inbound);
    }
    private static string Url(ChuyenBay outbound, ChuyenBay inbound) => $"/dat-ve/hanh-khach?flightId={outbound.Id}&returnFlightId={inbound.Id}&adults=1&children=1&infants=1&cabin=ThuongGia";
    private static Dictionary<string, string> Family(ChuyenBay outbound)
    {
        var id = Guid.NewGuid().ToString("N");
        var date = DateOnly.FromDateTime(FlightMapping.ToVietnamTime(outbound.GioKhoiHanh).DateTime);
        var form = new Dictionary<string, string> { ["ContactName"] = "NGUYEN BA", ["Email"] = $"family-{id}@example.test", ["Phone"] = "0900000000" };
        for (var i = 0; i < 3; i++)
        {
            var p = $"People[{i}]";
            form[p + ".Person.LastName"] = "NGUYEN"; form[p + ".Person.FirstName"] = new[] { "BA", "CON", "BE" }[i];
            form[p + ".Person.Gender"] = "Nam"; form[p + ".Person.Nationality"] = "Việt Nam";
            form[p + ".Person.DateOfBirth"] = date.AddYears(-new[] { 30, 8, 1 }[i]).ToString("dd/MM/yyyy");
            form[p + ".Person.IdentityNumber"] = i == 0 ? id[..16].ToUpperInvariant() : "";
            for (var leg = 0; leg < 2; leg++) { form[p + $".Seats[{leg}]"] = i == 2 ? "" : i == 0 ? "3A" : "3B"; form[p + $".Baggage[{leg}]"] = "none"; }
        }
        form["People[2].CompanionIndex"] = "0"; return form;
    }
    private static async Task<string> Token(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token); return token;
    }
}
