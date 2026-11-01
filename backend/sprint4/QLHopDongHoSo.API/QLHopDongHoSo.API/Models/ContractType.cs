namespace QLHopDongHoSo.API.Models;

public class ContractType
{
    public int ContractTypeId { get; set; }

    public string TypeName { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Navigation properties
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
