using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.DTOs;
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
    private readonly IAuditLogger _audit;
    private readonly IWebHostEnvironment _environment;
    private readonly IMemoryCache _cache;

    public DocumentController(
        ApplicationDbContext context,
        IFileValidationService fileValidationService,
        IDocumentService documentService,
        IAuditLogger audit,
        IWebHostEnvironment environment,
        IMemoryCache cache)
    {
        _cache = cache;
        _context = context;
        _fileValidationService = fileValidationService;
        _documentService = documentService;
        _audit = audit;
        _environment = environment;
    }

    private string PhysicalPath(string filePath) =>
        Path.Combine(_environment.ContentRootPath, filePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

    // Nhóm loại tệp (màn Tài liệu): PDF, DOC (Word), XLS (Excel), IMG (ảnh) – cột tính sẵn DOCUMENTS.FileKind
    private static readonly string[] Kinds = ["PDF", "DOC", "XLS", "IMG"];

    // =====================================================
    // GET DOCUMENTS (lọc + phân trang)
    // =====================================================
    // GET: api/Document?applicationId=&q=&kind=PDF&page=1&pageSize=20
    // Dung lượng lấy từ cột FileSize (lưu lúc upload) thay vì đọc ổ đĩa cho từng dòng
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DocumentListQuery request)
    {
        var (page, pageSize) = PagedResult<DocumentResponse>.Normalize(request.Page, request.PageSize);

        var query = _context.Documents.AsNoTracking();
        if (request.ApplicationId is > 0)
        {
            query = query.Where(d => d.ApplicationId == request.ApplicationId);
        }

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var k = request.Q.Trim();
            // Ba truy vấn đơn giản ghép bằng UNION (tên tệp / mã hợp đồng / khách hàng) thay vì một điều kiện OR lớn
            var customerIds = await ContractService.MatchingCustomerIds(_context, k).ToListAsync();
            var key = SearchText.Key(k);
            var appIds = await _context.Contracts.AsNoTracking()
                .Where(c => EF.Functions.Collate(c.ContractCode.ToUpper(), SearchText.Binary).Contains(key))
                .Select(c => c.ApplicationId)
                .Take(1000)
                .ToListAsync();
            var byName = query.Where(d => EF.Functions.Collate(d.DocumentName.ToUpper(), SearchText.Binary).Contains(key));
            // Chỉ ghép các nhánh có kết quả (nhánh rỗng làm SQL chọn kế hoạch chậm)
            if (appIds.Count > 0)
            {
                byName = byName.Union(query.Where(d => appIds.Contains(d.ApplicationId)));
            }

            if (customerIds.Count > 0)
            {
                byName = byName.Union(query.Where(d => customerIds.Contains(d.Application!.CustomerId)));
            }

            query = byName;
        }

        // Số tài liệu theo loại tệp + tổng dung lượng (cùng bộ lọc tìm kiếm) – một câu GROUP BY,
        // cache tới khi có thao tác ghi
        var (counts, totalSize) = await _cache.GetOrCreateAsync($"document-stats:{request.ApplicationId}:{request.Q?.Trim()}", async () =>
        {
            var stats = await query
                .GroupBy(d => d.FileKind)
                .Select(g => new { g.Key, Count = g.Count(), Size = g.Sum(d => d.FileSize ?? 0) })
                .ToListAsync();
            var byKind = new Dictionary<string, int> { ["ALL"] = stats.Sum(s => s.Count) };
            foreach (var kind in Kinds)
            {
                byKind[kind] = stats.Where(s => s.Key == kind).Sum(s => s.Count);
            }

            return (byKind, stats.Sum(s => s.Size));
        });

        var kindKey = (request.Kind ?? "ALL").ToUpperInvariant();
        if (Kinds.Contains(kindKey))
        {
            query = query.Where(d => d.FileKind == kindKey);
        }
        else if (kindKey != "ALL")
        {
            return BadRequest(new { message = "Loại tệp chỉ nhận ALL, PDF, DOC, XLS, IMG." });
        }

        // Tổng của loại đang chọn lấy từ số đếm ở trên (đỡ một lần quét bảng)
        var total = counts[kindKey];
        var items = await query
            .OrderByDescending(d => d.UploadedAt)
            .ThenByDescending(d => d.DocumentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DocumentResponse
            {
                DocumentId = d.DocumentId,
                ApplicationId = d.ApplicationId,
                DocumentType = d.DocumentType,
                DocumentName = d.DocumentName,
                FilePath = d.FilePath,
                FileSize = d.FileSize,
                UploadedBy = d.UploadedBy,
                UploadedByUsername = d.Uploader != null ? d.Uploader.Username : null,
                UploadedByName = d.Uploader != null ? d.Uploader.FullName : null,
                UploadedAt = d.UploadedAt,
                VerificationStatus = d.VerificationStatus,
                VerifiedBy = d.VerifiedBy,
                VerifiedByUsername = d.Verifier != null ? d.Verifier.Username : null,
                VerifiedAt = d.VerifiedAt,
                LinkedContractId = d.Application!.Contracts.OrderBy(c => c.ContractId).Select(c => (int?)c.ContractId).FirstOrDefault(),
                ContractCode = d.Application.Contracts.OrderBy(c => c.ContractId).Select(c => c.ContractCode).FirstOrDefault(),
                CustomerName = d.Application.Customer!.FullName
            })
            .ToListAsync();

        return Ok(new DocumentPagedResult
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            Counts = counts,
            TotalSize = totalSize
        });
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
                FileSize = d.FileSize,
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
    [Authorize(Roles = AppRoles.Editors)]
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
        // Client cũ gửi ContractId thì lấy hồ sơ của hợp đồng đó
        if (applicationId <= 0 && request.ContractId.HasValue && request.ContractId.Value > 0)
        {
            var contract = await _context.Contracts.FindAsync(request.ContractId.Value);
            if (contract == null)
            {
                return NotFound(new { message = "Không tìm thấy hợp đồng." });
            }
            applicationId = contract.ApplicationId;
        }

        if (applicationId <= 0)
        {
            return BadRequest(new { message = "Vui lòng chỉ định hồ sơ (ApplicationId) để tải tài liệu lên." });
        }

        var application = await _context.Applications.FindAsync(applicationId);
        if (application == null)
        {
            return NotFound(new { message = "Không tìm thấy hồ sơ liên kết." });
        }

        try
        {
            var actor = await _context.GetActorAsync(userId);
            actor.EnsureCanWriteBranch(application.BranchId);

            var result = await _documentService.UploadAsync(
                applicationId,
                request.DocumentType,
                request.File,
                userId);
            // Nhật ký UPLOAD đã lưu cùng bản ghi tài liệu trong DocumentService

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.DocumentId },
                result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =====================================================
    // VERIFY DOCUMENT (VERIFIED / REJECTED)
    // =====================================================
    [HttpPatch("{id:int}/verify")]
    [Authorize(Roles = AppRoles.Approvers)]
    public async Task<IActionResult> Verify(int id, [FromBody] DocumentVerifyRequest request)
    {
        var status = request.Status.Trim().ToUpperInvariant();
        if (status != "VERIFIED" && status != "REJECTED")
        {
            return BadRequest(new { message = "Trạng thái chỉ nhận VERIFIED hoặc REJECTED." });
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Không xác định được người dùng." });
        }

        var document = await _context.Documents
            .Include(d => d.Application)
            .FirstOrDefaultAsync(d => d.DocumentId == id);
        if (document == null)
        {
            return NotFound(new { message = "Không tìm thấy tài liệu." });
        }

        try
        {
            var actor = await _context.GetActorAsync(userId);
            actor.EnsureCanWriteBranch(document.Application!.BranchId);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }

        // N09 – Maker ≠ Checker: người tải tài liệu lên / người tạo hồ sơ không tự xác minh
        var isMaker = document.UploadedBy == userId ||
            await _context.Contracts.AnyAsync(c => c.ApplicationId == document.ApplicationId && c.CreatedBy == userId);
        if (isMaker)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "ERR_SOD_01: Người tạo hồ sơ / người tải tài liệu lên không có quyền xác minh tài liệu này.",
                code = "ERR_SOD_01"
            });
        }

        // Khóa hợp đồng của hồ sơ (cùng thứ tự với phê duyệt): phê duyệt đang chạy thì chờ nó xong rồi mới đánh dấu,
        // và ngược lại phê duyệt chờ lệnh đánh dấu xong rồi mới kiểm tra lại tài liệu (T03)
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var contracts = await _context.LockByApplicationAsync(document.ApplicationId);

        // Hợp đồng đã hiệu lực / kết thúc: tài liệu đã được dùng để phê duyệt => không đánh dấu "không hợp lệ" nữa
        string[] decided = [ContractStatuses.Active, ContractStatuses.Expired, ContractStatuses.Terminated];
        if (status == "REJECTED" && contracts.Any(c => decided.Contains(c.Status)))
        {
            return Conflict(new { message = "Hợp đồng đã được phê duyệt / kết thúc, không đánh dấu tài liệu không hợp lệ được nữa." });
        }

        // Chỉ tài liệu đang "Chờ xác minh" mới được xác minh/từ chối; cập nhật có điều kiện trong một câu UPDATE
        // => nhiều người bấm cùng lúc thì đúng một người được ghi, người sau nhận 409 (trước đây kết quả ngẫu nhiên)
        var now = DateTime.Now;
        var updated = await _context.Documents
            .Where(d => d.DocumentId == id && d.VerificationStatus == "PENDING")
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.VerificationStatus, status)
                .SetProperty(d => d.VerifiedBy, userId)
                .SetProperty(d => d.VerifiedAt, now));
        if (updated == 0)
        {
            return Conflict(new { message = "Tài liệu đã được xác minh trước đó (có thể bởi người khác). Vui lòng tải lại trang." });
        }

        _audit.Add(userId, AuditActions.Update, "DOCUMENTS", document.DocumentName,
            status == "VERIFIED" ? "Xác minh tài liệu hợp lệ" : "Đánh dấu tài liệu không hợp lệ");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        DataVersion.Bump();

        return Ok(new { message = status == "VERIFIED" ? "Đã xác minh tài liệu." : "Đã từ chối tài liệu." });
    }

    // =====================================================
    // DELETE DOCUMENT
    // =====================================================
    // Chỉ xóa khi hồ sơ chưa có hợp đồng hiệu lực/kết thúc (Nháp, Chờ duyệt, Bị từ chối)
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Editors)]
    public async Task<IActionResult> Delete(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Không xác định được người dùng." });
        }

        var document = await _context.Documents
            .Include(d => d.Application)
            .FirstOrDefaultAsync(d => d.DocumentId == id);
        if (document == null)
        {
            return NotFound(new { message = "Không tìm thấy tài liệu." });
        }

        try
        {
            var actor = await _context.GetActorAsync(userId);
            actor.EnsureCanWriteBranch(document.Application!.BranchId);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }

        // Hồ sơ đã trình thì tài liệu đang được Checker xem xét => không xóa được (chỉ bổ sung thêm);
        // muốn thay tài liệu thì Checker trả về / từ chối trước.
        // Khóa hợp đồng trước khi kiểm tra trạng thái: lệnh trình duyệt chạy cùng lúc phải chờ (T02)
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var editable = new[] { ContractStatuses.Draft, ContractStatuses.Rejected };
        var contracts = await _context.LockByApplicationAsync(document.ApplicationId);
        if (!await _context.Documents.AnyAsync(d => d.DocumentId == id))
        {
            return NotFound(new { message = "Không tìm thấy tài liệu." });
        }
        if (contracts.Any(c => c.Status == ContractStatuses.PendingApproval))
        {
            return BadRequest(new { message = "Hồ sơ đang chờ phê duyệt, không được xóa tài liệu (có thể tải bổ sung)." });
        }
        if (contracts.Any(c => !editable.Contains(c.Status)))
        {
            return BadRequest(new { message = "Hồ sơ đã có hợp đồng hiệu lực, không được xóa tài liệu." });
        }

        var code = await _context.Contracts
            .Where(c => c.ApplicationId == document.ApplicationId)
            .Select(c => c.ContractCode)
            .FirstOrDefaultAsync() ?? document.Application!.ApplicationCode;

        // Xóa bản ghi trước rồi mới xóa file: lưu DB lỗi thì file vẫn còn, không có bản ghi trỏ tới file đã mất
        _context.Documents.Remove(document);
        _audit.Add(userId, AuditActions.Delete, "DOCUMENTS", code, $"Xóa tài liệu {document.DocumentName}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var path = PhysicalPath(document.FilePath);
        try
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch (IOException)
        {
            // File đang bị mở (đang tải xuống...) => để lại file rác, không làm hỏng thao tác xóa
        }

        return Ok(new { message = "Đã xóa tài liệu." });
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

            // Stream thẳng từ đĩa, hỗ trợ tải tiếp (Range) – RAM không tăng theo dung lượng file
            return PhysicalFile(
                result.Value.PhysicalPath,
                result.Value.ContentType,
                result.Value.FileName,
                enableRangeProcessing: true);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}