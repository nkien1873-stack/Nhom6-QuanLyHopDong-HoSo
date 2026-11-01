namespace QLHopDongHoSo.API.DTOs.Contracts;

public class ContractStatusChangeRequest
{
    public string Status { get; set; } = string.Empty;

    public string? Reason { get; set; }
}
