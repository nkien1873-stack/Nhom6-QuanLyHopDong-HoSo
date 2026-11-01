namespace QLHopDongHoSo.API.Services;

// Vai trò dùng trong [Authorize(Roles = ...)] và kiểm tra quyền chuyển trạng thái
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Officer = "Officer";
    public const string Auditor = "Auditor";

    // Cấp phê duyệt ngoại lệ (Giám đốc chi nhánh / Trưởng phòng QLRR): chỉ duyệt Tờ trình xin nợ chứng từ,
    // không tạo/sửa/phê duyệt hồ sơ. Có chi nhánh => duyệt trong chi nhánh mình; không gán chi nhánh (QLRR hội sở) => mọi chi nhánh.
    public const string Director = "Director";

    public const string Approvers = Admin + "," + Manager;
    public const string Editors = Admin + "," + Manager + "," + Officer;
}

// Luồng trạng thái hợp đồng:
// DRAFT -> PENDING_APPROVAL -> ACTIVE -> EXPIRED / TERMINATED
//                          \-> REJECTED -> DRAFT
// DRAFT -> CANCELLED
public static class ContractStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Active = "ACTIVE";
    public const string Rejected = "REJECTED";
    public const string Expired = "EXPIRED";
    public const string Terminated = "TERMINATED";
    public const string Cancelled = "CANCELLED";

    public static readonly string[] All =
    [
        Draft, PendingApproval, Active, Rejected, Expired, Terminated, Cancelled
    ];

    // Trạng thái đích -> các trạng thái nguồn hợp lệ
    private static readonly Dictionary<string, string[]> AllowedFrom = new()
    {
        // Bị từ chối thì sửa rồi gửi duyệt lại luôn
        [PendingApproval] = [Draft, Rejected],
        [Active] = [PendingApproval],
        [Rejected] = [PendingApproval],
        [Draft] = [PendingApproval, Rejected],
        [Expired] = [Active],
        [Terminated] = [Active],
        [Cancelled] = [Draft, Rejected]
    };

    // Chuyển sang các trạng thái này cần quyền phê duyệt (Admin/Manager)
    private static readonly HashSet<string> ApproverOnly =
    [
        Active, Rejected, Expired, Terminated
    ];

    // Bắt buộc nhập lý do
    private static readonly HashSet<string> ReasonRequired =
    [
        Rejected, Terminated, Cancelled
    ];

    public static bool IsValid(string status) => All.Contains(status);

    public static bool RequiresReason(string to) => ReasonRequired.Contains(to);

    public static bool CanTransition(string from, string to) =>
        AllowedFrom.TryGetValue(to, out var sources) && sources.Contains(from);

    public static bool RequiresApprover(string to) => ApproverOnly.Contains(to);

    public static IEnumerable<string> NextStatuses(string from) =>
        AllowedFrom.Where(kv => kv.Value.Contains(from)).Select(kv => kv.Key);
}
