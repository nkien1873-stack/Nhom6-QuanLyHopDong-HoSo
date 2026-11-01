using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Data;

namespace QLHopDongHoSo.API.Controllers;

// Danh mục dùng cho dropdown ở FE
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LookupController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public LookupController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/Lookup/contract-types
    [HttpGet("contract-types")]
    public async Task<IActionResult> GetContractTypes()
    {
        var items = await _context.ContractTypes
            .OrderBy(t => t.TypeName)
            .Select(t => new { t.ContractTypeId, t.TypeName, t.Description })
            .ToListAsync();

        return Ok(items);
    }

    // GET: api/Lookup/branches
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches()
    {
        var items = await _context.Branches
            .Where(b => b.Status == "ACTIVE")
            .OrderBy(b => b.BranchCode)
            .Select(b => new { b.BranchId, b.BranchCode, b.BranchName })
            .ToListAsync();

        return Ok(items);
    }

    // GET: api/Lookup/applications?q=&branchId=&take=50 – chọn hồ sơ gốc (dropdown)
    // Trước đây trả TOÀN BỘ hồ sơ (100.000 dòng = 19 MB); nay lọc + tối đa 200 dòng.
    [HttpGet("applications")]
    public async Task<IActionResult> GetApplications(
        [FromQuery, System.ComponentModel.DataAnnotations.MaxLength(100)] string? q,
        [FromQuery] int? branchId,
        [FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        var query = _context.Applications.AsNoTracking();
        if (branchId is > 0)
        {
            query = query.Where(a => a.BranchId == branchId);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var k = q.Trim();
            query = query.Where(a => a.ApplicationCode.Contains(k) || a.Customer!.FullName.Contains(k));
        }

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .Select(a => new
            {
                a.ApplicationId,
                a.ApplicationCode,
                a.ApplicationType,
                a.BranchId,
                a.Status,
                CustomerName = a.Customer != null ? a.Customer.FullName : null
            })
            .ToListAsync();

        return Ok(items);
    }

    // GET: api/Lookup/users – danh bạ tối thiểu (tên, vai trò, chi nhánh) để hiện "chuyên viên phụ trách",
    // "người phê duyệt"... cho mọi người dùng đã đăng nhập. Không có email/điện thoại.
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .OrderBy(u => u.UserId)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.FullName,
                RoleName = u.Role != null ? u.Role.RoleName : null,
                u.BranchId,
                BranchName = u.Branch != null ? u.Branch.BranchName : null,
                u.Status
            })
            .ToListAsync();

        return Ok(users);
    }

    // GET: api/Lookup/roles
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var items = await _context.Roles
            .OrderBy(r => r.RoleId)
            .Select(r => new { r.RoleId, r.RoleName, r.Description })
            .ToListAsync();

        return Ok(items);
    }
}
