namespace QLHopDongHoSo.API.Models;

// Tờ trình xin nợ chứng từ: Maker xin trình duyệt hồ sơ khi còn thiếu tài liệu loại "Bổ sung sau",
// cam kết bổ sung trước DueDate. Chỉ cấp ngoại lệ (Giám đốc CN / Trưởng phòng QLRR) được duyệt.
public class DocumentDeferral
{
    public int DeferralId { get; set; }

    public int ContractId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    // Thời hạn hoàn thiện chứng từ
    public DateTime DueDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    // PENDING | APPROVED | REJECTED
    public string Status { get; set; } = "PENDING";

    public int RequestedBy { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.Now;

    public int? DecidedBy { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionNote { get; set; }

    // Navigation properties
    public Contract? Contract { get; set; }

    public User? Requester { get; set; }

    public User? Decider { get; set; }
}
