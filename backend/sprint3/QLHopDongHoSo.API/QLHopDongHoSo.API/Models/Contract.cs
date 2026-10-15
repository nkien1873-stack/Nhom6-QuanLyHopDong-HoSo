namespace QLHopDongHoSo.API.Models;

public class Contract
{
    public int ContractId { get; set; }

    public string ContractCode { get; set; } = string.Empty;

    public int ApplicationId { get; set; }

    public int ContractTypeId { get; set; }

    public int BranchId { get; set; }

    public int CreatedBy { get; set; }

    public DateTime? SignedDate { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public decimal? ContractValue { get; set; }

    public string Status { get; set; } = "DRAFT";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Application? Application { get; set; }

    public ContractType? ContractType { get; set; }

    public Branch? Branch { get; set; }

    public User? Creator { get; set; }

    public ICollection<ContractStatusHistory> StatusHistories { get; set; } = new List<ContractStatusHistory>();
}