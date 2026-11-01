using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLHopDongHoSo.API.DTOs.Contracts;
using QLHopDongHoSo.API.Services;
using System.Security.Claims;

namespace QLHopDongHoSo.API.Controllers;

// Tờ trình xin nợ chứng từ (tài liệu loại "Bổ sung sau"):
// Maker (Officer/Manager/Admin) lập tờ trình => cấp ngoại lệ (Director: Giám đốc CN / Trưởng phòng QLRR) duyệt hoặc từ chối.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentDeferralController : ControllerBase
{
    private readonly IDocumentComplianceService _compliance;

    public DocumentDeferralController(IDocumentComplianceService compliance)
    {
        _compliance = compliance;
    }

    // GET: api/DocumentDeferral?status=PENDING&branchId=1&contractId=&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DeferralListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Status) &&
            !DeferralStatuses.All.Contains(query.Status.Trim().ToUpperInvariant()))
        {
            return BadRequest(new { message = "status phải là PENDING, APPROVED hoặc REJECTED." });
        }

        return Ok(await _compliance.ListDeferralsAsync(query));
    }

    // POST: api/DocumentDeferral/contract/5 – lập tờ trình xin nợ chứng từ cho hồ sơ
    [HttpPost("contract/{contractId:int}")]
    [Authorize(Roles = AppRoles.Editors)]
    public async Task<IActionResult> Create(int contractId, [FromBody] DeferralCreateRequest request)
    {
        if (GetUserId() is not { } userId)
        {
            return Unauthorized(new { message = "Không xác định được người dùng đăng nhập." });
        }

        try
        {
            // Nhật ký ghi cùng transaction trong DocumentComplianceService
            var deferral = await _compliance.RequestDeferralAsync(contractId, request, userId);
            return StatusCode(StatusCodes.Status201Created, deferral);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
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
            return Conflict(new { message = ex.Message, code = ex.Code });
        }
    }

    // PUT: api/DocumentDeferral/3/decision { approve: true|false, note }
    [HttpPut("{id:int}/decision")]
    [Authorize(Roles = AppRoles.Director)]
    public async Task<IActionResult> Decide(int id, [FromBody] DeferralDecisionRequest request)
    {
        if (GetUserId() is not { } userId)
        {
            return Unauthorized(new { message = "Không xác định được người dùng đăng nhập." });
        }

        try
        {
            var deferral = await _compliance.DecideDeferralAsync(id, request, userId);
            if (deferral == null)
            {
                return NotFound(new { message = "Không tìm thấy tờ trình." });
            }

            return Ok(deferral);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
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
            return Conflict(new { message = ex.Message, code = ex.Code });
        }
    }

    private int? GetUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
