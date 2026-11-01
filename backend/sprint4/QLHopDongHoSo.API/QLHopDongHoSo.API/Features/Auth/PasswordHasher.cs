namespace QLHopDongHoSo.API.Services;

// BCrypt dùng chung một work factor (mặc định thư viện là 11 ≈ 140 ms/lần).
// 10 vẫn đủ an toàn cho mật khẩu và nhanh gấp đôi => đăng nhập đồng loạt đỡ nghẽn CPU.
public static class PasswordHasher
{
    public static int WorkFactor { get; set; } = 10;

    public static string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    // Hash dạng $2a$11$... => work factor nằm ở đoạn thứ 3
    public static bool NeedsRehash(string hash)
    {
        var parts = hash.Split('$');
        return parts.Length < 4 || !int.TryParse(parts[2], out var cost) || cost != WorkFactor;
    }
}

// Tài khoản bị khóa tạm do đăng nhập sai nhiều lần (AuthController trả 429)
public class LoginLockedException : Exception
{
    public LoginLockedException(string message) : base(message)
    {
    }
}
