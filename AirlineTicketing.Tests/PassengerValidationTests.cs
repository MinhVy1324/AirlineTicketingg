using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;

namespace AirlineTicketing.Tests;

public sealed class PassengerValidationTests
{
    [Fact]
    public void HoTenTiengVietVaSoQuocTe_HopLe()
    {
        var person = ValidPerson();
        Assert.Empty(PassengerValidation.Validate(person, new DateOnly(2026, 12, 1)));
    }

    [Theory]
    [InlineData("31/02/2000")]
    [InlineData("01/01/2030")]
    [InlineData("01/12/2015")]
    public void NgaySinhSaiHoacChuaDuTuoi_BiChan(string birthDate)
    {
        var person = ValidPerson();
        person.DateOfBirth = birthDate;
        Assert.Contains("dateOfBirth", PassengerValidation.Validate(person, new DateOnly(2026, 12, 1)).Keys);
    }

    [Fact]
    public void EmailDienThoaiVaGiayToSai_KhongQuaKiemTraMayChu()
    {
        var person = ValidPerson();
        person.Email = "not-an-email";
        person.Phone = "abcdef123";
        person.IdentityNumber = "<script>";
        var errors = PassengerValidation.Validate(person, new DateOnly(2026, 12, 1));
        Assert.Contains("email", errors.Keys);
        Assert.Contains("phone", errors.Keys);
        Assert.Contains("identityNumber", errors.Keys);
    }

    private static PassengerFormModel ValidPerson() => new()
    {
        LastName = "Nguyễn", FirstName = "Văn An", Gender = "Nam", DateOfBirth = "01/01/2000",
        Nationality = "Việt Nam", IdentityNumber = "079000000001", Email = "an@example.test", Phone = "+84 912 345 678"
    };
}
