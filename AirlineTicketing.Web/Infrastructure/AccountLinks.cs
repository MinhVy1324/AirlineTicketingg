using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace AirlineTicketing.Web.Infrastructure;

public sealed class AccountLinks(IDataProtectionProvider protection, IConfiguration configuration)
{
    public const string CredentialClaim = "credential_version";
    public const string UsernameAccountClaim = "username_account";
    public const int LifetimeMinutes = 30;

    public sealed record Proof(string Email, int AccountId = 0, string? Credential = null);

    public string Create(string purpose, string path, Proof proof)
    {
        var origin = new Uri(configuration["Auth:PublicBaseUrl"] ?? "http://127.0.0.1:8080", UriKind.Absolute);
        var token = Protector(purpose).Protect(JsonSerializer.Serialize(proof), TimeSpan.FromMinutes(LifetimeMinutes));
        return QueryHelpers.AddQueryString(new Uri(origin, path).AbsoluteUri, "token", token);
    }

    public Proof? Read(string purpose, string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return null;
        try
        {
            return JsonSerializer.Deserialize<Proof>(Protector(purpose).Unprotect(token));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    public static string Credential(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));

    private ITimeLimitedDataProtector Protector(string purpose) =>
        protection.CreateProtector("AirlineTicketing.AccountLinks.v1", purpose).ToTimeLimitedDataProtector();
}
