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

    // Chi tiết khoản vay/hợp đồng (bổ sung cho giao diện mới)
    public int? TermMonths { get; set; }

    public decimal? InterestRate { get; set; }

    public string? Purpose { get; set; }

    public string? Collateral { get; set; }

    public string Status { get; set; } = "DRAFT";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Lần thay đổi gần nhất (sửa thông tin hoặc đổi trạng thái) – dùng để sắp xếp danh sách
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // rowversion: chặn hai người duyệt/sửa cùng lúc ghi đè nhau (lỗi đồng thời trả 409)
    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public Application? Application { get; set; }

    public ContractType? ContractType { get; set; }

    public Branch? Branch { get; set; }

    public User? Creator { get; set; }

    public ICollection<ContractStatusHistory> StatusHistories { get; set; } = new List<ContractStatusHistory>();
}