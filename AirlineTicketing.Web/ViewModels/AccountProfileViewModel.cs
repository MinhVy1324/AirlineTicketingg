using System.ComponentModel.DataAnnotations;
using static AirlineTicketing.Web.Infrastructure.UiLanguage;

namespace AirlineTicketing.Web.ViewModels;

public sealed class AccountProfileViewModel
{
    [Required(ErrorMessageResourceName = nameof(ProfileValidation.FullNameRequired), ErrorMessageResourceType = typeof(ProfileValidation))]
    [StringLength(150, ErrorMessageResourceName = nameof(ProfileValidation.FullNameLength), ErrorMessageResourceType = typeof(ProfileValidation))]
    public string FullName { get; set; } = "";

    public string? Email { get; set; }
    public bool UsernameAccount { get; set; }
    public bool NeedsProfile { get; set; }
    public string? ReturnUrl { get; set; }

    [StringLength(20, ErrorMessageResourceName = nameof(ProfileValidation.PhoneLength), ErrorMessageResourceType = typeof(ProfileValidation))]
    [RegularExpression(@"\+?[0-9]{7,15}", ErrorMessageResourceName = nameof(ProfileValidation.PhoneFormat), ErrorMessageResourceType = typeof(ProfileValidation))]
    public string? Phone { get; set; }
}

internal static class ProfileValidation
{
    public static string FullNameRequired => T("Nhập họ và tên.", "Enter your full name.");
    public static string FullNameLength => T("Họ và tên tối đa 150 ký tự.", "Use up to 150 characters for your full name.");
    public static string PhoneLength => T("Số điện thoại tối đa 20 ký tự.", "Use up to 20 characters for your phone number.");
    public static string PhoneFormat => T("Nhập 7–15 chữ số, có thể bắt đầu bằng dấu +.", "Enter 7–15 digits with an optional leading +.");
}
