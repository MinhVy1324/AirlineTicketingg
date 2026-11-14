namespace AirlineTicketing.BUS;

public sealed class DatVeOptions
{
    public TimeSpan ThoiHanGiuCho { get; init; } = TimeSpan.FromMinutes(15);
    public IReadOnlyDictionary<string, decimal> HeSoHangVe { get; init; } =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["PhoThong"] = 1m,
            ["ThuongGia"] = 1.8m
        };
}
