using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QLHopDongHoSo.API.Configuration;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.DTOs.Documents;
using QLHopDongHoSo.API.Models;

namespace QLHopDongHoSo.API.Services;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileValidationService _fileValidationService;
    private readonly FileUploadOptions _options;
    private readonly IWebHostEnvironment _environment;

    public DocumentService(
        ApplicationDbContext context,
        IFileValidationService fileValidationService,
        IOptions<FileUploadOptions> options,
        IWebHostEnvironment environment)
    {
        _context = context;
        _fileValidationService = fileValidationService;
        _options = options.Value;
        _environment = environment;
    }

    public async Task<DocumentResponse> UploadAsync(
        int applicationId,
        string documentType,
        IFormFile file,
        int userId)
    {
        // 1. Kiểm tra hồ sơ tồn tại
        var applicationExists = await _context.Applications
            .AnyAsync(a => a.ApplicationId == applicationId);

        if (!applicationExists)
        {
            throw new KeyNotFoundException("Không tìm thấy hồ sơ liên kết.");
        }

        // 2. Validate file
        var validationError = _fileValidationService.Validate(file);
        if (validationError != null)
        {
            throw new InvalidOperationException(validationError);
        }

        // 3. Tạo thư mục lưu file
        var storagePath = Path.Combine(
            _environment.ContentRootPath,
            _options.StoragePath);

        Directory.CreateDirectory(storagePath);

        // 4. Lấy extension
        var extension = Path.GetExtension(file.FileName);

        // 5. Tạo tên file lưu duy nhất
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var physicalFilePath = Path.Combine(storagePath, storedFileName);

        // 6. Lưu file vật lý
        await using (var stream = new FileStream(physicalFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 7. Đường dẫn tương đối lưu DB
        var relativeFilePath = $"{_options.StoragePath}/{storedFileName}".Replace("\\", "/");

        // 8. Tạo metadata tài liệu
        var document = new Document
        {
            ApplicationId = applicationId,
            DocumentType = string.IsNullOrWhiteSpace(documentType) ? "ATTACHMENT" : documentType,
            DocumentName = Path.GetFileName(file.FileName),
            FilePath = relativeFilePath,
            UploadedBy = userId,
            UploadedAt = DateTime.UtcNow,
            VerificationStatus = "PENDING"
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // 9. Lấy username người upload
        var username = await _context.Users
            .Where(u => u.UserId == userId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync();

        return new DocumentResponse
        {
            DocumentId = document.DocumentId,
            ApplicationId = document.ApplicationId,
            DocumentType = document.DocumentType,
            DocumentName = document.DocumentName,
            FilePath = document.FilePath,
            UploadedBy = document.UploadedBy,
            UploadedByUsername = username,
            UploadedAt = document.UploadedAt,
            VerificationStatus = document.VerificationStatus
        };
    }

    public async Task<(byte[] Content, string ContentType, string FileName)?>
        DownloadAsync(int documentId)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null)
        {
            return null;
        }

        var physicalPath = Path.Combine(
            _environment.ContentRootPath,
            document.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (!File.Exists(physicalPath))
        {
            throw new FileNotFoundException("File không tồn tại trên hệ thống lưu trữ.");
        }

        var content = await File.ReadAllBytesAsync(physicalPath);

        return (
            content,
            "application/octet-stream",
            document.DocumentName
        );
    }
}