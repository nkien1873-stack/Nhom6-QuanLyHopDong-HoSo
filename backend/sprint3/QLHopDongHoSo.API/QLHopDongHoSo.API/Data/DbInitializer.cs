using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Models;

namespace QLHopDongHoSo.API.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
        // Kiểm tra kết nối
        if (!await context.Database.CanConnectAsync())
        {
            return;
        }

        // Nếu đã có User thì không tạo lại
        if (await context.Users.AnyAsync())
        {
            return;
        }

        // Lấy Role Admin
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
        var defaultBranch = await context.Branches.FirstOrDefaultAsync();

        if (adminRole != null)
        {
            var admin = new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                FullName = "System Administrator",
                Email = "admin@example.com",
                RoleId = adminRole.RoleId,
                BranchId = defaultBranch?.BranchId,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(admin);
            await context.SaveChangesAsync();
        }
    }
}