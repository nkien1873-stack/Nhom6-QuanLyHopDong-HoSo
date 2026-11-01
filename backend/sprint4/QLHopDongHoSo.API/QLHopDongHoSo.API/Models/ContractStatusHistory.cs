namespace QLHopDongHoSo.API.Models;

public class ContractStatusHistory
{
    public int HistoryId { get; set; }

    public int ContractId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = string.Empty;

    public int ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.Now;

    public string? Reason { get; set; }

    // Navigation properties
    public Contract? Contract { get; set; }

    public User? Changer { get; set; }
}
