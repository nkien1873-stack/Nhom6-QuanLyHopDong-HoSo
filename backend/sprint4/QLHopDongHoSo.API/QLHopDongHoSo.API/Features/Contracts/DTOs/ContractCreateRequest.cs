namespace QLHopDongHoSo.API.DTOs.Contracts;

public class ContractCreateRequest
{
    // Để trống => hệ thống tự sinh mã
    public string? ContractCode { get; set; }

    public int ApplicationId { get; set; }

    // Không có ApplicationId thì chọn khách hàng => tự tạo hồ sơ
    public int? CustomerId { get; set; }

    public int ContractTypeId { get; set; }

    public int BranchId { get; set; }

    public DateTime? SignedDate { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public decimal? ContractValue { get; set; }

    // Chi tiết khoản vay/hợp đồng
    public int? TermMonths { get; set; }

    public decimal? InterestRate { get; set; }

    public string? Purpose { get; set; }

    public string? Collateral { get; set; }

    public string Status { get; set; } = "DRAFT";

    // Compatibility fields from old DTO
    public string? ContractName { get; set; }

    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}