using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HouseOfRuns.Api.Models;
using HouseOfRuns.Api.Security;

namespace HouseOfRuns.Api.Services;

public sealed class TokenService(IConfiguration configuration)
{
    private readonly byte[] signingKey = Encoding.UTF8.GetBytes(
        configuration["Auth:TokenSigningKey"] ?? throw new InvalidOperationException("Auth signing key is missing."));

    private readonly int tokenLifetimeMinutes = configuration.GetValue("Auth:TokenLifetimeMinutes", 720);

    public string CreateToken(AppUser user)
    {
        var payload = new TokenPayload(
            user.Id,
            user.UserName,
            user.Email,
            user.DisplayName,
            user.Role,
            DateTimeOffset.UtcNow.AddMinutes(tokenLifetimeMinutes).ToUnixTimeSeconds());

        var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signature = Sign(encodedPayload);
        return $"{encodedPayload}.{signature}";
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        var expectedSignature = Sign(parts[0]);
        if (!FixedTimeEquals(expectedSignature, parts[1]))
        {
            return null;
        }

        var payload = JsonSerializer.Deserialize<TokenPayload>(Base64UrlDecode(parts[0]));
        if (payload is null || payload.ExpiresAtUnix < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return null;
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, payload.UserId.ToString()),
            new Claim(ClaimTypes.Name, payload.UserName),
            new Claim(ClaimTypes.Email, payload.Email),
            new Claim(ClaimTypes.Role, payload.Role),
            new Claim("display_name", payload.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, TokenAuthenticationDefaults.Scheme);
        return new ClaimsPrincipal(identity);
    }

    private string Sign(string encodedPayload)
    {
        using var hmac = new HMACSHA256(signingKey);
        return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(encodedPayload)));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private sealed record TokenPayload(
        Guid UserId,
        string UserName,
        string Email,
        string DisplayName,
        string Role,
        long ExpiresAtUnix);
}
