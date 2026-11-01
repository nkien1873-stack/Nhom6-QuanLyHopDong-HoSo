using Microsoft.EntityFrameworkCore;

namespace QLHopDongHoSo.API.Data;

// Khóa hàng hợp đồng (UPDLOCK) trong transaction đang mở: mọi thao tác phải "kiểm tra rồi mới ghi" trên cùng một hồ sơ
// (trình duyệt / phê duyệt / xóa tài liệu / xác minh tài liệu / lập tờ trình / xóa hồ sơ) lần lượt chạy nối tiếp.
// Trước đây hai thao tác chạy song song cùng đọc trạng thái cũ rồi cùng ghi => hồ sơ chờ duyệt mà thiếu CCCD (T02),
// hợp đồng hiệu lực với CCCD bị từ chối (T03).
// Thứ tự khóa luôn là: hợp đồng trước, tài liệu sau => không tạo deadlock giữa các thao tác này.
public static class ContractLocks
{
    public sealed class LockedContract
    {
        public int ContractId { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    public static Task<List<LockedContract>> LockByIdAsync(this ApplicationDbContext context, int contractId) =>
        context.Database
            .SqlQueryRaw<LockedContract>(
                "SELECT [ContractID] AS [ContractId], [Status] FROM [CONTRACTS] WITH (UPDLOCK, ROWLOCK) WHERE [ContractID] = {0}",
                contractId)
            .ToListAsync();

    public static Task<List<LockedContract>> LockByApplicationAsync(this ApplicationDbContext context, int applicationId) =>
        context.Database
            .SqlQueryRaw<LockedContract>(
                "SELECT [ContractID] AS [ContractId], [Status] FROM [CONTRACTS] WITH (UPDLOCK, ROWLOCK) WHERE [ApplicationID] = {0}",
                applicationId)
            .ToListAsync();
}
