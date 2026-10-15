namespace QLHopDongHoSo.API.DTOs.Contracts;

public class ContractResponse
{
    public int ContractId { get; set; }

    public string ContractCode { get; set; } = string.Empty;

    public int ApplicationId { get; set; }

    public string? ApplicationCode { get; set; }

    public string? CustomerName { get; set; }

    public int ContractTypeId { get; set; }

    public string? ContractTypeName { get; set; }

    public int BranchId { get; set; }

    public string? BranchName { get; set; }

    public int CreatedBy { get; set; }

    public string? CreatedByUsername { get; set; }

    public DateTime? SignedDate { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public decimal? ContractValue { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    // Compatibility properties for UI/old clients
    public string ContractName => !string.IsNullOrEmpty(ContractTypeName) ? ContractTypeName : ContractCode;

    public DateTime? StartDate => EffectiveDate;

    public DateTime? EndDate => ExpiryDate;
}