using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Configuration;
using QLHopDongHoSo.API.DTOs;
using QLHopDongHoSo.API.Services;

namespace QLHopDongHoSo.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAuditLogger _audit;
    private readonly Data.ApplicationDbContext _context;

    public AuthController(IAuthService authService, IAuditLogger audit, Data.ApplicationDbContext context)
    {
        _authService = authService;
        _audit = audit;
        _context = context;
    }

    // POST: api/auth/register – chỉ Admin (trước đây ai cũng tự đăng ký được rồi đọc toàn bộ dữ liệu).
    // Màn quản trị dùng POST /api/User; endpoint này giữ cho tương thích, tài khoản nhận vai trò Auth:DefaultRoleName.
    [HttpPost("register")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new
            {
                message = "Username, password và họ tên không được để trống."
            });
        }

        try
        {
            var result = await _authService.RegisterAsync(request);

            if (result == null)
            {
                return Conflict(new
                {
                    message = "Username đã tồn tại."
                });
            }

            return StatusCode(201, result);
        }
        catch (DbUpdateException)
        {
            // Phòng trường hợp hai yêu cầu đăng ký
            // cùng username được xử lý đồng thời.
            return Conflict(new
            {
                message = "Không thể tạo tài khoản. " +
                          "Username có thể đã tồn tại."
            });
        }
    }

    // POST: api/auth/login
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Username và password không được để trống."
            });
        }

        LoginResponse? result;
        try
        {
            result = await _authService.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }
        catch (LoginLockedException ex)
        {
            var lockedId = await FindUserIdAsync(request.Username);
            await _audit.LogAsync(lockedId, AuditActions.LoginFail, "SYSTEM", "Hệ thống", $"Tạm khóa do sai mật khẩu nhiều lần ({Short(request.Username)})");
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            var lockedId = await FindUserIdAsync(request.Username);
            await _audit.LogAsync(lockedId, AuditActions.LoginFail, "SYSTEM", "Hệ thống", $"Tài khoản bị khóa ({Short(request.Username)})");
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }

        if (result == null)
        {
            var userId = await FindUserIdAsync(request.Username);
            await _audit.LogAsync(userId, AuditActions.LoginFail, "SYSTEM", "Hệ thống", $"Sai thông tin đăng nhập ({Short(request.Username)})");
            return Unauthorized(new
            {
                message = "Username hoặc password không đúng."
            });
        }

        await _audit.LogAsync(result.UserId, AuditActions.Login, "SYSTEM", "Hệ thống", "Đăng nhập thành công");
        return Ok(result);
    }

    // POST: api/auth/logout – JWT không lưu phía server, chỉ ghi nhật ký
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromServices] Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;
        if (userId.HasValue)
        {
            // Thu hồi token: mọi token đã phát hành của tài khoản này hết hiệu lực ngay (đăng xuất mọi nơi)
            await UserStatusCache.RevokeTokensAsync(_context, cache, userId.Value);
        }

        await _audit.LogAsync(userId, AuditActions.Logout, "SYSTEM", "Hệ thống", "Đăng xuất");
        return Ok(new { message = "Đã đăng xuất." });
    }

    // GET: api/auth/demo-accounts – tài khoản demo hiện trên trang đăng nhập, CHỈ ở môi trường Development
    // (bản Production trả danh sách rỗng, không lộ tài khoản/mật khẩu chung)
    [HttpGet("demo-accounts")]
    public IActionResult DemoAccounts([FromServices] IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
        {
            return Ok(new { password = (string?)null, accounts = Array.Empty<string>() });
        }

        return Ok(new
        {
            password = Data.DbInitializer.DemoPassword,
            accounts = new[] { "admin", "manager_hn", "officer_hn01", "director_hn", "auditor" }
        });
    }

    // Username người dùng gõ vào có thể rất dài => cắt bớt trước khi ghi nhật ký
    private static string Short(string? username)
    {
        var name = (username ?? string.Empty).Trim();
        return name.Length > 50 ? name[..50] + "…" : name;
    }

    private async Task<int?> FindUserIdAsync(string username)
    {
        var name = (username ?? string.Empty).Trim();
        return await _context.Users
            .Where(u => u.Username == name)
            .Select(u => (int?)u.UserId)
            .FirstOrDefaultAsync();
    }
}