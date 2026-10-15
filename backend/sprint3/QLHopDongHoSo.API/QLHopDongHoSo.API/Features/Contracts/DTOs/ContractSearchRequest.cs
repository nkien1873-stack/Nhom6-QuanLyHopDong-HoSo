namespace QLHopDongHoSo.API.DTOs.Contracts;

public class ContractSearchRequest
{
    public string? Keyword { get; set; }

    public string? Status { get; set; }

    public int? BranchId { get; set; }

    public int? ContractTypeId { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}