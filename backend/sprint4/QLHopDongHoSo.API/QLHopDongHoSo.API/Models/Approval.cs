namespace QLHopDongHoSo.API.Models;

public class Approval
{
    public int ApprovalId { get; set; }

    public int ApplicationId { get; set; }

    public int ApproverId { get; set; }

    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED

    public string? Comment { get; set; }

    public DateTime? ApprovedAt { get; set; }

    // Navigation properties
    public Application? Application { get; set; }

    public User? Approver { get; set; }
}
