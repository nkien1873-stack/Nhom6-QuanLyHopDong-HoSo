using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
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
    private readonly IAuditLogger _audit;

    public DocumentService(
        ApplicationDbContext context,
        IFileValidationService fileValidationService,
        IOptions<FileUploadOptions> options,
        IWebHostEnvironment environment,
        IAuditLogger audit)
    {
        _context = context;
        _audit = audit;
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

        // Hồ sơ đã kết thúc (thanh lý / hết hạn / hủy) không nhận thêm tài liệu.
        // Hồ sơ đang hiệu lực vẫn nhận (vd bản hợp đồng đã ký).
        string[] closed = [ContractStatuses.Terminated, ContractStatuses.Expired, ContractStatuses.Cancelled];
        if (await _context.Contracts.AnyAsync(c => c.ApplicationId == applicationId && closed.Contains(c.Status)))
        {
            throw new BusinessRuleException("Hồ sơ đã thanh lý, hết hạn hoặc đã hủy nên không tải thêm tài liệu được.");
        }

        // 2. Validate file
        var validationError = _fileValidationService.Validate(file);
        if (validationError != null)
        {
            throw new BusinessRuleException(validationError);
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

        // 6. Lưu file vật lý. Ghi lỗi (đầy đĩa, mất quyền ghi...) => xóa phần đã ghi dở, báo 503 qua ApiExceptionHandler (T06)
        try
        {
            await using var stream = new FileStream(physicalFilePath, FileMode.CreateNew);
            await file.CopyToAsync(stream);
        }
        catch (IOException)
        {
            TryDelete(physicalFilePath);
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            // Thư mục lưu không có quyền ghi: lỗi máy chủ (503), không phải lỗi phân quyền người dùng (403)
            TryDelete(physicalFilePath);
            throw new IOException("Không ghi được tệp vào thư mục lưu trữ.", ex);
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
            FileSize = file.Length,
            UploadedBy = userId,
            UploadedAt = DateTime.Now,
            VerificationStatus = "PENDING"
        };

        _context.Documents.Add(document);

        // Nhật ký UPLOAD lưu cùng bản ghi tài liệu
        var code = await _context.Contracts
            .Where(c => c.ApplicationId == applicationId)
            .Select(c => c.ContractCode)
            .FirstOrDefaultAsync() ?? $"HS #{applicationId}";
        _audit.Add(userId, AuditActions.Upload, "DOCUMENTS", code, $"Tải lên {document.DocumentName}");

        try
        {
            // Ghi file xong mới khóa hợp đồng (không giữ khóa trong lúc ghi đĩa) rồi kiểm tra lại trạng thái:
            // thanh lý / hủy / xóa hồ sơ chạy cùng lúc thì không lọt tài liệu vào hồ sơ đã kết thúc
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var locked = await _context.LockByApplicationAsync(applicationId);
            if (locked.Any(c => closed.Contains(c.Status)))
            {
                throw new BusinessRuleException("Hồ sơ đã thanh lý, hết hạn hoặc đã hủy nên không tải thêm tài liệu được.");
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            // Lưu DB lỗi => xóa file vừa ghi để không thành file rác trên đĩa;
            // bỏ bản ghi chưa lưu khỏi DbContext để lần lưu khác trong request không ghi lại
            TryDelete(physicalFilePath);
            _context.ChangeTracker.Clear();

            // Hồ sơ vừa bị người khác xóa trong lúc upload (vi phạm khóa ngoại) => 404 thay vì 500
            if (ex is DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 547 } })
            {
                throw new KeyNotFoundException("Hồ sơ không còn tồn tại (có thể vừa bị xóa).");
            }

            throw;
        }

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
            FileSize = document.FileSize,
            UploadedBy = document.UploadedBy,
            UploadedByUsername = username,
            UploadedAt = document.UploadedAt,
            VerificationStatus = document.VerificationStatus
        };
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // file đang bị khóa: để lại, không che lỗi gốc
        }
    }

    // Trả đường dẫn file để controller stream thẳng từ đĩa (PhysicalFile): không nạp cả file vào RAM
    // (trước đây 400 lượt tải file 10 MB cùng lúc => API chiếm 4,4 GB RAM)
    public async Task<(string PhysicalPath, string ContentType, string FileName)?>
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

        // Trả đúng loại file (pdf, ảnh...) để trình duyệt xem trước được
        if (!new FileExtensionContentTypeProvider().TryGetContentType(document.DocumentName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return (
            physicalPath,
            contentType,
            document.DocumentName
        );
    }
}