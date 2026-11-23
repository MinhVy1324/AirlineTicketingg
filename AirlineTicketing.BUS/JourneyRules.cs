using AirlineTicketing.Domain;

namespace AirlineTicketing.BUS;

public sealed record PassengerParty(int Adults = 1, int Children = 0, int Infants = 0)
{
    public int Total => Adults + Children + Infants;
    public int Seats => Adults + Children;
    public string Summary => string.Join(", ", new[]
    {
        Adults > 0 ? $"{Adults} người lớn" : null, Children > 0 ? $"{Children} trẻ em" : null,
        Infants > 0 ? $"{Infants} em bé" : null
    }.Where(value => value is not null));
    public void Validate()
    {
        if (Adults is < 1 or > 9 || Children is < 0 or > 8 || Infants is < 0 or > 9 || Total > 9 || Infants > Adults)
            throw new ArgumentException("Mỗi đơn có 1–9 hành khách, ít nhất một người lớn; số em bé không vượt số người lớn.");
    }
    public IReadOnlyList<LoaiHanhKhach> Types => Enumerable.Repeat(LoaiHanhKhach.NguoiLon, Adults)
        .Concat(Enumerable.Repeat(LoaiHanhKhach.TreEm, Children))
        .Concat(Enumerable.Repeat(LoaiHanhKhach.EmBe, Infants)).ToArray();
}

public sealed record FareClass(string Code, string Name, decimal Multiplier);

/// <summary>Bảng giá demo; không thay thế điều kiện và biểu giá của một hãng bay thực tế.</summary>
public static class JourneyRules
{
    public static FareClass Economy { get; } = new("PhoThong", "Phổ thông", 1m);
    public static FareClass Business { get; } = new("ThuongGia", "Thương gia", 1.8m);
    public static IReadOnlyList<FareClass> Cabins { get; } = Array.AsReadOnly(new[] { Economy, Business });
    public static FareClass Cabin(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "phothong" or "phổ thông" or "economy" => Economy,
        "thuonggia" or "thương gia" or "business" => Business,
        _ => throw new ArgumentException("Hạng vé không hợp lệ. Vui lòng chọn Phổ thông hoặc Thương gia.")
    };
    public static string TypeName(LoaiHanhKhach type) => type switch
    {
        LoaiHanhKhach.NguoiLon => "Người lớn", LoaiHanhKhach.TreEm => "Trẻ em", _ => "Em bé"
    };
    public static decimal BaseFare(decimal basePrice, FareClass cabin, LoaiHanhKhach type) => decimal.Round(
        basePrice * cabin.Multiplier * (type switch { LoaiHanhKhach.TreEm => .75m, LoaiHanhKhach.EmBe => .10m, _ => 1m }),
        0, MidpointRounding.AwayFromZero);
    public static decimal Tax(ChinhSachDatVe policy, LoaiHanhKhach type) => type == LoaiHanhKhach.EmBe ? 0m : policy.Tax;
    public static decimal ServiceFee(ChinhSachDatVe policy, LoaiHanhKhach type) => type == LoaiHanhKhach.EmBe ? 0m : policy.ServiceFee;
    public static decimal AdultTotal(decimal price, ChinhSachDatVe policy, FareClass cabin) =>
        BaseFare(price, cabin, LoaiHanhKhach.NguoiLon) + policy.Tax + policy.ServiceFee;
    public static decimal PartyTotal(decimal price, ChinhSachDatVe policy, FareClass cabin, PassengerParty party) =>
        party.Adults * AdultTotal(price, policy, cabin)
        + party.Children * (BaseFare(price, cabin, LoaiHanhKhach.TreEm) + policy.Tax + policy.ServiceFee)
        + party.Infants * BaseFare(price, cabin, LoaiHanhKhach.EmBe);
    public static string? ValidateAge(DateOnly birthDate, DateOnly departure, LoaiHanhKhach type)
    {
        if (birthDate > departure || birthDate < departure.AddYears(-120)) return "Ngày sinh không hợp lệ.";
        return type switch
        {
            LoaiHanhKhach.NguoiLon when birthDate.AddYears(12) > departure => "Người lớn phải từ 12 tuổi tại ngày bay.",
            LoaiHanhKhach.TreEm when birthDate.AddYears(2) > departure || birthDate.AddYears(12) <= departure => "Trẻ em phải từ 2 đến dưới 12 tuổi tại ngày bay.",
            LoaiHanhKhach.EmBe when birthDate.AddDays(7) > departure || birthDate.AddYears(2) <= departure => "Em bé phải từ 7 ngày đến dưới 2 tuổi tại ngày bay theo quy tắc demo.",
            _ => null
        };
    }
}
