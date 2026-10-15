namespace QLHopDongHoSo.API.Models;

public class Document
{
    public int DocumentId { get; set; }

    public int ApplicationId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public int UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public string VerificationStatus { get; set; } = "PENDING";

    public int? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    // Helper compatibility property
    public string FileName
    {
        get => DocumentName;
        set => DocumentName = value;
    }

    // Navigation properties
    public Application? Application { get; set; }

    public User? Uploader { get; set; }

    public User? Verifier { get; set; }
}