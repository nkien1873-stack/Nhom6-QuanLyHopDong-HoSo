using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.Models;

namespace QLHopDongHoSo.API.Services;

// Người đang thao tác (lấy từ DB theo UserId trong JWT) và phạm vi chi nhánh của họ.
// Quy tắc: xem toàn hệ thống; thao tác ghi chỉ trong chi nhánh của mình, riêng Admin không giới hạn.
public static class ActorExtensions
{
    public static async Task<User> GetActorAsync(this ApplicationDbContext context, int userId)
    {
        var actor = await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.Status == "ACTIVE");

        return actor ?? throw new UnauthorizedAccessException(
            "Tài khoản không tồn tại hoặc đã bị khóa.");
    }

    public static bool IsAdmin(this User actor) =>
        actor.Role?.RoleName == AppRoles.Admin;

    public static bool IsApprover(this User actor) =>
        actor.Role?.RoleName is AppRoles.Admin or AppRoles.Manager;

    public static bool IsDirector(this User actor) =>
        actor.Role?.RoleName == AppRoles.Director;

    public static void EnsureCanWriteBranch(this User actor, int branchId)
    {
        if (actor.IsAdmin())
        {
            return;
        }

        if (actor.BranchId == null)
        {
            throw new UnauthorizedAccessException(
                "Tài khoản chưa được gán chi nhánh nên không thể thao tác.");
        }

        if (actor.BranchId != branchId)
        {
            throw new UnauthorizedAccessException(
                "Bạn chỉ được thao tác trên hợp đồng/hồ sơ thuộc chi nhánh của mình.");
        }
    }
}
