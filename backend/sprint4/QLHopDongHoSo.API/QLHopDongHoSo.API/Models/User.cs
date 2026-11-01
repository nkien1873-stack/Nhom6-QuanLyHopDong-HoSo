namespace QLHopDongHoSo.API.Models;

public class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public int RoleId { get; set; }

    public int? BranchId { get; set; }

    public string Status { get; set; } = "ACTIVE";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Chống dò mật khẩu: đếm số lần sai liên tiếp, quá ngưỡng thì khóa tạm tới LockoutEnd
    public int FailedLoginCount { get; set; }

    public DateTime? LockoutEnd { get; set; }

    // Phiên bản token: tăng khi đăng xuất / đổi vai trò, chi nhánh / khóa => token phát hành trước đó hết hiệu lực
    public int TokenVersion { get; set; }

    // Helper property to maintain backwards compatibility
    public bool IsActive => string.Equals(Status, "ACTIVE", StringComparison.OrdinalIgnoreCase);

    // Navigation properties
    public Role? Role { get; set; }

    public Branch? Branch { get; set; }

    public ICollection<Application> AssignedApplications { get; set; } = new List<Application>();

    public ICollection<Contract> CreatedContracts { get; set; } = new List<Contract>();

    public ICollection<Document> UploadedDocuments { get; set; } = new List<Document>();

    public ICollection<Approval> Approvals { get; set; } = new List<Approval>();

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}