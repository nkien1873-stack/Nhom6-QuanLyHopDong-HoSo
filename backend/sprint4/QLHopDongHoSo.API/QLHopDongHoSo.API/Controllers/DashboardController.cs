using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLHopDongHoSo.API.DTOs.Dashboard;
using QLHopDongHoSo.API.Services;

namespace QLHopDongHoSo.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    // GET: api/Dashboard/summary?branchId=1&expiringDays=30
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(
        [FromQuery] int? branchId,
        [FromQuery] int expiringDays = 30)
    {
        if (expiringDays < 1 || expiringDays > 365)
        {
            return BadRequest(new { message = "expiringDays phải từ 1 đến 365." });
        }

        var result = await _dashboardService.GetSummaryAsync(branchId, expiringDays);
        return Ok(result);
    }

    // GET: api/Dashboard/overview – số liệu màn Tổng quan (đếm sẵn ở server)
    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverview>> GetOverview() =>
        GetUserId() is { } userId ? Ok(await _dashboardService.GetOverviewAsync(userId)) : Unauthorized();

    // GET: api/Dashboard/approvals – thẻ thống kê màn Phê duyệt
    [HttpGet("approvals")]
    [Authorize(Roles = AppRoles.Approvers)]
    public async Task<ActionResult<ApprovalStats>> GetApprovals() =>
        GetUserId() is { } userId ? Ok(await _dashboardService.GetApprovalStatsAsync(userId)) : Unauthorized();

    // GET: api/Dashboard/notifications – số hồ sơ chờ duyệt + 5 hồ sơ mới nhất
    [HttpGet("notifications")]
    public async Task<ActionResult<NotificationSummary>> GetNotifications() =>
        GetUserId() is { } userId ? Ok(await _dashboardService.GetNotificationsAsync(userId)) : Unauthorized();

    // GET: api/Dashboard/report?branchId=1&days=90 (days = 0: toàn bộ)
    [HttpGet("report")]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Manager + "," + AppRoles.Auditor)]
    public async Task<ActionResult<ReportResponse>> GetReport([FromQuery] int? branchId, [FromQuery] int days = 0)
    {
        if (days is < 0 or > 3650)
        {
            return BadRequest(new { message = "days phải từ 0 đến 3650." });
        }

        return Ok(await _dashboardService.GetReportAsync(branchId, days));
    }

    private int? GetUserId() =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
