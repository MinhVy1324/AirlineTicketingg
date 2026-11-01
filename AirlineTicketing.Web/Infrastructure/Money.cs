using System.Globalization;
using System.Text;

namespace AirlineTicketing.Web.Infrastructure;

public static class Money
{
    /// <summary>Formats an amount using Vietnamese grouping, e.g. 2200000 -> "2.200.000 ₫".</summary>
    public static string Vnd(decimal amount)
    {
        var rounded = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
        var digits = Math.Abs(rounded).ToString("0", CultureInfo.InvariantCulture);
        var builder = new StringBuilder(digits.Length + digits.Length / 3 + 2);

        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0)
            {
                builder.Append('.');
            }

            builder.Append(digits[i]);
        }

        if (rounded < 0)
        {
            builder.Insert(0, '-');
        }

        return builder.Append(" ₫").ToString();
    }

    public static string Date(DateTimeOffset value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Time(DateTimeOffset value) => value.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>"2 giờ 10 phút"</summary>
    public static string Duration(TimeSpan value)
    {
        var hours = (int)value.TotalHours;
        var minutes = value.Minutes;

        if (hours <= 0)
        {
            return $"{minutes} phút";
        }

        return minutes == 0 ? $"{hours} giờ" : $"{hours} giờ {minutes} phút";
    }

    /// <summary>"Thứ Sáu, 20/11/2026"</summary>
    public static string LongDate(DateTimeOffset value)
    {
        var weekday = value.DayOfWeek switch
        {
            DayOfWeek.Monday => "Thứ Hai",
            DayOfWeek.Tuesday => "Thứ Ba",
            DayOfWeek.Wednesday => "Thứ Tư",
            DayOfWeek.Thursday => "Thứ Năm",
            DayOfWeek.Friday => "Thứ Sáu",
            DayOfWeek.Saturday => "Thứ Bảy",
            _ => "Chủ Nhật"
        };

        return $"{weekday}, {Date(value)}";
    }
}
