using System.ComponentModel.DataAnnotations;

namespace QLHopDongHoSo.API.DTOs.Contracts;

// Danh mục tài liệu theo hồ sơ (bắt buộc / bổ sung sau) và tình trạng hiện tại
public class ContractChecklistResponse
{
    public int ContractId { get; set; }

    public string ContractCode { get; set; } = string.Empty;

    public List<ChecklistItem> Items { get; set; } = [];

    // Thiếu tài liệu bắt buộc => không được trình duyệt (ERR_DOC_01)
    public List<string> MissingMandatory { get; set; } = [];

    // Thiếu tài liệu bổ sung sau mà chưa có tờ trình được duyệt (ERR_DOC_02)
    public List<string> MissingDeferred { get; set; } = [];

    // Tờ trình đã duyệt nhưng quá thời hạn hoàn thiện: không được trình (ERR_DOC_02), phê duyệt vẫn được kèm cảnh báo
    public List<string> OverdueDeferred { get; set; } = [];

    // WARN_DOC_02: đang nợ chứng từ theo tờ trình đã duyệt
    public List<string> Warnings { get; set; } = [];

    public bool CanSubmit => CanApprove && OverdueDeferred.Count == 0;

    public bool CanApprove => MissingMandatory.Count == 0 && MissingDeferred.Count == 0;
}

public class ChecklistItem
{
    public string DocumentType { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    // MANDATORY | DEFERRED
    public string Requirement { get; set; } = string.Empty;

    // Số tài liệu loại này đã tải lên (không tính tài liệu bị từ chối xác minh)
    public int Uploaded { get; set; }

    public bool Present => Uploaded > 0;

    // Tờ trình gần nhất cho loại tài liệu này (nếu có)
    public DeferralResponse? Deferral { get; set; }
}

public class DeferralResponse
{
    public int DeferralId { get; set; }

    public int ContractId { get; set; }

    public string? ContractCode { get; set; }

    public string? ContractStatus { get; set; }

    public int BranchId { get; set; }

    public string? BranchName { get; set; }

    public string? CustomerName { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentLabel => DocumentRequirementLabels.Label(DocumentType);

    public DateTime DueDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int RequestedBy { get; set; }

    public string? RequestedByName { get; set; }

    public DateTime RequestedAt { get; set; }

    public int? DecidedBy { get; set; }

    public string? DecidedByName { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionNote { get; set; }

    public bool Overdue => Status == "APPROVED" && DueDate < DateTime.Today;
}

public class DeferralCreateRequest
{
    [Required(ErrorMessage = "Vui lòng chọn loại tài liệu xin nợ.")]
    [MaxLength(50)]
    public string DocumentType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập thời hạn hoàn thiện chứng từ.")]
    public DateTime? DueDate { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do xin nợ chứng từ.")]
    [MinLength(10, ErrorMessage = "Lý do xin nợ chứng từ tối thiểu 10 ký tự.")]
    [MaxLength(500, ErrorMessage = "Lý do tối đa 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

public class DeferralDecisionRequest
{
    public bool Approve { get; set; }

    // Cột 500 ký tự: chừa chỗ ghi chú "Hết hiệu lực..." khi tờ trình bị thay thế
    [MaxLength(400, ErrorMessage = "Ý kiến tối đa 400 ký tự.")]
    public string? Note { get; set; }
}

public class DeferralListQuery
{
    // PENDING | APPROVED | REJECTED; trống = tất cả
    public string? Status { get; set; }

    public int? BranchId { get; set; }

    public int? ContractId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

// Nhãn tiếng Việt của loại tài liệu (khớp DOC_TYPES ở giao diện)
public static class DocumentRequirementLabels
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["CCCD"] = "CCCD / Hộ chiếu",
        ["APPLICATION_FORM"] = "Đơn đề nghị",
        ["INCOME_PROOF"] = "Chứng minh thu nhập",
        ["BUSINESS_LICENSE"] = "Giấy phép kinh doanh",
        ["COLLATERAL"] = "Hồ sơ tài sản bảo đảm",
        ["CONTRACT_SIGNED"] = "Hợp đồng đã ký",
        ["ATTACHMENT"] = "Tài liệu khác"
    };

    public static string Label(string type) => Labels.GetValueOrDefault(type, type);
}
