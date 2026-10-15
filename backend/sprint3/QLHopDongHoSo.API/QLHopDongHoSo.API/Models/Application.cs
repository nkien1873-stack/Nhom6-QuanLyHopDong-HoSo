namespace QLHopDongHoSo.API.Models;

public class Application
{
    public int ApplicationId { get; set; }

    public string ApplicationCode { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public string ApplicationType { get; set; } = string.Empty;

    public int? AssignedTo { get; set; }

    public int BranchId { get; set; }

    public string Status { get; set; } = "DRAFT";

    public string? Priority { get; set; } = "NORMAL";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SubmittedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    public Customer? Customer { get; set; }

    public User? Assignee { get; set; }

    public Branch? Branch { get; set; }

    public ICollection<Document> Documents { get; set; } = new List<Document>();

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public ICollection<Approval> Approvals { get; set; } = new List<Approval>();
}
