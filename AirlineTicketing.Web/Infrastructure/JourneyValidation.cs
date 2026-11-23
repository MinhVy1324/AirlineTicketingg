using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using AirlineTicketing.BUS;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.ViewModels;

namespace AirlineTicketing.Web.Infrastructure;

public static class JourneyValidation
{
    public static Dictionary<string, string> Validate(JourneySelection selection, JourneyBookingForm form,
        IReadOnlyList<ChuyenBay> flights, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string>();
        try { selection.Party.Validate(); JourneyRules.Cabin(selection.Cabin); }
        catch (ArgumentException e) { errors["journey"] = e.Message; return errors; }
        if (flights.Count != selection.LegCount || flights.Select(x => x.Id).Distinct().Count() != selection.LegCount
            || flights.Any(x => x.TrangThai != TrangThaiChuyenBay.DuKien || x.GioKhoiHanh <= now
                || !x.TuyenBay.DangKhaiThac || !x.MayBay.DangHoatDong))
            errors["journey"] = "Một chuyến bay không còn mở bán. Vui lòng chọn lại hành trình.";
        if (flights.Count == 2 && (flights[0].TuyenBay.SanBayDiId != flights[1].TuyenBay.SanBayDenId
            || flights[0].TuyenBay.SanBayDenId != flights[1].TuyenBay.SanBayDiId
            || flights[1].GioKhoiHanh <= flights[0].GioHaCanh))
            errors["journey"] = "Chiều về phải ngược tuyến và khởi hành sau khi chiều đi hạ cánh.";
        if (form.ContactName.Length is < 1 or > 150) errors["contactName"] = "Vui lòng nhập tên người liên hệ, tối đa 150 ký tự.";
        if (form.Email.Length is < 1 or > 254 || !new EmailAddressAttribute().IsValid(form.Email))
            errors["email"] = "Email liên hệ không hợp lệ.";
        if (form.Phone.Length > 20 || form.Phone.Count(char.IsDigit) is < 9 or > 15
            || !Regex.IsMatch(form.Phone, @"^\+?[0-9 ()-]+$", RegexOptions.CultureInvariant))
            errors["phone"] = "Số điện thoại liên hệ phải có 9–15 chữ số.";
        if (form.People.Count != selection.Party.Total)
        { errors["journey"] = "Số hồ sơ hành khách phải khớp với số khách đã chọn."; return errors; }
        var types = selection.Party.Types;
        var companions = new HashSet<int>();
        var documents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var people = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < form.People.Count; i++)
        {
            var row = form.People[i];
            foreach (var flight in flights)
            {
                var date = DateOnly.FromDateTime(FlightMapping.ToVietnamTime(flight.GioKhoiHanh).DateTime);
                foreach (var error in PassengerValidation.Validate(row.Person, date, types[i], validateContact: false))
                    errors.TryAdd($"person-{i}-{error.Key}", error.Value + (flights.Count == 2 ? " Nhóm tuổi phải phù hợp ở cả hai chiều." : ""));
            }
            var identity = row.Person.IdentityNumber;
            if (identity.Length > 0 && !documents.Add(identity))
                errors[$"person-{i}-identityNumber"] = "Giấy tờ bị lặp trong nhóm hành khách.";
            var birthKey = FlightMapping.TryParseDate(row.Person.DateOfBirth, out var parsedBirth)
                ? parsedBirth.ToString("yyyy-MM-dd") : row.Person.DateOfBirth;
            if (!people.Add(row.Person.FullName.ToUpperInvariant() + "|" + birthKey))
                errors[$"person-{i}-dateOfBirth"] = "Có hai hồ sơ trùng họ tên và ngày sinh trong nhóm.";
            if (row.Seats.Count != selection.LegCount || row.Baggage.Count != selection.LegCount)
            { errors["journey"] = "Vui lòng chọn đủ ghế và dịch vụ cho từng chặng."; continue; }
            for (var leg = 0; leg < selection.LegCount; leg++)
            {
                if (BaggageCatalog.Find(row.Baggage[leg]) is null)
                    errors[$"baggage-{i}-{leg}"] = "Gói hành lý không hợp lệ.";
                if (types[i] == LoaiHanhKhach.EmBe)
                {
                    if (row.Seats[leg].Length > 0 || row.Baggage[leg] != BaggageCatalog.None)
                        errors[$"seat-{i}-{leg}"] = "Em bé đi cùng người lớn, không chọn ghế hay hành lý mua thêm riêng.";
                }
                else if (string.IsNullOrWhiteSpace(row.Seats[leg])) errors[$"seat-{i}-{leg}"] = "Vui lòng chọn ghế cho hành khách.";
            }
            if (types[i] == LoaiHanhKhach.EmBe)
            {
                var companion = row.CompanionIndex;
                if (!companion.HasValue || companion < 0 || companion >= selection.Party.Adults || !companions.Add(companion.Value)
                    || !FlightMapping.TryParseDate(form.People[companion.GetValueOrDefault()].Person.DateOfBirth, out var birth)
                    || flights.Any(flight => birth > DateOnly.FromDateTime(FlightMapping.ToVietnamTime(flight.GioKhoiHanh).DateTime).AddYears(-18)))
                    errors[$"companion-{i}"] = "Mỗi em bé cần một người lớn riêng từ 18 tuổi đi cùng ở cả hành trình.";
            }
        }
        if (selection.Party.Children > 0 && !form.People.Take(selection.Party.Adults).Any(row =>
            FlightMapping.TryParseDate(row.Person.DateOfBirth, out var birth)
            && flights.All(flight => birth <= DateOnly.FromDateTime(FlightMapping.ToVietnamTime(flight.GioKhoiHanh).DateTime).AddYears(-18))))
            errors["journey"] = "Nhóm có trẻ em cần ít nhất một người lớn từ 18 tuổi đi cùng.";
        return errors;
    }

    public static void Normalize(JourneyBookingForm form)
    {
        form.ContactName = form.ContactName?.Trim() ?? ""; form.Email = form.Email?.Trim().ToLowerInvariant() ?? "";
        form.Phone = form.Phone?.Trim() ?? "";
        foreach (var row in form.People)
        {
            var p = row.Person;
            p.LastName = Regex.Replace(p.LastName?.Trim() ?? "", @"\s+", " ");
            p.FirstName = Regex.Replace(p.FirstName?.Trim() ?? "", @"\s+", " ");
            p.Gender = p.Gender?.Trim() ?? ""; p.DateOfBirth = p.DateOfBirth?.Trim() ?? "";
            p.Nationality = p.Nationality?.Trim() ?? ""; p.IdentityNumber = p.IdentityNumber?.Trim().ToUpperInvariant() ?? "";
            row.Seats = row.Seats.Select(value => value?.Trim().ToUpperInvariant() ?? "").ToList();
            row.Baggage = row.Baggage.Select(value => value?.Trim() ?? "").ToList();
        }
    }
}
