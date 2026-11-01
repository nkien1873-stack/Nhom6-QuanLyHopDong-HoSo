using System.Text.Json.Serialization;
using QLHopDongHoSo.API.Services;

namespace QLHopDongHoSo.API.DTOs.Contracts;

public class ContractResponse
{
    public int ContractId { get; set; }

    public string ContractCode { get; set; } = string.Empty;

    public int ApplicationId { get; set; }

    public string? ApplicationCode { get; set; }

    public int? CustomerId { get; set; }

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

    // Chi tiết khoản vay/hợp đồng
    public int? TermMonths { get; set; }

    public decimal? InterestRate { get; set; }

    public string? Purpose { get; set; }

    public string? Collateral { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    // Lần sửa/đổi trạng thái gần nhất
    public DateTime UpdatedAt { get; set; }

    public string? CreatedByName { get; set; }

    public int DocumentCount { get; set; }

    // Thông tin khách hàng hiển thị ở trang chi tiết (đỡ phải tải cả danh sách khách hàng)
    public string? CustomerKind { get; set; }

    public string? CustomerIdNo { get; set; }

    public string? CustomerRepresentative { get; set; }

    public string? CustomerPhone { get; set; }

    public string? CustomerEmail { get; set; }

    public string? CustomerAddress { get; set; }

    // Phiên bản bản ghi (rowversion) dạng base64 – gửi lại khi sửa để phát hiện sửa chồng
    [JsonIgnore]
    public byte[]? RowVersionBytes { get; set; }

    public string? RowVersion => RowVersionBytes == null ? null : Convert.ToBase64String(RowVersionBytes);

    // Compatibility properties for UI/old clients
    public string ContractName => !string.IsNullOrEmpty(ContractTypeName) ? ContractTypeName : ContractCode;

    public DateTime? StartDate => EffectiveDate;

    public DateTime? EndDate => ExpiryDate;

    // Cảnh báo kèm theo khi trình/duyệt (WARN_DOC_02 – đang nợ chứng từ); chỉ có trong response đổi trạng thái
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Warnings { get; set; }

    // Các trạng thái có thể chuyển tới từ trạng thái hiện tại (FE dùng để hiện nút thao tác)
    public IEnumerable<string> NextStatuses => ContractStatuses.NextStatuses(Status);
}