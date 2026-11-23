using AirlineTicketing.BUS;

namespace AirlineTicketing.Web.Infrastructure;

public sealed class BookingPromotion(IConfiguration configuration, TimeProvider clock)
{
    public const string Code = "AERONEW";
    public const decimal Minimum = 3_000_000m;
    public const decimal MaximumDiscount = 100_000m;
    public DateTimeOffset EndsAt { get; } = configuration.GetValue<DateTimeOffset?>("Promotion:EndsAt")
        ?? new DateTimeOffset(2027, 1, 1, 0, 0, 0, ChuyenBayBUS.VietnamOffset);

    public bool IsActive => clock.GetUtcNow() < EndsAt;
    public string EndsAtText => EndsAt.ToOffset(ChuyenBayBUS.VietnamOffset).AddMinutes(-1).ToString("HH:mm dd/MM/yyyy");

    public static decimal Discount(decimal subtotal, decimal serviceFee) =>
        subtotal >= Minimum ? Math.Min(MaximumDiscount, serviceFee) : 0m;
}
