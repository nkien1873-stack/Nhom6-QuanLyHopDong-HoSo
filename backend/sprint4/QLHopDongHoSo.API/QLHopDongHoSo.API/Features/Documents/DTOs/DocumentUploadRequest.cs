using Microsoft.AspNetCore.Http;

namespace QLHopDongHoSo.API.DTOs.Documents;

public class DocumentUploadRequest
{
    public int ApplicationId { get; set; }

    // Compatibility fallback for old clients
    public int? ContractId { get; set; }

    public string DocumentType { get; set; } = "ATTACHMENT";

    public IFormFile? File { get; set; }
}