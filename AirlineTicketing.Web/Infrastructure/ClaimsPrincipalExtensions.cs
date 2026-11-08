using System.Security.Claims;

namespace AirlineTicketing.Web.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static int? TaiKhoanId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }

    public static int? KhachHangId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("khach_hang_id");
        return int.TryParse(value, out var id) ? id : null;
    }

    public static string DisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue("display_name")
        ?? user.Identity?.Name
        ?? "Người dùng";

    public static string Initials(this ClaimsPrincipal user)
    {
        var name = user.DisplayName().Trim();
        if (string.IsNullOrEmpty(name))
        {
            return "??";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 1
            ? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant()
            : $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }
}
