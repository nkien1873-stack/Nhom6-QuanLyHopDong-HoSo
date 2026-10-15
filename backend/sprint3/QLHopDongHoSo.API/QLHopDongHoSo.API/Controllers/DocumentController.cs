using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.DTOs.Documents;
using QLHopDongHoSo.API.Services;
using System.Security.Claims;

namespace QLHopDongHoSo.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IFileValidationService _fileValidationService;
    private readonly IDocumentService _documentService;

    public DocumentController(
        ApplicationDbContext context,
        IFileValidationService fileValidationService,
        IDocumentService documentService)
    {
        _context = context;
        _fileValidationService = fileValidationService;
        _documentService = documentService;
    }

    // =====================================================
    // GET ALL DOCUMENTS
    // =====================================================
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentResponse>>> GetAll()
    {
        var documents = await _context.Documents
            .Include(d => d.Uploader)
            .Include(d => d.Verifier)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentResponse
            {
                DocumentId = d.DocumentId,
                ApplicationId = d.ApplicationId,
                DocumentType = d.DocumentType,
                DocumentName = d.DocumentName,
                FilePath = d.FilePath,
                UploadedBy = d.UploadedBy,
                UploadedByUsername = d.Uploader != null ? d.Uploader.Username : null,
                UploadedAt = d.UploadedAt,
                VerificationStatus = d.VerificationStatus,
                VerifiedBy = d.VerifiedBy,
                VerifiedByUsername = d.Verifier != null ? d.Verifier.Username : null,
                VerifiedAt = d.VerifiedAt
            })
            .ToListAsync();

        return Ok(documents);
    }

    // =====================================================
    // GET DOCUMENT BY ID
    // =====================================================
    [HttpGet("{id:int}")]
    public async Task<ActionResult<DocumentResponse>> GetById(int id)
    {
        var document = await _context.Documents
            .Include(d => d.Uploader)
            .Include(d => d.Verifier)
            .Where(d => d.DocumentId == id)
            .Select(d => new DocumentResponse
            {
                DocumentId = d.DocumentId,
                ApplicationId = d.ApplicationId,
                DocumentType = d.DocumentType,
                DocumentName = d.DocumentName,
                FilePath = d.FilePath,
                UploadedBy = d.UploadedBy,
                UploadedByUsername = d.Uploader != null ? d.Uploader.Username : null,
                UploadedAt = d.UploadedAt,
                VerificationStatus = d.VerificationStatus,
                VerifiedBy = d.VerifiedBy,
                VerifiedByUsername = d.Verifier != null ? d.Verifier.Username : null,
                VerifiedAt = d.VerifiedAt
            })
            .FirstOrDefaultAsync();

        if (document == null)
        {
            return NotFound(new { message = "Không tìm thấy tài liệu." });
        }

        return Ok(document);
    }

    // =====================================================
    // VALIDATE FILE
    // =====================================================
    [HttpPost("validate")]
    [Consumes("multipart/form-data")]
    public IActionResult ValidateFile(IFormFile file)
    {
        var error = _fileValidationService.Validate(file);

        if (error != null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new
        {
            message = "File hợp lệ.",
            fileName = file.FileName,
            fileType = file.ContentType,
            fileSize = file.Length
        });
    }

    // =====================================================
    // UPLOAD FILE
    // =====================================================
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentResponse>> Upload([FromForm] DocumentUploadRequest request)
    {
        if (request.File == null)
        {
            return BadRequest(new { message = "Vui lòng chọn file." });
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Không xác định được người dùng." });
        }

        var applicationId = request.ApplicationId;
        // Fallback nếu client cũ gửi ContractId
        if (applicationId == 0 && request.ContractId.HasValue && request.ContractId.Value > 0)
        {
            var contract = await _context.Contracts.FindAsync(request.ContractId.Value);
            if (contract != null)
            {
                applicationId = contract.ApplicationId;
            }
        }

        if (applicationId == 0)
        {
            var defaultApp = await _context.Applications.FirstOrDefaultAsync();
            if (defaultApp == null)
            {
                return BadRequest(new { message = "Cần chỉ định ApplicationId để tải lên tài liệu." });
            }
            applicationId = defaultApp.ApplicationId;
        }

        try
        {
            var result = await _documentService.UploadAsync(
                applicationId,
                request.DocumentType,
                request.File,
                userId);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.DocumentId },
                result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =====================================================
    // DOWNLOAD FILE
    // =====================================================
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        try
        {
            var result = await _documentService.DownloadAsync(id);

            if (result == null)
            {
                return NotFound(new { message = "Không tìm thấy tài liệu." });
            }

            return File(
                result.Value.Content,
                result.Value.ContentType,
                result.Value.FileName);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}