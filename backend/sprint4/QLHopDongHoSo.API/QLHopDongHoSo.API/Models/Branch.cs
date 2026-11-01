namespace QLHopDongHoSo.API.Models;

public class Branch
{
    public int BranchId { get; set; }

    public string BranchCode { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string Status { get; set; } = "ACTIVE";

    // Navigation properties
    public ICollection<User> Users { get; set; } = new List<User>();

    public ICollection<Application> Applications { get; set; } = new List<Application>();

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
