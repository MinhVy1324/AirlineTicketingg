using System.Security.Cryptography;

namespace AirlineTicketing.DAL.Security;

public static class PasswordSecurity
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithm,
            KeySize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        string[] parts = hashedPassword.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out int iterations))
        {
            return false;
        }

        byte[] salt = Convert.FromBase64String(parts[1]);
        byte[] expectedKey = Convert.FromBase64String(parts[2]);

        byte[] actualKey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithm,
            expectedKey.Length);

        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
