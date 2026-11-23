namespace AirlineTicketing.BUS;

/// <summary>Gói dịch vụ được định giá trên máy chủ; biểu mẫu chỉ gửi mã gói.</summary>
public sealed record BaggagePackage(string Code, int ExtraKg, decimal Price, string Label);

public static class BaggageCatalog
{
    public const string None = "none";
    public const int CabinKg = 12;
    public const int IncludedCheckedKg = 23;

    public static IReadOnlyList<BaggagePackage> Packages { get; } = Array.AsReadOnly<BaggagePackage>(
    [
        new(None, 0, 0m, "Không mua thêm"),
        new("extra10", 10, 250_000m, "+10 kg"),
        new("extra20", 20, 480_000m, "+20 kg")
    ]);

    public static BaggagePackage? Find(string? code) => Packages.FirstOrDefault(
        package => string.Equals(package.Code, code, StringComparison.Ordinal));

    public static BaggagePackage FromPrice(decimal price) =>
        Packages.FirstOrDefault(package => package.Price == price) ?? Packages[0];
}
