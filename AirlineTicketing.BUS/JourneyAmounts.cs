using AirlineTicketing.Domain;

namespace AirlineTicketing.BUS;

/// <summary>Phân bổ tiền thu/hoàn theo giá lưu ở từng chặng, giữ nguyên tổng sau làm tròn.</summary>
public static class JourneyAmounts
{
    public static IReadOnlyList<(Ve? Ticket, decimal Amount)> Allocate(DatVe booking, decimal amount)
    {
        var legs = booking.Ves.OrderBy(x => x.Id).GroupBy(x => x.ChuyenBayId).ToArray();
        if (legs.Length == 0) return [(null, amount)];
        var weights = legs.Select(leg => leg.Sum(x => x.GiaVe + x.ThuePhi + x.PhiDichVu + x.PhiHanhLy + x.PhiChoNgoi)).ToArray();
        var total = weights.Sum();
        var result = new List<(Ve?, decimal)>();
        var remaining = amount;
        for (var i = 0; i < legs.Length; i++)
        {
            var part = i == legs.Length - 1 ? remaining
                : decimal.Round(total > 0 ? amount * weights[i] / total : amount / legs.Length, 2, MidpointRounding.AwayFromZero);
            result.Add((legs[i].First(), part)); remaining -= part;
        }
        return result;
    }
}
