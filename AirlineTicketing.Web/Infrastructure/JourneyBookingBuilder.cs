using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Infrastructure;

public sealed class JourneyBookingBuilder(AppDbContext db, CauHinhHeThongBUS policies)
{
    public async Task<IReadOnlyList<ChuyenBay>> FlightsAsync(JourneySelection selection, CancellationToken ct)
    {
        var ids = selection.FlightIds.ToArray();
        var flights = await db.ChuyenBays.AsNoTracking().Include(x => x.MayBay).Include(x => x.HangHangKhong)
            .Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDen).Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        return ids.Select(id => flights.FirstOrDefault(x => x.Id == id)).Where(x => x is not null).Cast<ChuyenBay>().ToArray();
    }

    public async Task<JourneyBookingViewModel?> BuildAsync(JourneySelection selection, JourneyBookingForm? form,
        int? ownBookingId, CancellationToken ct)
    {
        selection.Party.Validate();
        var cabin = JourneyRules.Cabin(selection.Cabin);
        var flights = await FlightsAsync(selection, ct);
        if (flights.Count != selection.LegCount) return null;
        form ??= new JourneyBookingForm();
        while (form.People.Count < selection.Party.Total) form.People.Add(new JourneyPassengerForm());
        var types = selection.Party.Types;
        for (var i = 0; i < selection.Party.Total; i++)
        {
            var row = form.People[i];
            while (row.Seats.Count < selection.LegCount) row.Seats.Add("");
            while (row.Baggage.Count < selection.LegCount) row.Baggage.Add(BaggageCatalog.None);
            if (types[i] == LoaiHanhKhach.EmBe && !row.CompanionIndex.HasValue)
                row.CompanionIndex = i - selection.Party.Adults - selection.Party.Children;
        }
        var own = ownBookingId.HasValue ? await db.DatVes.AsNoTracking().Include(x => x.Ves)
            .FirstOrDefaultAsync(x => x.Id == ownBookingId && x.TrangThai == TrangThaiDatVe.ChoThanhToan
                && x.HetHanGiuCho > DateTimeOffset.UtcNow, ct) : null;
        var policy = await policies.LayChinhSachAsync(ct);
        var legs = new List<JourneyLegViewModel>();
        decimal fare = 0, taxes = 0, services = 0, baggage = 0, seats = 0;
        for (var leg = 0; leg < flights.Count; leg++)
        {
            var flight = flights[leg];
            var map = await FlightServices.ChuyenBay(db).LaySoDoGheAsync(flight.Id, ct);
            var ownSeats = own?.Ves.Where(ticket => ticket.ChuyenBayId == flight.Id && ticket.TrangThai == TrangThaiVe.TamGiu)
                .Select(ticket => ticket.SoGhe).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rows = SeatMapPresentation.Build(map, ownSeats: ownSeats);
            var available = rows.SelectMany(row => row.Seats).Where(seat => seat.State == SeatState.Free)
                .ToDictionary(seat => seat.Code, seat => seat.Surcharge);
            legs.Add(new(leg == 0 ? "Chiều đi" : "Chiều về", Offer(flight, cabin), rows));
            for (var i = 0; i < selection.Party.Total; i++)
            {
                fare += JourneyRules.BaseFare(flight.GiaCoBan, cabin, types[i]);
                taxes += JourneyRules.Tax(policy, types[i]); services += JourneyRules.ServiceFee(policy, types[i]);
                if (types[i] == LoaiHanhKhach.EmBe) continue;
                baggage += BaggageCatalog.Find(form.People[i].Baggage[leg])?.Price ?? 0;
                seats += available.GetValueOrDefault(form.People[i].Seats[leg] ?? string.Empty);
            }
        }
        return new JourneyBookingViewModel
        {
            Selection = selection, Form = form, Legs = legs,
            Price = new PriceSummary { BaseFare = fare, Taxes = taxes, ServiceFee = services, BaggageFee = baggage, SeatFee = seats,
                BaseFareLabel = "Giá vé cả nhóm / tất cả chặng", SeatLabel = "Phụ thu ghế cả nhóm", BaggageLabel = "Hành lý mua thêm cả nhóm" }
        };
    }

    public static bool UsesJourney(DatVe booking) => booking.Ves.Count > 1
        || booking.Ves.Any(ticket => ticket.HangVe is "ThuongGia" or "Thương gia" || ticket.LoaiHanhKhach != LoaiHanhKhach.NguoiLon);

    public static JourneyBookingViewModel FromBooking(DatVe booking)
    {
        var ordered = booking.Ves.OrderBy(x => x.Id).ToArray();
        var legs = ordered.GroupBy(x => x.ChuyenBayId).ToArray();
        var first = legs[0].ToArray();
        var persons = first.Select(x => x.HanhKhach).ToArray();
        var party = new PassengerParty(first.Count(x => x.LoaiHanhKhach == LoaiHanhKhach.NguoiLon),
            first.Count(x => x.LoaiHanhKhach == LoaiHanhKhach.TreEm), first.Count(x => x.LoaiHanhKhach == LoaiHanhKhach.EmBe));
        var cabin = CabinOf(ordered[0].HangVe);
        var rows = new List<JourneyTicketViewModel>();
        foreach (var ticket in ordered)
        {
            var flight = ticket.ChuyenBay;
            var companion = ticket.NguoiDiKemId.HasValue ? ordered.FirstOrDefault(x => x.HanhKhachId == ticket.NguoiDiKemId)?.HanhKhach.HoTen : null;
            var seat = ticket.LoaiHanhKhach == LoaiHanhKhach.EmBe ? "Đi cùng " + (companion ?? "người lớn") : ticket.SoGhe;
            rows.Add(new(ticket.HanhKhach.HoTen, JourneyRules.TypeName(ticket.LoaiHanhKhach), flight.SoHieu,
                $"{flight.TuyenBay.SanBayDi.MaSanBay} → {flight.TuyenBay.SanBayDen.MaSanBay}",
                $"{FlightMapping.ToVietnamTime(flight.GioKhoiHanh):dd/MM/yyyy HH:mm}", CabinOf(ticket.HangVe).Name,
                seat, ticket.LoaiHanhKhach == LoaiHanhKhach.EmBe ? "Không có hành lý riêng" : $"{ticket.HanhLyXachTayKg} kg xách tay + {ticket.HanhLyKyGuiKg} kg ký gửi",
                ticket.TrangThai == TrangThaiVe.DaPhatHanh ? ticket.MaVe : "", ticket.GiaVe + ticket.ThuePhi + ticket.PhiDichVu + ticket.PhiHanhLy + ticket.PhiChoNgoi));
        }
        var form = new JourneyBookingForm
        {
            ContactName = booking.TenLienHe ?? booking.KhachHang.HoTen,
            Email = booking.EmailLienHe ?? booking.KhachHang.Email,
            Phone = booking.SoDienThoaiLienHe ?? booking.KhachHang.SoDienThoai
        };
        foreach (var ticket in first)
        {
            var person = ticket.HanhKhach;
            var name = person.HoTen.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            form.People.Add(new JourneyPassengerForm
            {
                Person = new PassengerFormModel { LastName = name.ElementAtOrDefault(0) ?? "", FirstName = name.ElementAtOrDefault(1) ?? "",
                    DateOfBirth = person.NgaySinh?.ToString("dd/MM/yyyy") ?? "", Gender = person.GioiTinh switch { GioiTinh.Nam => "Nam", GioiTinh.Nu => "Nữ", _ => "Khác" },
                    Nationality = person.QuocTich ?? "", IdentityNumber = person.SoHoChieu ?? "" },
                Seats = legs.Select(leg => leg.First(x => x.HanhKhachId == person.Id).SoGhe).ToList(),
                Baggage = legs.Select(leg => BaggageCatalog.FromPrice(leg.First(x => x.HanhKhachId == person.Id).PhiHanhLy).Code).ToList(),
                CompanionIndex = ticket.NguoiDiKemId.HasValue ? Array.FindIndex(persons, p => p.Id == ticket.NguoiDiKemId) : null
            });
        }
        return new JourneyBookingViewModel
        {
            Selection = new(legs[0].Key, legs.Length > 1 ? legs[1].Key : null, party, cabin.Code), Form = form,
            Legs = legs.Select((leg, index) => new JourneyLegViewModel(index == 0 ? "Chiều đi" : "Chiều về", Offer(leg.First().ChuyenBay, cabin), [])).ToArray(),
            Tickets = rows, Pnr = booking.MaDatVe, HoldUntil = booking.HetHanGiuCho,
            Price = new PriceSummary { BaseFare = ordered.Sum(x => x.GiaVe), Taxes = booking.ThuePhi, ServiceFee = booking.PhiDichVu,
                BaggageFee = booking.PhiHanhLy, Discount = booking.GiamGia, PromotionCode = booking.MaKhuyenMai,
                SeatFee = Math.Max(0, booking.TongTien + booking.GiamGia - ordered.Sum(x => x.GiaVe) - booking.ThuePhi - booking.PhiDichVu - booking.PhiHanhLy),
                BaseFareLabel = "Giá vé cả nhóm / tất cả chặng", SeatLabel = "Phụ thu ghế cả nhóm", BaggageLabel = "Hành lý mua thêm cả nhóm" }
        };
    }

    public static FareClass CabinOf(string value)
    { try { return JourneyRules.Cabin(value); } catch (ArgumentException) { return JourneyRules.Economy; } }
    private static FlightOffer Offer(ChuyenBay flight, FareClass cabin) => new(flight.Id.ToString(),
        flight.HangHangKhong?.TenHang ?? FlightMapping.AirlineNameFor(flight.SoHieu), flight.SoHieu, flight.MayBay?.TenMayBay ?? "",
        cabin.Name, new(flight.TuyenBay.SanBayDi.MaSanBay, flight.TuyenBay.SanBayDi.ThanhPho, flight.TuyenBay.SanBayDi.TenSanBay),
        new(flight.TuyenBay.SanBayDen.MaSanBay, flight.TuyenBay.SanBayDen.ThanhPho, flight.TuyenBay.SanBayDen.TenSanBay),
        FlightMapping.ToVietnamTime(flight.GioKhoiHanh), FlightMapping.ToVietnamTime(flight.GioHaCanh), flight.GiaCoBan * cabin.Multiplier,
        "Bay thẳng", FlightMapping.CabinBaggage, FlightMapping.CheckedBaggage, 0, 0);
}
