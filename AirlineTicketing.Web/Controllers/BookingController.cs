using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AirlineTicketing.Web.Controllers;

public class BookingController(AppDbContext dbContext) : Controller
{
    private const string KeyFlightId = "booking.flightId";
    private const string KeyPassenger = "booking.passenger";
    private const string KeyBaggageFee = "booking.baggageFee";
    private const string KeyBookingId = "booking.bookingId";

    [HttpGet("/dat-ve/hanh-khach")]
    public async Task<IActionResult> Passengers(int? flightId, CancellationToken cancellationToken)
    {
        var id = flightId ?? HttpContext.Session.GetInt32(KeyFlightId);
        if (id is null || id <= 0)
        {
            return Redirect("/chuyen-bay");
        }

        HttpContext.Session.SetInt32(KeyFlightId, id.Value);
        var model = await BuildBookingModelAsync(id.Value, LoadPassengerFromSession(), HttpContext.Session.GetInt32(KeyBaggageFee) ?? 0, cancellationToken);
        if (model is null)
        {
            return View("Unavailable", Unavailable("Không tìm thấy chuyến bay", "Chuyến bay bạn chọn không còn tồn tại hoặc chưa thể đặt."));
        }

        return View(model);
    }

    [HttpPost("/dat-ve/hanh-khach")]
    [ValidateAntiForgeryToken]
    public IActionResult Passengers(IFormCollection form)
    {
        var passenger = new PassengerFormModel
        {
            LastName = form["lastName"].ToString().Trim(),
            FirstName = form["firstName"].ToString().Trim(),
            Gender = form["gender"].ToString().Trim(),
            DateOfBirth = form["dateOfBirth"].ToString().Trim(),
            Nationality = form["nationality"].ToString().Trim(),
            IdentityNumber = form["identityNumber"].ToString().Trim(),
            Email = form["email"].ToString().Trim(),
            Phone = form["phone"].ToString().Trim()
        };

        if (int.TryParse(form["flightId"], out var fid) && fid > 0)
        {
            HttpContext.Session.SetInt32(KeyFlightId, fid);
        }

        decimal baggageFee = 0;
        if (decimal.TryParse(form["baggage"], out var bf)) baggageFee = Math.Max(0, bf);
        HttpContext.Session.SetInt32(KeyBaggageFee, (int)baggageFee);
        HttpContext.Session.SetString(KeyPassenger, JsonSerializer.Serialize(passenger));

        return RedirectToAction(nameof(Checkout));
    }

    [HttpGet("/dat-ve/hanh-khach/kiem-tra")]
    public async Task<IActionResult> ReviewPassengers(CancellationToken cancellationToken)
    {
        var id = HttpContext.Session.GetInt32(KeyFlightId);
        if (id is null) return Redirect("/chuyen-bay");
        var model = await BuildBookingModelAsync(id.Value, LoadPassengerFromSession(), HttpContext.Session.GetInt32(KeyBaggageFee) ?? 0, cancellationToken);
        return model is null ? Redirect("/chuyen-bay") : View("Passengers", model);
    }

    [HttpGet("/dat-ve/thanh-toan")]
    public async Task<IActionResult> Checkout(CancellationToken cancellationToken)
    {
        var id = HttpContext.Session.GetInt32(KeyFlightId);
        if (id is null) return Redirect("/chuyen-bay");
        var model = await BuildBookingModelAsync(id.Value, LoadPassengerFromSession(), HttpContext.Session.GetInt32(KeyBaggageFee) ?? 0, cancellationToken);
        return model is null ? Redirect("/chuyen-bay") : View(model);
    }

    [HttpPost("/dat-ve/thanh-toan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(IFormCollection form, CancellationToken cancellationToken)
    {
        var id = HttpContext.Session.GetInt32(KeyFlightId);
        if (id is null) return Redirect("/chuyen-bay");

        var passenger = LoadPassengerFromSession() ?? new PassengerFormModel();
        if (!string.IsNullOrWhiteSpace(form["email"])) passenger.Email = form["email"].ToString().Trim();
        if (!string.IsNullOrWhiteSpace(form["phone"])) passenger.Phone = form["phone"].ToString().Trim();

        var model = await BuildBookingModelAsync(id.Value, passenger, HttpContext.Session.GetInt32(KeyBaggageFee) ?? 0, cancellationToken);
        if (model is null) return Redirect("/chuyen-bay");

        try
        {
            var booking = await CreateBookingAsync(model, passenger, cancellationToken);
            HttpContext.Session.SetInt32(KeyBookingId, booking.Id);
            return RedirectToAction(nameof(Success), new { pnr = booking.MaDatVe });
        }
        catch (Exception)
        {
            return RedirectToAction(nameof(PaymentFailed));
        }
    }

    [HttpGet("/dat-ve/thanh-cong")]
    public async Task<IActionResult> Success(string? pnr, CancellationToken cancellationToken)
    {
        DatVe? booking = null;
        if (!string.IsNullOrWhiteSpace(pnr))
        {
            booking = await LoadBookingAsync(pnr, cancellationToken);
        }
        if (booking is null)
        {
            var bid = HttpContext.Session.GetInt32(KeyBookingId);
            if (bid is not null)
            {
                booking = await dbContext.DatVes.AsNoTracking()
                    .Include(x => x.KhachHang).Include(x => x.ThanhToans)
                    .Include(x => x.Ves).ThenInclude(x => x.HanhKhach)
                    .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
                    .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
                    .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.MayBay)
                    .FirstOrDefaultAsync(x => x.Id == bid.Value, cancellationToken);
            }
        }

        if (booking is null) return Redirect("/ve-cua-toi");
        return View(ToBookingViewModel(booking));
    }

    [HttpGet("/dat-ve/het-cho")]
    public IActionResult SoldOut() => View("Unavailable", Unavailable("Chuyến bay vừa hết chỗ", "Chỗ bạn chọn đã được giữ bởi hành khách khác. Vui lòng chọn chuyến bay khác."));

    [HttpGet("/dat-ve/that-bai")]
    public IActionResult PaymentFailed() => View(Outcome("Thanh toán chưa thành công", "Đặt chỗ chưa được xác nhận. Bạn có thể thử lại hoặc chọn phương thức khác.", "failure"));

    [HttpGet("/dat-ve/het-han")]
    public IActionResult Expired() => View(Outcome("Đặt chỗ đã hết hạn", "Thời gian giữ chỗ đã kết thúc. Vui lòng tìm chuyến bay và đặt lại.", "warning"));

    [HttpGet("/dat-ve/dang-xuat-ve")]
    public IActionResult Processing() => View(Outcome("Đang xử lý", "Yêu cầu của bạn đang được xử lý. Vui lòng kiểm tra lại trong Vé của tôi.", "info"));

    private async Task<BookingViewModel?> BuildBookingModelAsync(int flightId, PassengerFormModel? passenger, int baggageFee, CancellationToken ct)
    {
        var chuyenBayBus = FlightServices.ChuyenBay(dbContext);
        var dto = await chuyenBayBus.LayChuyenBayAsync(flightId, ct);
        if (dto is null) return null;

        var offer = FlightMapping.ToOffer(dto);
        passenger ??= new PassengerFormModel();

        return new BookingViewModel
        {
            Flight = offer,
            Criteria = new FlightSearchCriteria
            {
                OriginCity = dto.ThanhPhoDi, OriginCode = dto.MaSanBayDi,
                DestinationCity = dto.ThanhPhoDen, DestinationCode = dto.MaSanBayDen,
                DepartDateText = FlightMapping.ToVietnamTime(dto.GioKhoiHanh).ToString("dd/MM/yyyy"),
                TripTypeText = "Một chiều", Passengers = 1, Cabin = "Phổ thông"
            },
            Price = new PriceSummary
            {
                Title = "Tổng tiền thanh toán",
                BaseFare = dto.GiaCoBan,
                Taxes = FlightMapping.TaxesPerPassenger,
                ServiceFee = FlightMapping.ServiceFeePerPassenger,
                BaggageFee = baggageFee,
                BaggageLabel = baggageFee > 0 ? "Hành lý thêm" : "Hành lý thêm"
            },
            Passenger = passenger,
            SeatCode = "Chưa chọn",
            SeatPosition = "",
            SeatRows = BuildSeatRows(),
            ExtraBaggage = baggageFee > 0 ? $"+{Money.Vnd(baggageFee)}" : "Không",
            PaymentMethod = "Thẻ ngân hàng",
            HoldUntilText = DateTimeOffset.UtcNow.AddHours(7).AddMinutes(15).ToString("HH:mm 'ngày' dd/MM/yyyy"),
            HoldRemainingText = "Còn 15 phút",
            CreatedAtText = DateTimeOffset.UtcNow.AddHours(7).ToString("HH:mm 'ngày' dd/MM/yyyy")
        };
    }

    private async Task<DatVe> CreateBookingAsync(BookingViewModel model, PassengerFormModel passenger, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var email = string.IsNullOrWhiteSpace(passenger.Email) ? $"guest.{Guid.NewGuid():N}@example.local" : passenger.Email.Trim();
        var phone = passenger.Phone?.Trim() ?? string.Empty;
        var fullName = passenger.FullName;
        if (string.IsNullOrWhiteSpace(fullName)) fullName = "Hành khách";

        var kh = await dbContext.KhachHangs.FirstOrDefaultAsync(x => x.Email.ToLower() == email.ToLower(), ct);
        if (kh is null)
        {
            kh = new KhachHang { HoTen = fullName, Email = email, SoDienThoai = phone, NgayTao = now };
            dbContext.KhachHangs.Add(kh);
            await dbContext.SaveChangesAsync(ct);
        }

        var hk = new HanhKhach
        {
            HoTen = fullName,
            QuocTich = passenger.Nationality,
            SoHoChieu = string.IsNullOrWhiteSpace(passenger.IdentityNumber) ? null : passenger.IdentityNumber.Trim(),
            NgaySinh = FlightMapping.TryParseDate(passenger.DateOfBirth, out var dob) ? dob : null
        };
        dbContext.HanhKhachs.Add(hk);
        await dbContext.SaveChangesAsync(ct);

        var pnr = GeneratePnr();
        while (await dbContext.DatVes.AnyAsync(x => x.MaDatVe == pnr, ct)) pnr = GeneratePnr();
        var maVe = $"738{now:yyMMddHHmm}{Random.Shared.Next(10, 99)}";

        var datVe = new DatVe
        {
            MaDatVe = pnr,
            KhachHangId = kh.Id,
            NgayDat = now,
            TrangThai = TrangThaiDatVe.DaXacNhan,
            TongTien = model.Price.Total,
            DonViTienTe = "VND",
            HanGiuCho = now.AddMinutes(15),
            NgayXacNhan = now,
            ThuePhi = model.Price.Taxes,
            PhiDichVu = model.Price.ServiceFee,
            PhiHanhLy = model.Price.BaggageFee,
            EmailLienHe = email,
            SoDienThoaiLienHe = phone,
            GhiChu = null
        };
        dbContext.DatVes.Add(datVe);
        await dbContext.SaveChangesAsync(ct);

        var flightDbId = int.TryParse(model.Flight.Id, out var parsedFlightId) ? parsedFlightId : 0;
        dbContext.Ves.Add(new Ve
        {
            MaVe = maVe,
            ChuyenBayId = flightDbId,
            DatVeId = datVe.Id,
            HanhKhachId = hk.Id,
            SoGhe = "Tự động",
            HangVe = model.Flight.FareName,
            GiaVe = model.Price.BaseFare,
            TrangThai = TrangThaiVe.DaPhatHanh,
            NgayPhatHanh = now,
            HanhLyXachTayKg = 10,
            HanhLyKyGuiKg = 23,
            PhiDoi = FlightMapping.ChangeFee,
            PhiHoan = FlightMapping.RefundFee
        });
        dbContext.ThanhToans.Add(new ThanhToan
        {
            DatVeId = datVe.Id,
            SoTien = model.Price.Total,
            DonViTienTe = "VND",
            PhuongThuc = PhuongThucThanhToan.TheNganHang,
            NhaCungCap = "NoiBo",
            TrangThai = TrangThaiThanhToan.ThanhCong,
            MaGiaoDich = $"TX{now:ddMMyyHHmmss}",
            NgayTao = now,
            NgayHoanTat = now
        });
        await dbContext.SaveChangesAsync(ct);

        return datVe;
    }

    private async Task<DatVe?> LoadBookingAsync(string pnr, CancellationToken ct) =>
        await dbContext.DatVes.AsNoTracking()
            .Include(x => x.KhachHang).Include(x => x.ThanhToans)
            .Include(x => x.Ves).ThenInclude(x => x.HanhKhach)
            .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
            .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.MayBay)
            .FirstOrDefaultAsync(x => x.MaDatVe == pnr, ct);

    private static BookingViewModel ToBookingViewModel(DatVe booking)
    {
        var ve = booking.Ves.OrderBy(x => x.Id).First();
        var f = ve.ChuyenBay;
        var dep = FlightMapping.ToVietnamTime(f.GioKhoiHanh);
        var arr = FlightMapping.ToVietnamTime(f.GioHaCanh);
        var paid = booking.ThanhToans.FirstOrDefault(t => t.TrangThai == TrangThaiThanhToan.ThanhCong);
        return new BookingViewModel
        {
            Flight = new FlightOffer(f.Id.ToString(), FlightMapping.AirlineNameFor(f.SoHieu), f.SoHieu,
                f.MayBay?.TenMayBay ?? "", string.IsNullOrWhiteSpace(ve.HangVe) ? "Phổ thông" : ve.HangVe,
                new AirportInfo(f.TuyenBay.SanBayDi.MaSanBay, f.TuyenBay.SanBayDi.ThanhPho, f.TuyenBay.SanBayDi.TenSanBay),
                new AirportInfo(f.TuyenBay.SanBayDen.MaSanBay, f.TuyenBay.SanBayDen.ThanhPho, f.TuyenBay.SanBayDen.TenSanBay),
                dep, arr, ve.GiaVe > 0 ? ve.GiaVe : f.GiaCoBan, "Bay thẳng",
                FlightMapping.CabinBaggage, FlightMapping.CheckedBaggage, FlightMapping.ChangeFee, FlightMapping.RefundFee),
            Criteria = new FlightSearchCriteria
            {
                OriginCity = f.TuyenBay.SanBayDi.ThanhPho, OriginCode = f.TuyenBay.SanBayDi.MaSanBay,
                DestinationCity = f.TuyenBay.SanBayDen.ThanhPho, DestinationCode = f.TuyenBay.SanBayDen.MaSanBay,
                DepartDateText = dep.ToString("dd/MM/yyyy"), Passengers = 1, Cabin = "Phổ thông"
            },
            Price = new PriceSummary
            {
                Title = "Tổng tiền thanh toán",
                BaseFare = ve.GiaVe,
                Taxes = Math.Max(0, booking.TongTien - ve.GiaVe - FlightMapping.ServiceFeePerPassenger),
                ServiceFee = FlightMapping.ServiceFeePerPassenger,
                BaggageFee = 0
            },
            Passenger = new PassengerFormModel
            {
                LastName = ve.HanhKhach.HoTen, FirstName = "",
                Email = booking.KhachHang.Email, Phone = booking.KhachHang.SoDienThoai,
                Nationality = ve.HanhKhach.QuocTich ?? "Việt Nam", IdentityNumber = ve.HanhKhach.SoHoChieu ?? ""
            },
            SeatCode = string.IsNullOrWhiteSpace(ve.SoGhe) ? "Tự động" : ve.SoGhe,
            Pnr = booking.MaDatVe,
            TicketNumber = ve.MaVe,
            TransactionCode = paid?.MaGiaoDich ?? "—",
            CreatedAtText = FlightMapping.ToVietnamTime(booking.NgayDat).ToString("HH:mm 'ngày' dd/MM/yyyy"),
            PaymentMethod = paid?.PhuongThuc.ToString() ?? "Thẻ ngân hàng"
        };
    }

    private PassengerFormModel? LoadPassengerFromSession()
    {
        var json = HttpContext.Session.GetString(KeyPassenger);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<PassengerFormModel>(json); } catch { return null; }
    }

    private static string GeneratePnr()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }

    private static IReadOnlyList<SeatRow> BuildSeatRows()
    {
        var rows = new List<SeatRow>();
        for (var r = 12; r <= 20; r++)
        {
            rows.Add(new SeatRow(r.ToString(), new[]
            {
                new SeatCell("A", SeatState.Free), new SeatCell("B", SeatState.Free), new SeatCell("C", SeatState.Free),
                new SeatCell("D", SeatState.Free), new SeatCell("E", SeatState.Free), new SeatCell("F", SeatState.Free)
            }));
        }
        return rows;
    }

    private static SoldOutViewModel Unavailable(string title, string text) => new()
    {
        Criteria = new FlightSearchCriteria { PriceRangeText = "—" },
        SelectedFlight = new FlightOffer("0", "—", "—", "", "—",
            new AirportInfo("---", title, ""), new AirportInfo("---", text, ""),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0m, "—", "—", "—", 0m, 0m),
        SelectedPrice = 0m,
        Alternatives = Array.Empty<FlightOffer>()
    };

    private static BookingOutcomeViewModel Outcome(string title, string text, string tone)
    {
        var now = DateTimeOffset.UtcNow.AddHours(7).ToString("HH:mm 'ngày' dd/MM/yyyy");
        var flight = new FlightOffer("0", "—", "—", "", "—",
            new AirportInfo("---", "—", "—"), new AirportInfo("---", "—", "—"),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0m, "Bay thẳng",
            FlightMapping.CabinBaggage, FlightMapping.CheckedBaggage, FlightMapping.ChangeFee, FlightMapping.RefundFee);
        return new BookingOutcomeViewModel
        {
            Booking = new BookingViewModel
            {
                Flight = flight,
                Criteria = new FlightSearchCriteria(),
                Price = new PriceSummary { BaseFare = 0, Taxes = 0, ServiceFee = 0, BaggageFee = 0 },
                Passenger = new PassengerFormModel(),
                Pnr = "—",
                CreatedAtText = now
            },
            UpdatedAtText = now,
            Statuses = new[] { new BookingStatusItem("Trạng thái", title) },
            EventsTitle = "Thông tin",
            Events = new[] { new OutcomeEvent(now, title, text) }
        };
    }
}
