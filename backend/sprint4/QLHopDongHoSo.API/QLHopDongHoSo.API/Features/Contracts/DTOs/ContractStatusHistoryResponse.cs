namespace QLHopDongHoSo.API.DTOs.Contracts;

public class ContractStatusHistoryResponse
{
    public int HistoryId { get; set; }

    public int ContractId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = string.Empty;

    public int ChangedBy { get; set; }

    public string? ChangedByName { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Reason { get; set; }
}
