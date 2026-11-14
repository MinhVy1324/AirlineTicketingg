using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QRCoder;
using System.Text.Json;

namespace AirlineTicketing.Web.Controllers;

public class BookingController(AppDbContext dbContext) : Controller
{
    private const string KeyFlightId = "booking.flightId";
    private const string KeyPassenger = "booking.passenger";
    private const string KeyBaggageFee = "booking.baggageFee";
    private const string KeySeat = "booking.seat";
    private const string KeyBookingId = "booking.bookingId";

    private const int SeatsPerRow = 6;

    [HttpGet("/dat-ve/hanh-khach")]
    public async Task<IActionResult> Passengers(
        int? flightId,
        string? loi,
        CancellationToken cancellationToken)
    {
        var id = flightId ?? HttpContext.Session.GetInt32(KeyFlightId);
        if (id is null || id <= 0)
        {
            return Redirect("/chuyen-bay");
        }

        HttpContext.Session.SetInt32(KeyFlightId, id.Value);
        var seat = HttpContext.Session.GetString(KeySeat);
        var model = await BuildBookingModelAsync(
            id.Value,
            LoadPassengerFromSession(),
            HttpContext.Session.GetInt32(KeyBaggageFee) ?? 0,
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

        var flightId = HttpContext.Session.GetInt32(KeyFlightId);
        if (flightId is null) return Redirect("/chuyen-bay");

        var seatCode = form["seat"].ToString().Trim();
        var seatMap = await FlightServices.ChuyenBay(dbContext).LaySoDoGheAsync(flightId.Value, cancellationToken);
        var seat = seatMap.Ghes.FirstOrDefault(x =>
            string.Equals(x.SoGhe, seatCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.TrangThai, "Trong", StringComparison.OrdinalIgnoreCase));

        if (seat is null)
        {
            var model = await BuildBookingModelAsync(
                flightId.Value, passenger, (int)baggageFee, null, cancellationToken);
            if (model is null) return Redirect("/chuyen-bay");

            model.SeatError = string.IsNullOrWhiteSpace(seatCode)
                ? "Vui lòng chọn một chỗ ngồi trước khi sang bước thanh toán."
                : $"Ghế {seatCode} không còn trống. Vui lòng chọn ghế khác.";
            return View(model);
        }

        HttpContext.Session.SetString(KeySeat, seat.SoGhe);
        return RedirectToAction(nameof(Checkout));
    }

    [HttpGet("/dat-ve/hanh-khach/kiem-tra")]
    public async Task<IActionResult> ReviewPassengers(CancellationToken cancellationToken)
    {
        var id = HttpContext.Session.GetInt32(KeyFlightId);
        if (id is null) return Redirect("/chuyen-bay");
        var model = await BuildBookingModelAsync(
            id.Value,
            LoadPassengerFromSession(),
            HttpContext.Session.GetInt32(KeyBaggageFee) ?? 0,
            HttpContext.Session.GetString(KeySeat),
            cancellationToken);
        return model is null ? Redirect("/chuyen-bay") : View("Passengers", model);
    }

    [HttpGet("/dat-ve/thanh-toan")]
    public async Task<IActionResult> Checkout(CancellationToken cancellationToken)
    {
        var id = HttpContext.Session.GetInt32(KeyFlightId);
        if (id is null) return Redirect("/chuyen-bay");
        var seat = HttpContext.Session.GetString(KeySeat);
        if (string.IsNullOrWhiteSpace(seat)) return RedirectToAction(nameof(Passengers), new { loi = "chon-ghe" });

        var model = await BuildBookingModelAsync(
            id.Value,
            LoadPassengerFromSession(),
            HttpContext.Session.GetInt32(KeyBaggageFee) ?? 0,
            seat,
            cancellationToken);
        if (model is null) return Redirect("/chuyen-bay");
        if (model.SeatCode == "Chưa chọn")
        {
            HttpContext.Session.Remove(KeySeat);
            return RedirectToAction(nameof(Passengers), new { loi = "ghe-da-bi-chon" });
        }

        return View(model);
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

        var seatCode = HttpContext.Session.GetString(KeySeat);
        if (string.IsNullOrWhiteSpace(seatCode))
        {
            return RedirectToAction(nameof(Passengers), new { loi = "chon-ghe" });
        }

        var model = await BuildBookingModelAsync(
            id.Value,
            passenger,
            HttpContext.Session.GetInt32(KeyBaggageFee) ?? 0,
            seatCode,
            cancellationToken);
        if (model is null) return Redirect("/chuyen-bay");

        if (model.SeatCode == "Chưa chọn")
        {
            HttpContext.Session.Remove(KeySeat);
            return RedirectToAction(nameof(Passengers), new { loi = "ghe-da-bi-chon" });
        }

        var payment = string.Equals(form["payment"], "transfer", StringComparison.OrdinalIgnoreCase)
            ? "transfer"
            : "card";
        model.SelectedPayment = payment;

        string cardBrand = "Visa";
        string cardLast4 = string.Empty;
        if (payment == "card")
        {
            var cardError = ValidateCard(form, out cardBrand, out cardLast4);
            if (cardError is not null)
            {
                model.PaymentError = cardError;
                return View(model);
            }
        }

        try
        {
            var booking = await CreateBookingAsync(
                model, passenger, seatCode, payment == "transfer", cardBrand, cardLast4, cancellationToken);
            HttpContext.Session.SetInt32(KeyBookingId, booking.Id);

            if (payment == "transfer")
            {
                return RedirectToAction(nameof(Transfer), new { pnr = booking.MaDatVe });
            }

            return RedirectToAction(nameof(Success), new { pnr = booking.MaDatVe });
        }
        catch (InvalidOperationException)
        {
            HttpContext.Session.Remove(KeySeat);
            return RedirectToAction(nameof(Passengers), new { loi = "ghe-da-bi-chon" });
        }
        catch (DbUpdateException ex) when (IsDuplicateSeat(ex))
        {
            HttpContext.Session.Remove(KeySeat);
            return RedirectToAction(nameof(Passengers), new { loi = "ghe-da-bi-chon" });
        }
        catch (Exception)
        {
            return RedirectToAction(nameof(PaymentFailed));
        }
    }

    [HttpGet("/dat-ve/chuyen-khoan/{pnr}")]
    public async Task<IActionResult> Transfer(string pnr, CancellationToken cancellationToken)
    {
        var booking = await LoadBookingAsync(pnr, cancellationToken);
        if (booking is null) return Redirect("/ve-cua-toi");

        if (booking.TrangThai == TrangThaiDatVe.DaXacNhan)
        {
            return RedirectToAction(nameof(Success), new { pnr });
        }

        if (booking.TrangThai != TrangThaiDatVe.ChoThanhToan)
        {
            return View("Expired", Outcome("Đặt chỗ không còn chờ thanh toán",
                "Đặt chỗ này đã được xử lý hoặc không còn hiệu lực. Vui lòng kiểm tra Vé của tôi.", "warning"));
        }

        var now = DateTimeOffset.UtcNow;
        if (booking.HetHanGiuCho <= now)
        {
            await ExpireBookingAsync(pnr, now, cancellationToken);
            return View("Expired", Outcome("Đã hết thời gian giữ chỗ",
                "Vui lòng tìm chuyến bay và đặt lại. Ghế đã được trả về sơ đồ.", "warning"));
        }

        var ve = booking.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var model = new QrPaymentViewModel
        {
            Pnr = booking.MaDatVe,
            Amount = booking.TongTien,
            Memo = $"AEROVIET {booking.MaDatVe}",
            HoldUntilText = FlightMapping.ToVietnamTime(booking.HetHanGiuCho)
                .ToString("HH:mm 'ngày' dd/MM/yyyy"),
            QrUrl = Url.Action(nameof(TransferQr), new { pnr }) ?? string.Empty,
            SeatCode = ve?.SoGhe ?? "—",
            FlightNumber = ve?.ChuyenBay?.SoHieu ?? "—",
            PassengerName = booking.KhachHang?.HoTen ?? "—"
        };

        return View(model);
    }

    [HttpGet("/dat-ve/chuyen-khoan/{pnr}/qr.png")]
    public async Task<IActionResult> TransferQr(string pnr, CancellationToken cancellationToken)
    {
        var booking = await LoadBookingAsync(pnr, cancellationToken);
        if (booking is null) return NotFound();

        var payload = VietQr.BuildPayload(booking.TongTien, $"AEROVIET {booking.MaDatVe}");
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
        var booking = await dbContext.DatVes
            .Include(x => x.Ves)
            .Include(x => x.ThanhToans)
            .FirstOrDefaultAsync(x => x.MaDatVe == pnr, cancellationToken);

        if (booking is null) return Redirect("/ve-cua-toi");

        if (booking.TrangThai == TrangThaiDatVe.DaXacNhan)
        {
            return RedirectToAction(nameof(Success), new { pnr });
        }

        var now = DateTimeOffset.UtcNow;
        if (booking.TrangThai != TrangThaiDatVe.ChoThanhToan || booking.HetHanGiuCho <= now)
        {
            Expire(booking, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return View("Expired", Outcome("Đã hết thời gian giữ chỗ",
                "Giao dịch chuyển khoản không còn hiệu lực. Vui lòng đặt lại chuyến bay.", "warning"));
        }

        var payment = booking.ThanhToans
            .FirstOrDefault(x => x.PhuongThuc == PhuongThucThanhToan.ChuyenKhoan
                && x.TrangThai == TrangThaiThanhToan.ChoXuLy);

        if (payment is null)
        {
            return RedirectToAction(nameof(PaymentFailed));
        }

        payment.TrangThai = TrangThaiThanhToan.ThanhCong;
        payment.NgayHoanTat = now;

        foreach (var ticket in booking.Ves.Where(x => x.TrangThai == TrangThaiVe.TamGiu))
        {
            ticket.TrangThai = TrangThaiVe.DaPhatHanh;
            ticket.MaVe = GenerateTicketNumber(now);
            ticket.NgayPhatHanh = now;
        }

        var previous = booking.TrangThai;
        booking.TrangThai = TrangThaiDatVe.DaXacNhan;
        booking.NgayXacNhan = now;
        booking.LichSus.Add(new LichSuDatVe
        {
            DatVeId = booking.Id,
            HanhDong = "XacNhanChuyenKhoan",
            TrangThaiTruoc = previous.ToString(),
            TrangThaiSau = booking.TrangThai.ToString(),
            LyDo = "Xác nhận chuyển khoản VietQR",
            NguoiThucHien = "system",
            ThoiDiem = now,
            DuLieuBoSung = JsonSerializer.Serialize(new { booking.MaDatVe, payment.MaGiaoDich })
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        HttpContext.Session.SetInt32(KeyBookingId, booking.Id);
        HttpContext.Session.Remove(KeySeat);
        return RedirectToAction(nameof(Success), new { pnr });
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
        if (booking.TrangThai != TrangThaiDatVe.DaXacNhan)
        {
            return RedirectToAction(nameof(Transfer), new { pnr = booking.MaDatVe });
        }

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

    private async Task<BookingViewModel?> BuildBookingModelAsync(
        int flightId,
        PassengerFormModel? passenger,
        int baggageFee,
        string? seatCode,
        CancellationToken ct)
    {
        await ReleaseExpiredHoldsAsync(ct);

        var chuyenBayBus = FlightServices.ChuyenBay(dbContext);
        var dto = await chuyenBayBus.LayChuyenBayAsync(flightId, ct);
        if (dto is null) return null;

        var offer = FlightMapping.ToOffer(dto);
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

        var selected = string.IsNullOrWhiteSpace(seatCode)
            ? null
            : seatMap.Ghes.FirstOrDefault(x =>
                string.Equals(x.SoGhe, seatCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.TrangThai, "Trong", StringComparison.OrdinalIgnoreCase));

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
                Taxes = FlightMapping.TaxesPerPassenger,
                ServiceFee = FlightMapping.ServiceFeePerPassenger,
                BaggageFee = baggageFee,
                BaggageLabel = "Hành lý ký gửi bổ sung",
                SeatFee = seatFee,
                SeatLabel = selected is null
                    ? "Chỗ ngồi · chưa chọn"
                    : $"Chỗ ngồi · {selected.TenLoaiGhe} ({selected.SoGhe})"
            },
            Passenger = passenger,
            SeatCode = selected?.SoGhe ?? "Chưa chọn",
            SeatPosition = selected is null ? string.Empty : SeatPositionFor(selected.SoGhe),
            SeatTypeName = seatTypeName,
            SeatRows = BuildSeatRows(seatMap, selected?.SoGhe),
            ExtraBaggage = baggageFee > 0 ? $"+{Money.Vnd(baggageFee)}" : "Không",
            BaggageText = baggageFee > 0 ? $"23 kg + mua thêm {Money.Vnd(baggageFee)}" : "23 kg",
            PaymentMethod = "Thẻ ngân hàng",
            PaymentDisplayText = "Thẻ ngân hàng",
            Pnr = "—",
            HoldUntilText = DateTimeOffset.UtcNow.AddHours(7).AddMinutes(15).ToString("HH:mm 'ngày' dd/MM/yyyy"),
            HoldRemainingText = "Còn 15 phút",
            CreatedAtText = DateTimeOffset.UtcNow.AddHours(7).ToString("HH:mm 'ngày' dd/MM/yyyy")
        };
    }

    private async Task<DatVe> CreateBookingAsync(
        BookingViewModel model,
        PassengerFormModel passenger,
        string seatCode,
        bool isTransfer,
        string cardBrand,
        string cardLast4,
        CancellationToken ct)
    {
        var flightDbId = int.TryParse(model.Flight.Id, out var parsedFlightId) ? parsedFlightId : 0;
        if (flightDbId <= 0)
        {
            throw new InvalidOperationException("Chuyến bay đã chọn không hợp lệ.");
        }

        // Toàn bộ đặt chỗ + vé + thanh toán nằm trong một transaction: nếu ghế bị
        // trùng hoặc ghi vé lỗi thì không còn bản ghi đặt chỗ treo lại.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        // Kiểm tra lại ngay trước khi ghi: ghế phải còn trống và đúng loại ghế đã chọn.
        var seatMap = await FlightServices.ChuyenBay(dbContext).LaySoDoGheAsync(flightDbId, ct);
        var seat = seatMap.Ghes.FirstOrDefault(x =>
            string.Equals(x.SoGhe, seatCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.TrangThai, "Trong", StringComparison.OrdinalIgnoreCase));
        if (seat is null)
        {
            throw new InvalidOperationException(
                $"Ghế {seatCode} vừa được hành khách khác giữ. Vui lòng chọn ghế khác.");
        }

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

        var identity = string.IsNullOrWhiteSpace(passenger.IdentityNumber)
            ? null
            : passenger.IdentityNumber.Trim();

        // Số giấy tờ là duy nhất trong hệ thống: khách đặt chuyến mới sẽ dùng lại
        // hồ sơ hành khách cũ thay vì tạo bản ghi trùng.
        HanhKhach? hk = null;
        if (identity is not null)
        {
            hk = await dbContext.HanhKhachs.FirstOrDefaultAsync(x => x.SoHoChieu == identity, ct);
        }

        if (hk is null)
        {
            hk = new HanhKhach
            {
                HoTen = fullName,
                QuocTich = passenger.Nationality,
                SoHoChieu = identity,
                NgaySinh = FlightMapping.TryParseDate(passenger.DateOfBirth, out var dob) ? dob : null
            };
            dbContext.HanhKhachs.Add(hk);
        }
        else
        {
            hk.HoTen = fullName;
            if (!string.IsNullOrWhiteSpace(passenger.Nationality)) hk.QuocTich = passenger.Nationality;
            if (FlightMapping.TryParseDate(passenger.DateOfBirth, out var dob)) hk.NgaySinh = dob;
        }

        await dbContext.SaveChangesAsync(ct);

        var pnr = GeneratePnr();
        while (await dbContext.DatVes.AnyAsync(x => x.MaDatVe == pnr, ct)) pnr = GeneratePnr();

        var datVe = new DatVe
        {
            MaDatVe = pnr,
            KhachHangId = kh.Id,
            NgayDat = now,
            TrangThai = isTransfer ? TrangThaiDatVe.ChoThanhToan : TrangThaiDatVe.DaXacNhan,
            TongTien = model.Price.Total,
            DonViTienTe = "VND",
            HetHanGiuCho = now.AddMinutes(15),
            NgayXacNhan = isTransfer ? null : now,
            ThuePhi = model.Price.Taxes,
            PhiDichVu = model.Price.ServiceFee,
            PhiHanhLy = model.Price.BaggageFee,
            EmailLienHe = email,
            SoDienThoaiLienHe = phone,
            GhiChu = isTransfer
                ? "Thanh toán chuyển khoản VietQR"
                : $"Thanh toán thẻ {cardBrand} •••• {cardLast4}"
        };
        dbContext.DatVes.Add(datVe);
        await dbContext.SaveChangesAsync(ct);

        dbContext.Ves.Add(new Ve
        {
            MaVe = isTransfer ? $"HOLD{now:yyMMddHHmmss}{Random.Shared.Next(100, 999)}" : GenerateTicketNumber(now),
            ChuyenBayId = flightDbId,
            DatVeId = datVe.Id,
            HanhKhachId = hk.Id,
            SoGhe = seat.SoGhe,
            HangVe = model.Flight.FareName,
            GiaVe = model.Price.BaseFare,
            TrangThai = isTransfer ? TrangThaiVe.TamGiu : TrangThaiVe.DaPhatHanh,
            NgayPhatHanh = isTransfer ? null : now,
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
            PhuongThuc = isTransfer ? PhuongThucThanhToan.ChuyenKhoan : PhuongThucThanhToan.TheNganHang,
            NhaCungCap = isTransfer ? "VietQR" : $"{cardBrand} •••• {cardLast4}",
            TrangThai = isTransfer ? TrangThaiThanhToan.ChoXuLy : TrangThaiThanhToan.ThanhCong,
            MaGiaoDich = $"{(isTransfer ? "QR" : "TX")}{now:ddMMyyHHmmss}{Random.Shared.Next(100, 999)}",
            NgayTao = now,
            NgayHoanTat = isTransfer ? null : now
        });
        await dbContext.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
        return datVe;
    }

    private async Task ReleaseExpiredHoldsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await dbContext.DatVes
            .Include(x => x.Ves)
            .Where(x => x.TrangThai == TrangThaiDatVe.ChoThanhToan && x.HetHanGiuCho <= now)
            .ToListAsync(ct);

        if (expired.Count == 0) return;

        foreach (var booking in expired)
        {
            Expire(booking, now);
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task ExpireBookingAsync(string pnr, DateTimeOffset now, CancellationToken ct)
    {
        var booking = await dbContext.DatVes
            .Include(x => x.Ves)
            .FirstOrDefaultAsync(x => x.MaDatVe == pnr, ct);
        if (booking is null) return;

        Expire(booking, now);
        await dbContext.SaveChangesAsync(ct);
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
        var seatCode = string.IsNullOrWhiteSpace(ve.SoGhe) ? "Tự động" : ve.SoGhe;
        var seatInfo = ResolveSeatInfo(seatCode, f.MayBay?.SoGhe ?? 0);

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
                Taxes = booking.ThuePhi,
                ServiceFee = booking.PhiDichVu,
                BaggageFee = booking.PhiHanhLy,
                BaggageLabel = "Hành lý ký gửi bổ sung",
                SeatFee = seatInfo.PhuThu,
                SeatLabel = $"Chỗ ngồi · {seatInfo.Ten} ({seatCode})"
            },
            Passenger = new PassengerFormModel
            {
                LastName = ve.HanhKhach.HoTen, FirstName = "",
                Email = booking.KhachHang.Email, Phone = booking.KhachHang.SoDienThoai,
                Nationality = ve.HanhKhach.QuocTich ?? "Việt Nam", IdentityNumber = ve.HanhKhach.SoHoChieu ?? ""
            },
            SeatCode = seatCode,
            SeatPosition = SeatPositionFor(seatCode),
            SeatTypeName = seatInfo.Ten,
            BaggageText = booking.PhiHanhLy > 0
                ? $"{ve.HanhLyKyGuiKg} kg + mua thêm {Money.Vnd(booking.PhiHanhLy)}"
                : $"{ve.HanhLyKyGuiKg} kg",
            PaymentMethod = paid?.PhuongThuc.ToString() ?? "Thẻ ngân hàng",
            PaymentDisplayText = paid is null
                ? "Chưa thanh toán"
                : paid.PhuongThuc switch
                {
                    PhuongThucThanhToan.ChuyenKhoan => "Chuyển khoản ngân hàng · VietQR",
                    PhuongThucThanhToan.TheNganHang => $"Thẻ {paid.NhaCungCap}",
                    _ => paid.PhuongThuc.ToString()
                },
            Pnr = booking.MaDatVe,
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

    private PassengerFormModel? LoadPassengerFromSession()
    {
        var json = HttpContext.Session.GetString(KeyPassenger);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<PassengerFormModel>(json); } catch { return null; }
    }

    private static string? ValidateCard(IFormCollection form, out string brand, out string last4)
    {
        var number = new string(form["cardNumber"].ToString().Where(char.IsDigit).ToArray());
        brand = number.StartsWith('4') ? "Visa"
            : number.StartsWith('5') ? "Mastercard"
            : number.StartsWith('6') ? "Napas"
            : "Thẻ ngân hàng";
        last4 = number.Length >= 4 ? number[^4..] : string.Empty;

        if (number.Length is < 13 or > 19)
        {
            return "Số thẻ chưa hợp lệ. Vui lòng nhập 13–19 chữ số in trên thẻ.";
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

    private static IReadOnlyList<SeatRow> BuildSeatRows(SoDoGheDTO seatMap, string? selectedSeat)
    {
        var rows = new List<SeatRow>();
        foreach (var group in seatMap.Ghes
                     .GroupBy(x => ParseRow(x.SoGhe))
                     .Where(x => x.Key > 0)
                     .OrderBy(x => x.Key))
        {
            var cells = group
                .OrderBy(x => x.SoGhe[^1])
                .Select(seat =>
                {
                    var state = string.Equals(seat.SoGhe, selectedSeat, StringComparison.OrdinalIgnoreCase)
                        ? SeatState.Selected
                        : string.Equals(seat.TrangThai, "Trong", StringComparison.OrdinalIgnoreCase)
                            ? SeatState.Free
                            : SeatState.Taken;

                    return new SeatCell(
                        seat.SoGhe[^1].ToString(),
                        state,
                        seat.SoGhe,
                        seat.LoaiGhe,
                        seat.TenLoaiGhe,
                        seat.PhuThu,
                        SeatPositionFor(seat.SoGhe));
                })
                .ToArray();

            rows.Add(new SeatRow(group.Key.ToString(), cells));
        }

        return rows;
    }

    private static int ParseRow(string seatCode)
    {
        var digits = new string(seatCode.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var row) ? row : 0;
    }

    private static string SeatPositionFor(string seatCode)
    {
        if (string.IsNullOrWhiteSpace(seatCode)) return string.Empty;
        return char.ToUpperInvariant(seatCode[^1]) switch
        {
            'A' or 'F' => "Cạnh cửa sổ",
            'C' or 'D' => "Cạnh lối đi",
            'B' or 'E' => "Ở giữa",
            _ => string.Empty
        };
    }

    private static (string Ma, string Ten, decimal PhuThu) ResolveSeatInfo(string seatCode, int aircraftSeats)
    {
        var row = ParseRow(seatCode);
        if (row <= 0 || aircraftSeats <= 0)
        {
            return ("TieuChuan", "Ghế tiêu chuẩn", 0m);
        }

        var rowCount = (int)Math.Ceiling(aircraftSeats / (double)SeatsPerRow);
        return ChuyenBayBUS.PhanLoaiGhe(row, rowCount);
    }

    private static string GenerateTicketNumber(DateTimeOffset now) =>
        $"738{now:yyMMddHHmm}{Random.Shared.Next(10, 99)}";

    private static string GeneratePnr()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
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
