using Microsoft.AspNetCore.Http;
using QLHopDongHoSo.API.DTOs.Documents;

namespace QLHopDongHoSo.API.Services;

public interface IDocumentService
{
    Task<DocumentResponse> UploadAsync(
        int applicationId,
        string documentType,
        IFormFile file,
        int userId);

    Task<(byte[] Content, string ContentType, string FileName)?>
        DownloadAsync(int documentId);
}