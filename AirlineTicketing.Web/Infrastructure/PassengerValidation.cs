using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using AirlineTicketing.Web.ViewModels;
using AirlineTicketing.BUS;
using AirlineTicketing.Domain;

namespace AirlineTicketing.Web.Infrastructure;

public static class PassengerValidation
{
    public static Dictionary<string, string> Validate(PassengerFormModel passenger, DateOnly departureDate,
        LoaiHanhKhach type = LoaiHanhKhach.NguoiLon, bool validateContact = true)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(passenger.LastName) || passenger.LastName.Length > 70)
            errors["lastName"] = "Vui lòng nhập họ, tối đa 70 ký tự.";
        if (string.IsNullOrWhiteSpace(passenger.FirstName) || passenger.FirstName.Length > 70)
            errors["firstName"] = "Vui lòng nhập tên đệm và tên, tối đa 70 ký tự.";
        if (passenger.Gender is not ("Nam" or "Nữ" or "Khác"))
            errors["gender"] = "Vui lòng chọn giới tính hợp lệ.";
        if (!FlightMapping.TryParseDate(passenger.DateOfBirth, out var birthDate)
            || birthDate > departureDate || birthDate < departureDate.AddYears(-120))
            errors["dateOfBirth"] = "Ngày sinh không hợp lệ. Nhập theo định dạng dd/MM/yyyy.";
        else if (JourneyRules.ValidateAge(birthDate, departureDate, type) is string ageError)
            errors["dateOfBirth"] = ageError;
        if (string.IsNullOrWhiteSpace(passenger.Nationality) || passenger.Nationality.Length > 100)
            errors["nationality"] = "Vui lòng nhập quốc tịch hợp lệ.";
        if ((type == LoaiHanhKhach.NguoiLon || !string.IsNullOrWhiteSpace(passenger.IdentityNumber))
            && !Regex.IsMatch(passenger.IdentityNumber ?? "", "^[A-Za-z0-9]{6,30}$", RegexOptions.CultureInvariant))
            errors["identityNumber"] = "Số giấy tờ phải có 6–30 chữ cái hoặc chữ số.";
        if (validateContact && (string.IsNullOrWhiteSpace(passenger.Email) || passenger.Email.Length > 254
            || !new EmailAddressAttribute().IsValid(passenger.Email)))
            errors["email"] = "Vui lòng nhập email liên hệ đúng định dạng.";
        var phone = passenger.Phone ?? "";
        var digits = phone.Count(char.IsDigit);
        if (validateContact && (phone.Length > 20 || digits is < 9 or > 15
            || !Regex.IsMatch(phone, @"^\+?[0-9 ()-]+$", RegexOptions.CultureInvariant)))
            errors["phone"] = "Số điện thoại phải có 9–15 chữ số, có thể bắt đầu bằng +.";
        return errors;
    }
}
