namespace AirlineTicketing.Web.Infrastructure;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string KhachHang = "KhachHang";
    public const string Staff = "Admin,Manager";

    public static string HomePathFor(string? role) => role switch
    {
        Admin => "/admin",
        Manager => "/quan-ly",
        _ => "/"
    };
}
