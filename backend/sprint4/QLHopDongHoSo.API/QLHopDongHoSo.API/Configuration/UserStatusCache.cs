using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QLHopDongHoSo.API.Data;

namespace QLHopDongHoSo.API.Configuration;

// Trạng thái + TokenVersion của tài khoản dùng khi kiểm tra JWT (Program.cs, cache 15 giây)
public static class UserStatusCache
{
    // Claim chứa TokenVersion lúc phát hành token
    public const string TokenVersionClaim = "tv";

    public static string Key(int userId) => $"user-active:{userId}";

    // Đăng xuất / đổi vai trò, chi nhánh => tăng TokenVersion: mọi token đã phát hành của tài khoản này hết hiệu lực ngay
    public static async Task RevokeTokensAsync(ApplicationDbContext context, IMemoryCache cache, int userId)
    {
        await context.Users
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.TokenVersion, u => u.TokenVersion + 1));
        cache.Remove(Key(userId));
    }
}
