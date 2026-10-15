namespace QLHopDongHoSo.API.DTOs.Documents;

public class DocumentResponse
{
    public int DocumentId { get; set; }

    public int ApplicationId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public int UploadedBy { get; set; }

    public string? UploadedByUsername { get; set; }

    public DateTime UploadedAt { get; set; }

    public string VerificationStatus { get; set; } = "PENDING";

    public int? VerifiedBy { get; set; }

    public string? VerifiedByUsername { get; set; }

    public DateTime? VerifiedAt { get; set; }

    // Compatibility properties for old frontend/services
    public string FileName => DocumentName;

    public int ContractId => ApplicationId;
}