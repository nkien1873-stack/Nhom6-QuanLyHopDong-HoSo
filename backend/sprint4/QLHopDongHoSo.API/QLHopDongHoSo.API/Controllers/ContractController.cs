using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLHopDongHoSo.API.DTOs.Contracts;
using QLHopDongHoSo.API.Services;
using System.Security.Claims;

namespace QLHopDongHoSo.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContractController : ControllerBase
{
    private readonly IContractService _contractService;
    private readonly IWebHostEnvironment _environment;
    private readonly IDocumentComplianceService _compliance;

    public ContractController(
        IContractService contractService,
        IWebHostEnvironment environment,
        IDocumentComplianceService compliance)
    {
        _contractService = contractService;
        _environment = environment;
        _compliance = compliance;
    }

    // GET: api/Contract?q=&tab=PENDING&typeId=&branchId=&customerId=&mine=true&sort=updated&page=1&pageSize=20
    // Trả 1 trang + tổng số + số đếm theo tab (trước đây trả toàn bộ bảng => chậm khi dữ liệu lớn)
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ContractListQuery query)
    {
        if (GetUserId() is not { } userId)
        {
            return Unauthorized(new { message = "Không xác định được người dùng đăng nhập." });
        }

        return Ok(await _contractService.ListAsync(query, userId));
    }

    // GET: api/Contract/export?... – cùng bộ lọc, không phân trang (tối đa 10.000 dòng) để xuất Excel
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] ContractListQuery query)
    {
        if (GetUserId() is not { } userId)
        {
            return Unauthorized(new { message = "Không xác định được người dùng đăng nhập." });
        }

        return Ok(await _contractService.ExportAsync(query, userId));
    }

    // GET: api/Contract/decisions?mine=false&page=1&pageSize=20 – các lần phê duyệt/từ chối, mới nhất trước
    [HttpGet("decisions")]
    public async Task<IActionResult> GetDecisions([FromQuery] bool mine, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (GetUserId() is not { } userId)
        {
            return Unauthorized(new { message = "Không xác định được người dùng đăng nhập." });
        }

        return Ok(await _contractService.GetDecisionsAsync(mine, userId, page, pageSize));
    }

    // GET: api/Contract/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var contract = await _contractService.GetByIdAsync(id);

        if (contract == null)
        {
            return NotFound(new { message = "Không tìm thấy hợp đồng." });
        }

        return Ok(contract);
    }

    // POST: api/Contract
    [HttpPost]
    [Authorize(Roles = AppRoles.Editors)]
    public async Task<IActionResult> Create([FromBody] ContractCreateRequest request)
    {
        var startDate = request.EffectiveDate ?? request.StartDate;
        var endDate = request.ExpiryDate ?? request.EndDate;

        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            return BadRequest(new { message = "Ngày bắt đầu/hiệu lực không được lớn hơn ngày kết thúc/hết hạn." });
        }

        if (GetUserId() is not { } userId)
        {
            return Unauthorized(new { message = "Không xác định được người dùng đăng nhập." });
        }

        try
        {
            // Nhật ký CREATE được ghi cùng transaction trong ContractService
            var contract = await _contractService.CreateAsync(request, userId);
            return CreatedAtAction(nameof(GetById), new { id = contract.ContractId }, contract);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<ContractSearchResponse>> Search([FromQuery] ContractSearchRequest request)
    {
        if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate)
        {
            return BadRequest(new { message = "StartDate không được lớn hơn EndDate." });
        }

        var result = await _contractService.SearchAsync(request);
        return Ok(result);
    }

    // PUT: api/Contract/1
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Editors)]
    public async Task<IActionResult> Update(int id, [FromBody] ContractUpdateRequest request)
    {
        var startDate = request.EffectiveDate ?? request.StartDate;
        var endDate = request.ExpiryDate ?? request.EndDate;

        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            return BadRequest(new { message = "Ngày bắt đầu/hiệu lực không được lớn hơn ngày kết thúc/hết hạn." });
        }

        if (GetUserId() is not { } userId)
        {
            return Unauthorized(new { message = "Không xác định được người dùng đăng nhập." });
        }

        try
        {
            var contract = await _contractService.UpdateAsync(id, request, userId);
            if (contract == null)
            {
                return NotFound(new { message = "Không tìm thấy hợp đồng." });
            }

            return Ok(contract);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // PATCH: api/Contract/1/status
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = AppRoles.Editors)]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ContractStatusChangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { message = "Trạng thái mới không được để trống." });
        }

        if (GetUserId() is not { } userId)
        {
            return Unauthorized(new { message = "Không xác định được người dùng đăng nhập." });
        }

        try
        {
            var contract = await _contractService.ChangeStatusAsync(id, request, userId);
            if (contract == null)
            {
                return NotFound(new { message = "Không tìm thấy hợp đồng." });
            }

            // Nhật ký SUBMIT/APPROVE/REJECT đã ghi cùng transaction trong ContractService

            // WARN_DOC_02: trình/duyệt khi đang nợ chứng từ theo tờ trình đã duyệt
            if (contract.Status is ContractStatuses.PendingApproval or ContractStatuses.Active)
            {
                contract.Warnings = (await _compliance.GetChecklistAsync(id))?.Warnings ?? [];
            }

            return Ok(contract);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (BusinessRuleException ex) when (ex.IsForbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message, code = ex.Code });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message, code = ex.Code });
        }
    }

    // GET: api/Contract/1/checklist – danh mục tài liệu bắt buộc / bổ sung sau + tờ trình nợ chứng từ + cảnh báo
    [HttpGet("{id:int}/checklist")]
    public async Task<IActionResult> GetChecklist(int id)
    {
        var checklist = await _compliance.GetChecklistAsync(id);
        return checklist == null ? NotFound(new { message = "Không tìm thấy hợp đồng." }) : Ok(checklist);
    }

    // GET: api/Contract/1/history
    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id)
    {
        var history = await _contractService.GetHistoryAsync(id);

        // Hợp đồng nào cũng có ít nhất dòng "khởi tạo" => rỗng thường là hợp đồng không tồn tại => 404
        if (history.Count == 0 && await _contractService.GetByIdAsync(id) == null)
        {
            return NotFound(new { message = "Không tìm thấy hợp đồng." });
        }

        return Ok(history);
    }

    // GET: api/Contract/statuses
    [HttpGet("statuses")]
    public IActionResult GetStatuses()
    {
        return Ok(ContractStatuses.All);
    }

    // DELETE: api/Contract/1 – Admin xóa mọi hợp đồng; người khác chỉ xóa bản nháp của chi nhánh mình
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Editors)]
    public async Task<IActionResult> Delete(int id)
    {
        if (GetUserId() is not { } userId)
        {
            return Unauthorized(new { message = "Không xác định được người dùng đăng nhập." });
        }

        try
        {
            var code = await _contractService.DeleteAsync(id, userId, _environment.ContentRootPath);
            if (code == null)
            {
                return NotFound(new { message = "Không tìm thấy hợp đồng." });
            }

            return Ok(new { message = "Xóa hợp đồng thành công." });
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

    private int? GetUserId() =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
