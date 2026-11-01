using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLHopDongHoSo.API.Services;
using System.Security.Claims;

namespace QLHopDongHoSo.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RbacTestController : ControllerBase
{
    // ==========================================
    // 1. Chỉ cần đăng nhập
    // ==========================================

    [Authorize]
    [HttpGet("authenticated")]
    public IActionResult Authenticated()
    {
        return Ok(new
        {
            message = "Bạn đã đăng nhập.",
            username = User.Identity?.Name,
            role = User.FindFirst(ClaimTypes.Role)?.Value
        });
    }

    // ==========================================
    // 2. Chỉ Admin
    // ==========================================

    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet("admin")]
    public IActionResult AdminOnly()
    {
        return Ok(new
        {
            message = "Bạn có quyền Admin.",
            username = User.Identity?.Name,
            role = User.FindFirst(ClaimTypes.Role)?.Value
        });
    }

    // ==========================================
    // 3. Admin hoặc Quản lý
    // ==========================================

    [Authorize(Roles = AppRoles.Approvers)]
    [HttpGet("manager")]
    public IActionResult ManagerArea()
    {
        return Ok(new
        {
            message = "Bạn có quyền Quản lý.",
            username = User.Identity?.Name,
            role = User.FindFirst(ClaimTypes.Role)?.Value
        });
    }

    // ==========================================
    // 4. Admin, Quản lý hoặc Nhân viên
    // ==========================================

    [Authorize(Roles = AppRoles.Editors)]
    [HttpGet("employee")]
    public IActionResult EmployeeArea()
    {
        return Ok(new
        {
            message = "Bạn có quyền truy cập khu vực Nhân viên.",
            username = User.Identity?.Name,
            role = User.FindFirst(ClaimTypes.Role)?.Value
        });
    }
}