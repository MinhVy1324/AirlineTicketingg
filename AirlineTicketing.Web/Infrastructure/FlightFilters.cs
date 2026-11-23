using System.Globalization;
using System.Text.RegularExpressions;
using AirlineTicketing.BUS;
using AirlineTicketing.DTO;

namespace AirlineTicketing.Web.Infrastructure;

public sealed record DepartureWindow(string Key, string Label, int FromHour, int ToHour)
{
    public bool Contains(DateTimeOffset departure)
    {
        var hour = FlightMapping.ToVietnamTime(departure).Hour;
        return hour >= FromHour && hour < ToHour;
    }
}

/// <summary>Bộ lọc của trang kết quả; các giới hạn giá dùng tổng giá bắt buộc mỗi khách.</summary>
public sealed class FlightFilters
{
    public const decimal MaximumPrice = 9_999_999_999m;
    public static IReadOnlyList<DepartureWindow> Windows { get; } = Array.AsReadOnly<DepartureWindow>(
    [
        new("night", "Đêm / sáng sớm · 00:00–04:59", 0, 5),
        new("morning", "Sáng · 05:00–11:59", 5, 12),
        new("afternoon", "Chiều · 12:00–17:59", 12, 18),
        new("evening", "Tối · 18:00–23:59", 18, 24)
    ]);

    public IReadOnlyList<string> Airlines { get; private init; } = [];
    public IReadOnlyList<string> Times { get; private init; } = [];
    public string MinPriceText { get; private init; } = "";
    public string MaxPriceText { get; private init; } = "";
    public decimal? MinPrice { get; private set; }
    public decimal? MaxPrice { get; private set; }
    public Dictionary<string, string> Errors { get; } = new(StringComparer.Ordinal);
    public bool IsValid => Errors.Count == 0;
    public bool IsActive => Airlines.Count > 0 || Times.Count > 0 || MinPriceText.Length > 0 || MaxPriceText.Length > 0;

    public string PriceRangeText => !IsValid ? UiLanguage.T("Vui lòng kiểm tra các giá trị bộ lọc.", "Review the filter values.")
        : MinPrice.HasValue && MaxPrice.HasValue ? $"{Money.Vnd(MinPrice.Value)} – {Money.Vnd(MaxPrice.Value)}"
        : MinPrice.HasValue ? UiLanguage.T($"Từ {Money.Vnd(MinPrice.Value)}", $"From {Money.Vnd(MinPrice.Value)}")
        : MaxPrice.HasValue ? UiLanguage.T($"Đến {Money.Vnd(MaxPrice.Value)}", $"Up to {Money.Vnd(MaxPrice.Value)}")
        : UiLanguage.T("Không giới hạn giá", "Any fare");

    public static FlightFilters Parse(IEnumerable<string>? airlines = null, IEnumerable<string>? times = null,
        string? minPrice = null, string? maxPrice = null)
    {
        var filters = new FlightFilters
        {
            Airlines = Normalize(airlines, upper: true), Times = Normalize(times, upper: false),
            MinPriceText = minPrice?.Trim() ?? "", MaxPriceText = maxPrice?.Trim() ?? ""
        };
        if (filters.Airlines.Count > 30 || filters.Airlines.Any(code => !Regex.IsMatch(code, "^[A-Z0-9]{1,5}$", RegexOptions.CultureInvariant)))
            filters.Errors["airlines"] = "Mã hãng hàng không không hợp lệ. Vui lòng chọn lại hãng.";
        if (filters.Times.Any(key => !Windows.Any(window => window.Key == key)))
            filters.Errors["times"] = "Khung giờ không hợp lệ. Vui lòng chọn lại.";
        filters.MinPrice = filters.ParsePrice(filters.MinPriceText, "minPrice");
        filters.MaxPrice = filters.ParsePrice(filters.MaxPriceText, "maxPrice");
        if (filters.MinPrice.HasValue && filters.MaxPrice.HasValue && filters.MinPrice > filters.MaxPrice)
            filters.Errors["priceRange"] = "Giá từ không được lớn hơn giá đến.";
        return filters;
    }

    public IReadOnlyList<ChuyenBayDTO> Apply(IEnumerable<ChuyenBayDTO> flights, ChinhSachDatVe policy,
        bool ignoreAirlines = false, bool ignoreTimes = false, FareClass? cabin = null)
    {
        if (!IsValid) return [];
        return flights.Where(flight =>
        {
            var price = JourneyRules.AdultTotal(flight.GiaCoBan, policy, cabin ?? JourneyRules.Economy);
            return (ignoreAirlines || Airlines.Count == 0 || Airlines.Contains(flight.MaHangHangKhong, StringComparer.OrdinalIgnoreCase))
                && (ignoreTimes || Times.Count == 0 || Windows.Any(window => Times.Contains(window.Key) && window.Contains(flight.GioKhoiHanh)))
                && (!MinPrice.HasValue || price >= MinPrice.Value)
                && (!MaxPrice.HasValue || price <= MaxPrice.Value);
        }).ToArray();
    }

    private decimal? ParsePrice(string raw, string field)
    {
        if (raw.Length == 0) return null;
        if (raw.Length <= 10 && raw.All(character => character is >= '0' and <= '9')
            && decimal.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            && amount <= MaximumPrice) return amount;
        Errors[field] = "Nhập giá bằng số nguyên không âm, tối đa 9.999.999.999 ₫, không dùng dấu phân cách.";
        return null;
    }

    private static string[] Normalize(IEnumerable<string>? values, bool upper) => (values ?? [])
        .SelectMany(value => (value ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        .Select(value => upper ? value.ToUpperInvariant() : value.ToLowerInvariant())
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
