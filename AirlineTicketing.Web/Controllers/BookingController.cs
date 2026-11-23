<<<<<<< Updated upstream
using AirlineTicketing.Web.Demo;
using Microsoft.AspNetCore.Mvc;

namespace AirlineTicketing.Web.Controllers;

public class BookingController : Controller
{
    [HttpGet("/dat-ve/hanh-khach")]
    public IActionResult Passengers() => View(DemoCatalog.Passengers());

    [HttpPost("/dat-ve/hanh-khach")]
    [ValidateAntiForgeryToken]
    public IActionResult Passengers(IFormCollection form) =>
        RedirectToAction(nameof(Checkout));

    [HttpGet("/dat-ve/hanh-khach/kiem-tra")]
    public IActionResult ReviewPassengers() => View("Passengers", DemoCatalog.ReviewPassengers());

    [HttpGet("/dat-ve/thanh-toan")]
    public IActionResult Checkout() => View(DemoCatalog.Checkout());

    [HttpPost("/dat-ve/thanh-toan")]
    [ValidateAntiForgeryToken]
    public IActionResult Checkout(IFormCollection form) =>
        RedirectToAction(nameof(Success));

    [HttpGet("/dat-ve/thanh-cong")]
    public IActionResult Success() => View(DemoCatalog.Success());
=======
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.Infrastructure.Payments;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using QRCoder;
using System.Text.Json;
using System.Security.Claims;
using System.Globalization;

namespace AirlineTicketing.Web.Controllers;

[AllowAnonymous]
public class BookingController(
    AppDbContext dbContext,
    CauHinhHeThongBUS cauHinhHeThong,
    BookingCheckoutService checkoutService,
    VnpayBookingService vnpay,
    IOptions<VnpayOptions> vnpayConfiguration,
    JourneyBookingBuilder journeys,
    DatVeBUS datVeBUS) : Controller
{
    private const string KeyFlightId = BookingSession.FlightId;
    private const string KeyPassenger = BookingSession.Passenger;
    private const string KeyBaggageCode = BookingSession.BaggageCode;
    private const string KeySeat = BookingSession.Seat;
    private const string KeyBookingId = BookingSession.BookingId;
    private const string ReportedTransferAction = "KhachBaoDaChuyenKhoan";

    private const int SeatsPerRow = 6;

    [HttpGet("/dat-ve/hanh-khach")]
    public async Task<IActionResult> Passengers(
        int? flightId,
        string? loi,
        CancellationToken cancellationToken,
        int? returnFlightId = null, int? adults = null, int? children = null, int? infants = null, string? cabin = null)
    {
        Response.Headers.CacheControl = "no-store";
        if (!ModelState.IsValid) return BadRequest("Thông tin hành trình không hợp lệ. Vui lòng chọn lại chuyến bay.");
        JourneySelection? selection;
        try
        {
            selection = flightId.HasValue
                ? new JourneySelection(flightId.Value, returnFlightId, new(adults ?? 1, children ?? 0, infants ?? 0), JourneyRules.Cabin(cabin).Code)
                : LoadJourney();
            selection?.Party.Validate();
        }
        catch (ArgumentException e) { return BadRequest(e.Message); }
        if (selection is not null)
        {
            if (LoadJourney() != selection) { HttpContext.Session.Remove(BookingSession.JourneyForm); HttpContext.Session.Remove(KeySeat); }
            HttpContext.Session.SetString(BookingSession.Journey, JsonSerializer.Serialize(selection));
            HttpContext.Session.SetInt32(KeyFlightId, selection.FlightId);
            if (!selection.IsLegacy)
            {
                var group = await journeys.BuildAsync(selection, LoadJourneyForm(), HttpContext.Session.GetInt32(KeyBookingId), cancellationToken);
                return group is null ? Redirect("/chuyen-bay") : View("JourneyPassengers", group);
            }
        }
        var id = flightId ?? HttpContext.Session.GetInt32(KeyFlightId);
        if (id is null || id <= 0)
        {
            return Redirect("/chuyen-bay");
        }

        if (HttpContext.Session.GetInt32(KeyFlightId) != id.Value) HttpContext.Session.Remove(KeySeat);
        HttpContext.Session.SetInt32(KeyFlightId, id.Value);
        var seat = HttpContext.Session.GetString(KeySeat);
        var model = await BuildBookingModelAsync(
            id.Value,
            LoadPassengerFromSession(),
            HttpContext.Session.GetString(KeyBaggageCode) ?? BaggageCatalog.None,
            seat,
            cancellationToken);
        if (model is null)
        {
            return Redirect("/chuyen-bay");
        }

        if (!string.IsNullOrWhiteSpace(seat) && model.SeatCode == "Chưa chọn")
        {
            HttpContext.Session.Remove(KeySeat);
            model.SeatError = $"Ghế {seat} vừa được hành khách khác chọn. Vui lòng chọn ghế khác.";
        }
        else if (loi == "chon-ghe")
        {
            model.SeatError = "Vui lòng chọn một chỗ ngồi trước khi sang bước thanh toán.";
        }
        else if (loi == "ghe-da-bi-chon")
        {
            model.SeatError = "Ghế bạn chọn vừa được hành khách khác giữ. Vui lòng chọn ghế khác.";
        }

        return View(model);
    }

    [HttpPost("/dat-ve/hanh-khach")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Passengers(IFormCollection form, CancellationToken cancellationToken)
    {
        var selection = LoadJourney();
        if (selection is { IsLegacy: false }) return await SubmitJourneyAsync(selection, cancellationToken);
        var passenger = new PassengerFormModel
        {
            LastName = form["lastName"].ToString().Trim(),
            FirstName = form["firstName"].ToString().Trim(),
            Gender = form["gender"].ToString().Trim(),
            DateOfBirth = form["dateOfBirth"].ToString().Trim(),
            Nationality = form["nationality"].ToString().Trim(),
            IdentityNumber = form["identityNumber"].ToString().Trim().ToUpperInvariant(),
            Email = form["email"].ToString().Trim().ToLowerInvariant(),
            Phone = form["phone"].ToString().Trim()
        };
        if (!int.TryParse(form["flightId"], out var flightId) || flightId <= 0)
            return BadRequest("Chuyến bay không hợp lệ.");
        var baggage = BaggageCatalog.Find(form["baggage"].ToString());
        var seatCode = form["seat"].ToString().Trim().ToUpperInvariant();
        HttpContext.Session.SetInt32(KeyFlightId, flightId);
        HttpContext.Session.SetString(KeyPassenger, JsonSerializer.Serialize(passenger));
        var model = await BuildBookingModelAsync(flightId, passenger,
            baggage?.Code ?? BaggageCatalog.None, seatCode, cancellationToken);
        if (model is null) return Redirect("/chuyen-bay");
        model.ValidationErrors = PassengerValidation.Validate(passenger,
            DateOnly.FromDateTime(model.Flight.Departure.DateTime));
        if (baggage is null)
            model.ValidationErrors["baggage"] = "Gói hành lý không hợp lệ. Vui lòng chọn lại.";
        if (model.SeatCode == "Chưa chọn")
            model.ValidationErrors["seat"] = "Vui lòng chọn một ghế còn trống.";
        if (model.ValidationErrors.Count > 0) return View(model);
        try
        {
            var hold = await checkoutService.HoldAsync(flightId, seatCode, passenger,
                baggage!.Code, HttpContext.Session.GetInt32(KeyBookingId), cancellationToken, RegisteredCustomerId());
            HttpContext.Session.SetInt32(KeyBookingId, hold.Id);
            HttpContext.Session.SetString(KeySeat, seatCode);
            HttpContext.Session.SetString(KeyBaggageCode, baggage.Code);
            return RedirectToAction(nameof(Checkout));
        }
        catch (BookingValidationException exception)
        {
            model.ValidationErrors[exception.Field] = exception.Message;
            return View(model);
        }
        catch (DbUpdateException exception) when (IsDuplicateSeat(exception) || IsDuplicatePassengerTicket(exception))
        {
            model.ValidationErrors["seat"] = "Chỗ hoặc hành khách vừa được đặt ở phiên khác. Vui lòng kiểm tra và chọn lại.";
            return View(model);
        }
    }

    [HttpGet("/dat-ve/hanh-khach/kiem-tra")]
    public async Task<IActionResult> ReviewPassengers(CancellationToken cancellationToken)
    {
        if (LoadJourney() is { IsLegacy: false }) return await Passengers(null, null, cancellationToken);
        var id = HttpContext.Session.GetInt32(KeyFlightId);
        if (id is null) return Redirect("/chuyen-bay");
        var model = await BuildBookingModelAsync(
            id.Value,
            LoadPassengerFromSession(),
            HttpContext.Session.GetString(KeyBaggageCode) ?? BaggageCatalog.None,
            HttpContext.Session.GetString(KeySeat),
            cancellationToken);
        return model is null ? Redirect("/chuyen-bay") : View("Passengers", model);
    }

    [HttpGet("/dat-ve/thanh-toan")]
    public async Task<IActionResult> Checkout(CancellationToken cancellationToken)
    {
        var booking = await CurrentBookingAsync(cancellationToken);
        if (booking is null) return RedirectToAction(nameof(Passengers));
        if (booking.TrangThai == TrangThaiDatVe.DaXacNhan)
            return RedirectToAction(nameof(Success), new { pnr = booking.MaDatVe });
        if (booking.TrangThai != TrangThaiDatVe.ChoThanhToan || booking.HetHanGiuCho <= DateTimeOffset.UtcNow)
            return RedirectToAction(nameof(Expired));
        Response.Headers.CacheControl = "no-store";
        return View(await BuildCheckoutAsync(booking, null, cancellationToken));
    }

    [HttpPost("/dat-ve/thanh-toan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(IFormCollection form, CancellationToken cancellationToken)
    {
        var booking = await CurrentBookingAsync(cancellationToken);
        if (booking is null) return RedirectToAction(nameof(Passengers));
        if (booking.TrangThai == TrangThaiDatVe.DaXacNhan)
            return RedirectToAction(nameof(Success), new { pnr = booking.MaDatVe });
        if (booking.TrangThai != TrangThaiDatVe.ChoThanhToan || booking.HetHanGiuCho <= DateTimeOffset.UtcNow)
            return RedirectToAction(nameof(Expired));
        Response.Headers.CacheControl = "no-store";
        var payment = form["payment"].ToString();
        var model = await BuildCheckoutAsync(booking, payment, cancellationToken);
        if (payment is not ("card" or "transfer" or "vnpay"))
        {
            model.PaymentError = "Vui lòng chọn phương thức thanh toán hợp lệ.";
            return View(model);
        }
        if (model.PendingPayment is not null && model.PendingPayment != payment)
        {
            model.PaymentError = "Đặt chỗ đã có giao dịch đang chờ. Hãy tiếp tục với phương thức đã chọn.";
            return View(model);
        }
        if ((form.ContainsKey("email") && !string.Equals(form["email"].ToString().Trim(),
                booking.EmailLienHe, StringComparison.OrdinalIgnoreCase))
            || (form.ContainsKey("phone") && form["phone"].ToString().Trim() != booking.SoDienThoaiLienHe))
        {
            model.PaymentError = "Thông tin liên hệ đã thay đổi. Vui lòng quay lại bước hành khách để xác nhận.";
            return View(model);
        }
        string brand = "Visa", last4 = "4242";
        if (payment == "card")
        {
            var error = ReadDemoCard(form, model, out brand, out last4);
            if (error is not null)
            {
                model.PaymentError = error;
                return View(model);
            }
        }
        try
        {
            decimal? expectedTotal = null;
            if (form.TryGetValue("expectedTotal", out var submittedTotal))
            {
                if (!decimal.TryParse(submittedTotal, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
                    throw new BookingValidationException("promotion", "Tổng tiền không hợp lệ. Vui lòng tải lại trang.");
                expectedTotal = amount;
            }
            if (payment == "vnpay")
            {
                var address = HttpContext.Connection.RemoteIpAddress;
                if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
                var customerIp = address is null || System.Net.IPAddress.IsLoopback(address)
                    ? "127.0.0.1" : address.ToString();
                return Redirect(await vnpay.StartAsync(booking.Id, expectedTotal, customerIp, cancellationToken));
            }
            var completed = await checkoutService.CompleteAsync(booking.Id, payment == "transfer", brand, last4, expectedTotal, cancellationToken);
            if (payment == "transfer")
                return RedirectToAction(nameof(Transfer), new { pnr = completed.MaDatVe });
            return RedirectToAction(nameof(Success), new { pnr = completed.MaDatVe });
        }
        catch (BookingValidationException exception)
        {
            if (exception.Field == "promotion")
            {
                TempData["PromotionError"] = exception.Message;
                return RedirectToAction(nameof(Checkout));
            }
            model.PaymentError = exception.Message;
            return View(model);
        }
    }

    private async Task<BookingViewModel> BuildCheckoutAsync(DatVe booking, string? payment, CancellationToken ct)
    {
        var model = ToBookingViewModel(booking, await cauHinhHeThong.LayChinhSachAsync(ct));
        var pending = booking.ThanhToans.FirstOrDefault(x => x.TrangThai == TrangThaiThanhToan.ChoXuLy);
        model.PendingPayment = pending?.NhaCungCap switch
        {
            VietQr.Provider => "transfer",
            VnpayOptions.Provider => "vnpay",
            _ => null
        };
        model.SelectedPayment = model.PendingPayment ?? (payment switch
        {
            "transfer" => "transfer",
            "card" => "card",
            "vnpay" when vnpayConfiguration.Value.Ready => "vnpay",
            _ => vnpayConfiguration.Value.Ready ? "vnpay" : "card"
        });
        return model;
    }

    [HttpPost("/dat-ve/khuyen-mai")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Promotion(string? code, bool remove, CancellationToken ct)
    {
        var bookingId = HttpContext.Session.GetInt32(KeyBookingId);
        if (!bookingId.HasValue) return RedirectToAction(nameof(Passengers));
        try
        {
            await checkoutService.ApplyPromotionAsync(bookingId.Value, code, remove, ct);
            TempData["PromotionMessage"] = remove ? "Đã bỏ mã ưu đãi." : "Đã áp dụng mã ưu đãi.";
        }
        catch (BookingValidationException exception)
        {
            TempData["PromotionError"] = exception.Message;
        }
        return RedirectToAction(nameof(Checkout));
    }

    [HttpGet("/dat-ve/chuyen-khoan/{pnr}")]
    public async Task<IActionResult> Transfer(string pnr, CancellationToken cancellationToken)
    {
        var booking = await LoadBookingAsync(pnr, cancellationToken);
        if (booking is null || !CanAccessBooking(booking)) return NotFound();

        if (booking.TrangThai == TrangThaiDatVe.DaXacNhan)
        {
            return RedirectToAction(nameof(Success), new { pnr });
        }

        if (booking.TrangThai != TrangThaiDatVe.ChoThanhToan)
        {
            return View("Expired", Outcome("Đặt chỗ không còn chờ thanh toán",
                "Đặt chỗ này đã được xử lý hoặc không còn hiệu lực. Vui lòng kiểm tra Vé của tôi.", "warning"));
        }

        var payment = PendingTransfer(booking);
        if (payment is null) return RedirectToAction(nameof(Checkout));

        var now = DateTimeOffset.UtcNow;
        if (booking.HetHanGiuCho <= now)
        {
            await ExpireBookingAsync(pnr, now, cancellationToken);
            return View("Expired", Outcome("Đã hết thời gian giữ chỗ",
                "Vui lòng tìm chuyến bay và đặt lại. Ghế đã được trả về sơ đồ.", "warning"));
        }

        Response.Headers.CacheControl = "no-store";
        var ve = booking.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var model = new QrPaymentViewModel
        {
            Journey = JourneyBookingBuilder.UsesJourney(booking) ? JourneyBookingBuilder.FromBooking(booking) : null,
            Pnr = booking.MaDatVe,
            Amount = payment.SoTien,
            Memo = $"AEROVIET {booking.MaDatVe}",
            HoldUntilText = FlightMapping.ToVietnamTime(booking.HetHanGiuCho)
                .ToString("HH:mm 'ngày' dd/MM/yyyy"),
            QrUrl = Url.Action(nameof(TransferQr), new { pnr }) ?? string.Empty,
            SeatCode = ve?.SoGhe ?? "—",
            FlightNumber = ve?.ChuyenBay?.SoHieu ?? "—",
            PassengerName = ve?.HanhKhach?.HoTen ?? "—",
            Reported = booking.LichSus.Any(x => x.HanhDong == ReportedTransferAction)
        };

        return View(model);
    }

    [HttpGet("/dat-ve/chuyen-khoan/{pnr}/qr.png")]
    public async Task<IActionResult> TransferQr(string pnr, CancellationToken cancellationToken)
    {
        var booking = await LoadBookingAsync(pnr, cancellationToken);
        if (booking is null || !CanAccessBooking(booking)) return NotFound();

        var payment = PendingTransfer(booking);
        if (booking.TrangThai != TrangThaiDatVe.ChoThanhToan || booking.HetHanGiuCho <= DateTimeOffset.UtcNow
            || payment is null)
            return NotFound();
        var payload = VietQr.BuildPayload(payment.SoTien, $"AEROVIET {booking.MaDatVe}");
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        var bytes = qr.GetGraphic(10);

        Response.Headers.CacheControl = "no-store, no-cache";
        return File(bytes, "image/png");
    }

    [HttpPost("/dat-ve/chuyen-khoan/{pnr}/xac-nhan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmTransfer(string pnr, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await BookingCheckoutService.LockAsync(dbContext, cancellationToken);
        var booking = await dbContext.DatVes
            .Include(x => x.KhachHang)
            .Include(x => x.Ves)
            .Include(x => x.ThanhToans)
            .Include(x => x.LichSus)
            .FirstOrDefaultAsync(x => x.MaDatVe == pnr, cancellationToken);

        if (booking is null || !CanAccessBooking(booking)) return NotFound();

        if (booking.TrangThai == TrangThaiDatVe.DaXacNhan)
        {
            return RedirectToAction(nameof(Success), new { pnr });
        }

        var now = DateTimeOffset.UtcNow;
        if (booking.TrangThai != TrangThaiDatVe.ChoThanhToan || booking.HetHanGiuCho <= now)
        {
            Expire(booking, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return View("Expired", Outcome("Đã hết thời gian giữ chỗ",
                "Giao dịch chuyển khoản không còn hiệu lực. Vui lòng đặt lại chuyến bay.", "warning"));
        }

        var payment = PendingTransfer(booking);

        if (payment is null)
        {
            return RedirectToAction(nameof(PaymentFailed));
        }

        // Không tự đánh dấu đã thanh toán: chỉ ghi nhận khách báo đã chuyển khoản.
        // Nhân viên đối soát sao kê ngân hàng rồi xác nhận ở khu quản lý.
        if (!booking.LichSus.Any(x => x.HanhDong == ReportedTransferAction))
        {
            booking.LichSus.Add(new LichSuDatVe
            {
                DatVeId = booking.Id,
                HanhDong = ReportedTransferAction,
                TrangThaiTruoc = booking.TrangThai.ToString(),
                TrangThaiSau = booking.TrangThai.ToString(),
                LyDo = "Khách báo đã chuyển khoản, chờ nhân viên đối soát",
                NguoiThucHien = User.Identity?.IsAuthenticated == true ? User.DisplayName() : "Khách vãng lai",
                ThoiDiem = now,
                DuLieuBoSung = JsonSerializer.Serialize(new { booking.MaDatVe, payment.MaGiaoDich })
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return RedirectToAction(nameof(Transfer), new { pnr });
    }

    [HttpGet("/dat-ve/thanh-cong")]
    public async Task<IActionResult> Success(string? pnr, CancellationToken cancellationToken)
    {
        var booking = !string.IsNullOrWhiteSpace(pnr)
            ? await LoadBookingAsync(pnr, cancellationToken)
            : await CurrentBookingAsync(cancellationToken);
        if (booking is null || !CanAccessBooking(booking)) return NotFound();
        if (booking.TrangThai != TrangThaiDatVe.DaXacNhan)
        {
            return RedirectToAction(nameof(Transfer), new { pnr = booking.MaDatVe });
        }

        if (FlightDisruptionService.NeedsAttention(booking))
            return View("Disrupted", booking.MaDatVe);

        var delivery = await dbContext.EmailDeliveryJobs.AsNoTracking()
            .Where(job => job.BookingId == booking.Id).OrderByDescending(job => job.CreatedAt)
            .Select(job => new { job.Delivered, job.CompletedAt }).FirstOrDefaultAsync(cancellationToken);
        if (delivery is not null)
        {
            ViewData["TicketDeliveryMessage"] = delivery.Delivered
                ? "Vé điện tử đã được gửi qua kênh email của hệ thống."
                : delivery.CompletedAt is null
                    ? "Email vé đang chờ gửi. Bạn có thể tải PDF ngay."
                    : "Email chưa gửi được. Bạn có thể gửi lại hoặc tải PDF.";
        }
        return View(JourneyBookingBuilder.UsesJourney(booking) ? "JourneySuccess" : "Success", ToBookingViewModel(booking, await cauHinhHeThong.LayChinhSachAsync(cancellationToken)));
    }
>>>>>>> Stashed changes

    [HttpGet("/dat-ve/het-cho")]
    public IActionResult SoldOut() => View("Unavailable", DemoCatalog.SoldOut());

    [HttpGet("/dat-ve/that-bai")]
    public IActionResult PaymentFailed() => View(DemoCatalog.PaymentFailed());

    [HttpGet("/dat-ve/het-han")]
    public IActionResult Expired() => View(DemoCatalog.Expired());

    [HttpGet("/dat-ve/dang-xuat-ve")]
<<<<<<< Updated upstream
    public IActionResult Processing() => View(DemoCatalog.Processing());
=======
    public IActionResult Processing() => View(Outcome("Đang xử lý", "Yêu cầu của bạn đang được xử lý. Vui lòng kiểm tra lại trong Vé của tôi.", "info"));

    private async Task<BookingViewModel?> BuildBookingModelAsync(
        int flightId,
        PassengerFormModel? passenger,
        string baggageCode,
        string? seatCode,
        CancellationToken ct)
    {
        await ReleaseExpiredHoldsAsync(ct);

        var chuyenBayBus = FlightServices.ChuyenBay(dbContext);
        var dto = await chuyenBayBus.LayChuyenBayAsync(flightId, ct);
        if (dto is null) return null;

        var chinhSach = await cauHinhHeThong.LayChinhSachAsync(ct);
        var offer = FlightMapping.ToOffer(dto,
            changeFee: chinhSach.ChangeFee, refundFee: chinhSach.RefundFee, policy: chinhSach);
        passenger ??= new PassengerFormModel();

        SoDoGheDTO seatMap;
        try
        {
            seatMap = await chuyenBayBus.LaySoDoGheAsync(flightId, ct);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        var baggage = BaggageCatalog.Find(baggageCode) ?? BaggageCatalog.Packages[0];
        var ownHold = await CurrentBookingAsync(ct);
        var heldSeat = ownHold is { TrangThai: TrangThaiDatVe.ChoThanhToan } && ownHold.HetHanGiuCho > DateTimeOffset.UtcNow
            ? ownHold.Ves.FirstOrDefault(x => x.ChuyenBayId == flightId)?.SoGhe : null;
        var selected = string.IsNullOrWhiteSpace(seatCode)
            ? null
            : seatMap.Ghes.FirstOrDefault(x =>
                string.Equals(x.SoGhe, seatCode, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(x.TrangThai, "Trong", StringComparison.OrdinalIgnoreCase) || x.SoGhe == heldSeat));

        var seatFee = selected?.PhuThu ?? 0;
        var seatTypeName = selected?.TenLoaiGhe ?? "Chưa chọn chỗ";

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
                Taxes = chinhSach.Tax,
                ServiceFee = chinhSach.ServiceFee,
                BaggageFee = baggage.Price,
                BaggageLabel = "Hành lý ký gửi bổ sung",
                SeatFee = seatFee,
                SeatLabel = selected is null
                    ? "Chỗ ngồi · chưa chọn"
                    : $"Chỗ ngồi · {selected.TenLoaiGhe} ({selected.SoGhe})"
            },
            Passenger = passenger,
            SeatCode = selected?.SoGhe ?? "Chưa chọn",
            SeatPosition = selected is null ? string.Empty : SeatMapPresentation.PositionFor(selected.SoGhe),
            SeatTypeName = seatTypeName,
            SeatRows = SeatMapPresentation.Build(seatMap, selected?.SoGhe),
            BaggageCode = baggage.Code, ExtraBaggage = baggage.Label,
            BaggageText = $"{BaggageCatalog.IncludedCheckedKg + baggage.ExtraKg} kg",
            PaymentMethod = "Thẻ ngân hàng",
            PaymentDisplayText = "Thẻ ngân hàng",
            Pnr = "—",
            HoldUntilText = DateTimeOffset.UtcNow.AddHours(7).Add(chinhSach.HoldDuration).ToString("HH:mm 'ngày' dd/MM/yyyy"),
            HoldRemainingText = $"Còn {chinhSach.HoldMinutes} phút",
            CreatedAtText = DateTimeOffset.UtcNow.AddHours(7).ToString("HH:mm 'ngày' dd/MM/yyyy")
        };
    }

    private Task<int> ReleaseExpiredHoldsAsync(CancellationToken ct) => datVeBUS.HetHanCacDatVeAsync(ct);

    private async Task<DatVe?> CurrentBookingAsync(CancellationToken ct)
    {
        var id = HttpContext.Session.GetInt32(KeyBookingId);
        return id.HasValue ? await BookingQuery().FirstOrDefaultAsync(x => x.Id == id.Value, ct) : null;
    }

    private IQueryable<DatVe> BookingQuery() => dbContext.DatVes.AsNoTracking().AsSplitQuery()
        .Include(x => x.KhachHang).Include(x => x.ThanhToans).ThenInclude(x => x.HoanTiens).Include(x => x.LichSus)
        .Include(x => x.BienDongs.Where(change => change.NgayXuLy == null))
        .Include(x => x.Ves).ThenInclude(x => x.HanhKhach)
        .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
        .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
        .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.MayBay);

    private bool CanAccessBooking(DatVe booking)
    {
        if (HttpContext.Session.GetInt32(KeyBookingId) == booking.Id) return true;
        if (User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager)) return true;
        if (User.KhachHangId() is int customerId && booking.KhachHangId == customerId) return true;
        var email = User.FindFirstValue(ClaimTypes.Email)?.Trim();
        return !string.IsNullOrWhiteSpace(email) && (string.Equals(email, booking.EmailLienHe, StringComparison.OrdinalIgnoreCase)
            || string.Equals(email, booking.KhachHang.Email, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ExpireBookingAsync(string pnr, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        await BookingCheckoutService.LockAsync(dbContext, ct);
        var booking = await dbContext.DatVes
            .Include(x => x.Ves)
            .FirstOrDefaultAsync(x => x.MaDatVe == pnr, ct);
        if (booking is null) return;

        Expire(booking, now);
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static void Expire(DatVe booking, DateTimeOffset now)
    {
        if (booking.TrangThai != TrangThaiDatVe.ChoThanhToan) return;

        booking.TrangThai = TrangThaiDatVe.HetHan;
        foreach (var ticket in booking.Ves.Where(x => x.TrangThai == TrangThaiVe.TamGiu))
        {
            ticket.TrangThai = TrangThaiVe.DaHuy;
            ticket.NgayHuy = now;
            ticket.LyDoHuy = "Hết thời hạn giữ chỗ";
        }
    }

    private async Task<DatVe?> LoadBookingAsync(string pnr, CancellationToken ct)
    {
        var booking = await BookingQuery().FirstOrDefaultAsync(x => x.MaDatVe == pnr.Trim().ToUpperInvariant(), ct);
        Response.Headers.CacheControl = "no-store";
        return booking is not null && CanAccessBooking(booking) ? booking : null;
    }

    private static BookingViewModel ToBookingViewModel(DatVe booking, ChinhSachDatVe chinhSach)
    {
        var journey = JourneyBookingBuilder.UsesJourney(booking) ? JourneyBookingBuilder.FromBooking(booking) : null;
        var ve = booking.Ves.OrderBy(x => x.Id).First();
        var f = ve.ChuyenBay;
        var dep = FlightMapping.ToVietnamTime(f.GioKhoiHanh);
        var arr = FlightMapping.ToVietnamTime(f.GioHaCanh);
        var paid = booking.ThanhToans.FirstOrDefault(t => t.TrangThai == TrangThaiThanhToan.ThanhCong
            && !VnpayBookingService.IsRefundOnly(t));
        var seatCode = string.IsNullOrWhiteSpace(ve.SoGhe) ? "Tự động" : ve.SoGhe;
        var seatInfo = ResolveSeatInfo(seatCode, f.MayBay?.SoGhe ?? 0);

        return new BookingViewModel
        {
            Journey = journey,
            CanEditPromotion = booking.TrangThai == TrangThaiDatVe.ChoThanhToan
                && booking.ThanhToans.All(x => x.TrangThai == TrangThaiThanhToan.ThatBai),
            Flight = new FlightOffer(f.Id.ToString(), FlightMapping.AirlineNameFor(f.SoHieu), f.SoHieu,
                f.MayBay?.TenMayBay ?? "", string.IsNullOrWhiteSpace(ve.HangVe) ? "Phổ thông" : ve.HangVe,
                new AirportInfo(f.TuyenBay.SanBayDi.MaSanBay, f.TuyenBay.SanBayDi.ThanhPho, f.TuyenBay.SanBayDi.TenSanBay),
                new AirportInfo(f.TuyenBay.SanBayDen.MaSanBay, f.TuyenBay.SanBayDen.ThanhPho, f.TuyenBay.SanBayDen.TenSanBay),
                dep, arr, ve.GiaVe > 0 ? ve.GiaVe : f.GiaCoBan, "Bay thẳng",
                FlightMapping.CabinBaggage, FlightMapping.CheckedBaggage, chinhSach.ChangeFee, chinhSach.RefundFee),
            Criteria = new FlightSearchCriteria
            {
                OriginCity = f.TuyenBay.SanBayDi.ThanhPho, OriginCode = f.TuyenBay.SanBayDi.MaSanBay,
                DestinationCity = f.TuyenBay.SanBayDen.ThanhPho, DestinationCode = f.TuyenBay.SanBayDen.MaSanBay,
                DepartDateText = dep.ToString("dd/MM/yyyy"), Passengers = 1, Cabin = "Phổ thông"
            },
            Price = journey?.Price ?? new PriceSummary
            {
                Title = "Tổng tiền thanh toán",
                BaseFare = ve.GiaVe,
                Taxes = booking.ThuePhi,
                ServiceFee = booking.PhiDichVu,
                BaggageFee = booking.PhiHanhLy,
                BaggageLabel = "Hành lý ký gửi bổ sung",
                Discount = booking.GiamGia, PromotionCode = booking.MaKhuyenMai,
                SeatFee = Math.Max(0, booking.TongTien + booking.GiamGia - booking.Ves.Sum(x => x.GiaVe) - booking.ThuePhi - booking.PhiDichVu - booking.PhiHanhLy - booking.PhiDoiDaThu),
                ChangeFeePaid = booking.PhiDoiDaThu,
                SeatLabel = $"Chỗ ngồi · {seatInfo.Ten} ({seatCode})"
            },
            Passenger = new PassengerFormModel
            {
                LastName = ve.HanhKhach.HoTen, FirstName = "",
                Email = booking.EmailLienHe ?? booking.KhachHang.Email, Phone = booking.SoDienThoaiLienHe ?? booking.KhachHang.SoDienThoai,
                Nationality = ve.HanhKhach.QuocTich ?? "Việt Nam", IdentityNumber = ve.HanhKhach.SoHoChieu ?? ""
            },
            SeatCode = seatCode,
            SeatPosition = SeatMapPresentation.PositionFor(seatCode),
            SeatTypeName = seatInfo.Ten,
            BaggageCode = BaggageCatalog.FromPrice(booking.PhiHanhLy).Code, BaggageText = $"{ve.HanhLyKyGuiKg} kg",
            PaymentMethod = paid?.PhuongThuc.ToString() ?? "Thẻ ngân hàng",
            PaymentDisplayText = paid is null
                ? "Chưa thanh toán"
                : paid.PhuongThuc switch
                {
                    PhuongThucThanhToan.ChuyenKhoan => "Chuyển khoản ngân hàng · VietQR",
                    PhuongThucThanhToan.TheNganHang when paid.NhaCungCap == VnpayOptions.Provider => VnpayOptions.Provider,
                    PhuongThucThanhToan.TheNganHang => $"Thẻ {paid.NhaCungCap}",
                    _ => paid.PhuongThuc.ToString()
                },
            Pnr = booking.MaDatVe,
            HoldUntil = booking.HetHanGiuCho,
            HoldUntilText = FlightMapping.ToVietnamTime(booking.HetHanGiuCho).ToString("HH:mm 'ngày' dd/MM/yyyy"),
            HoldRemainingText = $"Còn {Math.Max(0, (int)Math.Ceiling((booking.HetHanGiuCho - DateTimeOffset.UtcNow).TotalMinutes))} phút",
            TicketNumber = ve.MaVe,
            TransactionCode = paid?.MaGiaoDich ?? "—",
            CreatedAtText = FlightMapping.ToVietnamTime(booking.NgayDat).ToString("HH:mm 'ngày' dd/MM/yyyy"),
            IssuedAtText = ve.NgayPhatHanh is { } issued
                ? FlightMapping.ToVietnamTime(issued).ToString("dd/MM/yyyy · HH:mm")
                : "—",
            PaidAtText = paid?.NgayHoanTat is { } paidAt
                ? FlightMapping.ToVietnamTime(paidAt).ToString("dd/MM/yyyy · HH:mm")
                : "—"
        };
    }

    private int? RegisteredCustomerId() => User.HasClaim(AccountLinks.UsernameAccountClaim, "true")
        ? User.KhachHangId() : null;

    private PassengerFormModel? LoadPassengerFromSession()
    {
        var json = HttpContext.Session.GetString(KeyPassenger);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<PassengerFormModel>(json); } catch { return null; }
    }

    private JourneySelection? LoadJourney()
    {
        var json = HttpContext.Session.GetString(BookingSession.Journey);
        return json is null ? null : JsonSerializer.Deserialize<JourneySelection>(json);
    }

    private JourneyBookingForm? LoadJourneyForm()
    {
        var json = HttpContext.Session.GetString(BookingSession.JourneyForm);
        return json is null ? null : JsonSerializer.Deserialize<JourneyBookingForm>(json);
    }

    private async Task<IActionResult> SubmitJourneyAsync(JourneySelection selection, CancellationToken ct)
    {
        var form = new JourneyBookingForm();
        await TryUpdateModelAsync(form, "");
        JourneyValidation.Normalize(form);
        var flights = await journeys.FlightsAsync(selection, ct);
        var errors = JourneyValidation.Validate(selection, form, flights, DateTimeOffset.UtcNow);
        HttpContext.Session.SetString(BookingSession.JourneyForm, JsonSerializer.Serialize(form));
        var model = await journeys.BuildAsync(selection, form, HttpContext.Session.GetInt32(KeyBookingId), ct);
        if (model is null) return Redirect("/chuyen-bay");
        model.Errors = errors;
        if (errors.Count > 0) return View("JourneyPassengers", model);
        try
        {
            var hold = await checkoutService.HoldJourneyAsync(selection, form, HttpContext.Session.GetInt32(KeyBookingId), ct, RegisteredCustomerId());
            HttpContext.Session.SetInt32(KeyBookingId, hold.Id);
            return RedirectToAction(nameof(Checkout));
        }
        catch (BookingValidationException e) { model.Errors[e.Field] = e.Message; }
        catch (DbUpdateException e) when (IsDuplicateSeat(e) || IsDuplicatePassengerTicket(e))
        { model.Errors["journey"] = "Ghế hoặc hành khách vừa được đặt ở phiên khác. Vui lòng chọn lại."; }
        return View("JourneyPassengers", model);
    }

    private static ThanhToan? PendingTransfer(DatVe booking) => booking.ThanhToans.FirstOrDefault(x =>
        x.NhaCungCap == VietQr.Provider && x.PhuongThuc == PhuongThucThanhToan.ChuyenKhoan
        && x.TrangThai == TrangThaiThanhToan.ChoXuLy);

    private static string? ReadDemoCard(IFormCollection form, BookingViewModel model, out string brand, out string last4)
    {
        if (!form.TryGetValue("demoCard", out var option))
            return ValidateCard(form, out brand, out last4);

        switch (option.ToString())
        {
            case "visa":
                (brand, last4) = ("Visa", "4242");
                return null;
            case "mastercard":
                model.DemoCard = "mastercard";
                (brand, last4) = ("Mastercard", "4444");
                return null;
            default:
                (brand, last4) = ("Visa", "4242");
                return "Chọn một thẻ mẫu được cung cấp trong danh sách.";
        }
    }

    private static string? ValidateCard(IFormCollection form, out string brand, out string last4)
    {
        var number = new string(form["cardNumber"].ToString().Where(char.IsDigit).ToArray());
        brand = number.StartsWith('4') ? "Visa"
            : number.StartsWith('5') ? "Mastercard"
            : number.StartsWith('6') ? "Napas"
            : "Thẻ ngân hàng";
        last4 = number.Length >= 4 ? number[^4..] : string.Empty;

        if (number is not ("4242424242424242" or "5555555555554444"))
        {
            return "Thanh toán mô phỏng chỉ nhận thẻ thử 4242 4242 4242 4242 hoặc 5555 5555 5555 4444.";
        }

        if (string.IsNullOrWhiteSpace(form["cardHolder"].ToString()))
        {
            return "Vui lòng nhập tên chủ thẻ.";
        }

        var expiry = form["cardExpiry"].ToString().Trim();
        var parts = expiry.Split('/', '-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var month)
            || !int.TryParse(parts[1], out var year)
            || month is < 1 or > 12)
        {
            return "Ngày hết hạn chưa đúng định dạng MM/YY.";
        }

        if (year < 100) year += 2000;
        if (year is < 2000 or > 9998) return "Năm hết hạn không hợp lệ.";
        var expiryEnd = new DateTime(year, month, 1).AddMonths(1);
        if (expiryEnd <= DateTime.UtcNow.AddHours(7))
        {
            return "Thẻ đã hết hạn. Vui lòng dùng thẻ còn hiệu lực.";
        }

        var cvv = new string(form["cardCvv"].ToString().Where(char.IsDigit).ToArray());
        if (cvv.Length is < 3 or > 4)
        {
            return "Mã CVV/CVC phải gồm 3 hoặc 4 chữ số.";
        }

        return null;
    }

    private static bool IsDuplicateSeat(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Ves_ChuyenBayId_SoGhe"
        };

    private static bool IsDuplicatePassengerTicket(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Ves_ChuyenBayId_HanhKhachId"
        };

    private static (string Ma, string Ten, decimal PhuThu) ResolveSeatInfo(string seatCode, int aircraftSeats)
    {
        var row = SeatMapPresentation.RowNumber(seatCode);
        if (row <= 0 || aircraftSeats <= 0)
        {
            return ("TieuChuan", "Ghế tiêu chuẩn", 0m);
        }

        var rowCount = (int)Math.Ceiling(aircraftSeats / (double)SeatsPerRow);
        return ChuyenBayBUS.PhanLoaiGhe(row, rowCount);
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
>>>>>>> Stashed changes
}
