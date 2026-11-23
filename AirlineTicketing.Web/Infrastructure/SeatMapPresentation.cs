using AirlineTicketing.DTO;
using AirlineTicketing.Web.ViewModels;

namespace AirlineTicketing.Web.Infrastructure;

internal static class SeatMapPresentation
{
    internal static IReadOnlyList<SeatRow> Build(SoDoGheDTO map, string? selectedSeat = null, IReadOnlySet<string>? ownSeats = null) =>
        map.Ghes.GroupBy(seat => RowNumber(seat.SoGhe)).Where(row => row.Key > 0).OrderBy(row => row.Key)
            .Select(row => new SeatRow(row.Key.ToString(), row.OrderBy(seat => seat.SoGhe[^1]).Select(seat =>
            {
                var state = string.Equals(seat.SoGhe, selectedSeat, StringComparison.OrdinalIgnoreCase)
                    ? SeatState.Selected
                    : string.Equals(seat.TrangThai, "Trong", StringComparison.OrdinalIgnoreCase) || ownSeats?.Contains(seat.SoGhe) == true
                        ? SeatState.Free : SeatState.Taken;
                return new SeatCell(seat.SoGhe[^1].ToString(), state, seat.SoGhe, seat.LoaiGhe,
                    seat.TenLoaiGhe, seat.PhuThu, PositionFor(seat.SoGhe));
            }).ToArray())).ToArray();

    internal static int RowNumber(string seatCode)
    {
        var digits = new string(seatCode.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var row) ? row : 0;
    }

    internal static string PositionFor(string seatCode) =>
        string.IsNullOrWhiteSpace(seatCode) ? string.Empty : char.ToUpperInvariant(seatCode[^1]) switch
        {
            'A' or 'F' => "Cạnh cửa sổ",
            'C' or 'D' => "Cạnh lối đi",
            'B' or 'E' => "Ở giữa",
            _ => string.Empty
        };
}
