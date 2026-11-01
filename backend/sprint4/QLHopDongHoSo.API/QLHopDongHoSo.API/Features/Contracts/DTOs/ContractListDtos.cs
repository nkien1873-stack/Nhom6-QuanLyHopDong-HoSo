using System.ComponentModel.DataAnnotations;

namespace QLHopDongHoSo.API.DTOs.Contracts;

// GET /api/Contract – lọc + phân trang phía server
public class ContractListQuery
{
    // Tìm theo mã hợp đồng, tên khách hàng, CCCD/MST
    [MinLength(2, ErrorMessage = "Từ khóa tìm kiếm cần ít nhất 2 ký tự.")]
    [MaxLength(100, ErrorMessage = "Từ khóa tìm kiếm tối đa 100 ký tự.")]
    public string? Q { get; set; }

    // ALL, PENDING, ACTIVE (còn hiệu lực, gồm sắp hết hạn), EXPIRING (≤ 30 ngày), EXPIRED (gồm quá hạn),
    // DRAFT, REJECTED, TERMINATED, CANCELLED
    [MaxLength(30)]
    public string? Tab { get; set; }

    public int? TypeId { get; set; }

    public int? BranchId { get; set; }

    public int? CustomerId { get; set; }

    // true => chỉ hồ sơ do tôi tạo
    public bool Mine { get; set; }

    // true => bỏ hồ sơ do tôi tạo (hàng đợi phê duyệt: Maker không tự duyệt – ERR_SOD_01)
    public bool NotMine { get; set; }

    // updated (mặc định) | created | amount | oldest (chờ lâu nhất trước)
    [MaxLength(20)]
    public string? Sort { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

// Một dòng danh sách: đủ để hiển thị bảng, không kèm lịch sử/tài liệu
public class ContractListItem
{
    public int ContractId { get; set; }

    public string ContractCode { get; set; } = string.Empty;

    public int ApplicationId { get; set; }

    public int? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    // DN | CN
    public string? CustomerKind { get; set; }

    public string? CustomerIdNo { get; set; }

    public int ContractTypeId { get; set; }

    public string? ContractTypeName { get; set; }

    public int BranchId { get; set; }

    public string? BranchName { get; set; }

    public int CreatedBy { get; set; }

    public string? CreatedByName { get; set; }

    public decimal? ContractValue { get; set; }

    public int? TermMonths { get; set; }

    public DateTime? SignedDate { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int DocumentCount { get; set; }
}

// Quyết định phê duyệt / từ chối (màn Phê duyệt – tab "Đã xử lý")
public class ContractDecisionItem
{
    public int HistoryId { get; set; }

    public int ContractId { get; set; }

    public string ContractCode { get; set; } = string.Empty;

    public string? CustomerName { get; set; }

    public decimal? ContractValue { get; set; }

    public string NewStatus { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public int ChangedBy { get; set; }

    public string? ChangedByName { get; set; }

    public DateTime ChangedAt { get; set; }
}
