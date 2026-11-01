using System.Text.Json.Serialization;

namespace QLHopDongHoSo.API.DTOs.Contracts;

// Sửa hồ sơ kiểu "chỉ sửa trường có gửi": trường CÓ trong JSON được ghi đúng giá trị gửi lên
// (gửi null / chuỗi rỗng = xóa giá trị, vd bỏ tài sản bảo đảm, bỏ lãi suất); trường KHÔNG gửi giữ nguyên.
// System.Text.Json chỉ gọi setter của thuộc tính có trong JSON => setter ghi lại tên trường vào Provided.
public class ContractUpdateRequest
{
    [JsonIgnore]
    public HashSet<string> Provided { get; } = [];

    public bool Has(string field) => Provided.Contains(field);

    // Để trống => giữ mã cũ
    public string? ContractCode { get; set; }

    public int ContractTypeId { get; set; }

    public int BranchId { get; set; }

    private DateTime? _signedDate;
    public DateTime? SignedDate { get => _signedDate; set { _signedDate = value; Provided.Add(nameof(SignedDate)); } }

    private DateTime? _effectiveDate;
    public DateTime? EffectiveDate { get => _effectiveDate; set { _effectiveDate = value; Provided.Add(nameof(EffectiveDate)); } }

    private DateTime? _expiryDate;
    public DateTime? ExpiryDate { get => _expiryDate; set { _expiryDate = value; Provided.Add(nameof(ExpiryDate)); } }

    private decimal? _contractValue;
    public decimal? ContractValue { get => _contractValue; set { _contractValue = value; Provided.Add(nameof(ContractValue)); } }

    // Chi tiết khoản vay/hợp đồng
    private int? _termMonths;
    public int? TermMonths { get => _termMonths; set { _termMonths = value; Provided.Add(nameof(TermMonths)); } }

    private decimal? _interestRate;
    public decimal? InterestRate { get => _interestRate; set { _interestRate = value; Provided.Add(nameof(InterestRate)); } }

    private string? _purpose;
    public string? Purpose { get => _purpose; set { _purpose = value; Provided.Add(nameof(Purpose)); } }

    private string? _collateral;
    public string? Collateral { get => _collateral; set { _collateral = value; Provided.Add(nameof(Collateral)); } }

    public string Status { get; set; } = "DRAFT";

    // Phiên bản (rowVersion) lúc mở form sửa; khác bản trong DB => có người vừa sửa/duyệt => 409
    public string? RowVersion { get; set; }

    // Compatibility fields from old DTO (StartDate/EndDate = EffectiveDate/ExpiryDate)
    public string? ContractName { get; set; }

    public string? Description { get; set; }

    public DateTime? StartDate { get => _effectiveDate; set => EffectiveDate = value; }

    public DateTime? EndDate { get => _expiryDate; set => ExpiryDate = value; }
}
