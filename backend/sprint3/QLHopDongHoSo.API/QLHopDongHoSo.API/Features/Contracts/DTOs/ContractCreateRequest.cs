namespace QLHopDongHoSo.API.DTOs.Contracts;

public class ContractCreateRequest
{
    public string ContractCode { get; set; } = string.Empty;

    public int ApplicationId { get; set; }

    public int ContractTypeId { get; set; }

    public int BranchId { get; set; }

    public DateTime? SignedDate { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public decimal? ContractValue { get; set; }

    public string Status { get; set; } = "DRAFT";

    // Compatibility fields from old DTO
    public string? ContractName { get; set; }

    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}