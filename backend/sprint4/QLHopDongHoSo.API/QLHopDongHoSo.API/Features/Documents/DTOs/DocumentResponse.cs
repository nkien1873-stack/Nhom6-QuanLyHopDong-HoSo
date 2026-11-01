namespace QLHopDongHoSo.API.DTOs.Documents;

public class DocumentResponse
{
    public int DocumentId { get; set; }

    public int ApplicationId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    // Dung lượng file (byte) lưu trong DB lúc upload
    public long? FileSize { get; set; }

    public int UploadedBy { get; set; }

    public string? UploadedByUsername { get; set; }

    public string? UploadedByName { get; set; }

    // Hợp đồng đầu tiên của hồ sơ chứa tài liệu (để màn Tài liệu dẫn link) + tên khách hàng
    public int? LinkedContractId { get; set; }

    public string? ContractCode { get; set; }

    public string? CustomerName { get; set; }

    public DateTime UploadedAt { get; set; }

    public string VerificationStatus { get; set; } = "PENDING";

    public int? VerifiedBy { get; set; }

    public string? VerifiedByUsername { get; set; }

    public DateTime? VerifiedAt { get; set; }

    // Compatibility properties for old frontend/services
    public string FileName => DocumentName;

    public int ContractId => ApplicationId;
}