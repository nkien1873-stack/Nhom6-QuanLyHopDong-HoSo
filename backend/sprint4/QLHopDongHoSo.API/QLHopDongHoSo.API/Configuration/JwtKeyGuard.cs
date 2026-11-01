using System.Security.Cryptography;

namespace QLHopDongHoSo.API.Configuration;

public static class JwtKeyGuard
{
    // Các khóa mẫu từng nằm công khai trong mã nguồn – không bao giờ được dùng để ký token
    private static readonly string[] KnownPublicKeys =
    [
        "your-super-secret-jwt-key-at-least-32-characters"
    ];

    // HMAC-SHA256 cần khóa ≥ 32 byte
    private const int MinLength = 32;

    public static string Resolve(string? configured, out bool generated)
    {
        var key = configured?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length < MinLength ||
            KnownPublicKeys.Contains(key, StringComparer.Ordinal))
        {
            generated = true;
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        generated = false;
        return key;
    }
}
