namespace QLHopDongHoSo.API.Configuration;

public static class RateLimitPolicies
{
    // Áp cho POST /api/Auth/login – số lần/phút/IP cấu hình ở Security:LoginRateLimitPerMinute
    public const string Login = "login";
}
