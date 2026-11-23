using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AirlineTicketing.Web.ViewModels;

public sealed class AccountFormViewModel
{
    public string Email { get; set; } = "";
    public string? Token { get; set; }
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }
    [BindNever] public string? Error { get; set; }
    [BindNever] public bool Sent { get; set; }
    [BindNever] public bool InvalidLink { get; set; }
}
