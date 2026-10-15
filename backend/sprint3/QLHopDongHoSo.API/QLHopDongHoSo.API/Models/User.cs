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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

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