namespace QLHopDongHoSo.API.DTOs.Contracts;

public class ContractSearchResponse
{
    public List<ContractResponse> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}