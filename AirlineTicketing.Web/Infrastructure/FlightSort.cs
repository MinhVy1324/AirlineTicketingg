using AirlineTicketing.DTO;

namespace AirlineTicketing.Web.Infrastructure;

public sealed record FlightSortOption(string Key, string Label);

/// <summary>Các kiểu sắp xếp danh sách chuyến bay ở trang kết quả tìm chuyến.</summary>
public static class FlightSort
{
    public const string Recommended = "recommended";
    public const string Price = "price";
    public const string Departure = "departure";
    public const string Duration = "duration";

    public static readonly IReadOnlyList<FlightSortOption> Options = new[]
    {
        new FlightSortOption(Recommended, "Đề xuất"),
        new FlightSortOption(Price, "Giá thấp nhất"),
        new FlightSortOption(Departure, "Giờ bay sớm nhất"),
        new FlightSortOption(Duration, "Bay nhanh nhất")
    };

    public static string Normalize(string? key) =>
        Options.FirstOrDefault(option => option.Key == key)?.Key ?? Recommended;

    public static string LabelFor(string key) =>
        Options.FirstOrDefault(option => option.Key == key)?.Label ?? Options[0].Label;

    public static IReadOnlyList<ChuyenBayDTO> Apply(IReadOnlyList<ChuyenBayDTO> items, string key) => key switch
    {
        Price => items.OrderBy(x => x.GiaCoBan).ThenBy(x => x.GioKhoiHanh).ToArray(),
        Departure => items.OrderBy(x => x.GioKhoiHanh).ThenBy(x => x.GiaCoBan).ToArray(),
        Duration => items
            .OrderBy(x => x.GioHaCanh - x.GioKhoiHanh)
            .ThenBy(x => x.GiaCoBan)
            .ToArray(),
        // Đề xuất: cân bằng giữa giá vé và thời gian bay, mỗi phút bay tính 5.000 ₫.
        _ => items
            .OrderBy(x => x.GiaCoBan + (decimal)(x.GioHaCanh - x.GioKhoiHanh).TotalMinutes * 5_000m)
            .ThenBy(x => x.GiaCoBan)
            .ToArray()
    };
}
