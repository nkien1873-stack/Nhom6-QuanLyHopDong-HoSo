using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QLHopDongHoSo.API.Configuration;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.DTOs.Users;
using QLHopDongHoSo.API.Models;
using QLHopDongHoSo.API.Services;
using System.Security.Claims;

namespace QLHopDongHoSo.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class UserController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogger _audit;
    private readonly IMemoryCache _cache;

    public UserController(ApplicationDbContext context, IAuditLogger audit, IMemoryCache cache)
    {
        _context = context;
        _audit = audit;
        _cache = cache;
    }

    // GET: api/User
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAll()
    {
        var users = await _context.Users
            .OrderBy(u => u.UserId)
            .Select(u => new UserResponse
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                RoleId = u.RoleId,
                RoleName = u.Role != null ? u.Role.RoleName : null,
                BranchId = u.BranchId,
                BranchName = u.Branch != null ? u.Branch.BranchName : null,
                Status = u.Status,
                CreatedAt = u.CreatedAt,
                // Lần đăng nhập gần nhất lấy từ nhật ký hệ thống
                LastLoginAt = _context.AuditLogs
                    .Where(l => l.UserId == u.UserId && l.Action == AuditActions.Login)
                    .Max(l => (DateTime?)l.CreatedAt)
            })
            .ToListAsync();

        return Ok(users);
    }

    // POST: api/User
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserCreateRequest request)
    {
        var username = request.Username.Trim();

        if (await _context.Users.AnyAsync(u => u.Username == username))
        {
            return Conflict(new { message = "Username đã tồn tại." });
        }

        var error = await ValidateRoleAndBranchAsync(request.RoleId, request.BranchId);
        if (error != null)
        {
            return BadRequest(new { message = error });
        }

        var user = new User
        {
            Username = username,
            PasswordHash = PasswordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            RoleId = request.RoleId,
            BranchId = request.BranchId,
            Status = "ACTIVE",
            CreatedAt = DateTime.Now
        };

        _context.Users.Add(user);
        _audit.Add(GetCurrentUserId(), AuditActions.Create, "USERS", user.Username, "Tạo tài khoản");
        await _context.SaveChangesAsync();

        return StatusCode(201, new { user.UserId, message = "Tạo tài khoản thành công." });
    }

    // PUT: api/User/1
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UserUpdateRequest request)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản." });
        }

        var error = await ValidateRoleAndBranchAsync(request.RoleId, request.BranchId);
        if (error != null)
        {
            return BadRequest(new { message = error });
        }

        // Không cho Admin tự hạ quyền của chính mình
        if (id == GetCurrentUserId() && request.RoleId != user.RoleId)
        {
            return BadRequest(new { message = "Không thể tự đổi vai trò của chính mình." });
        }

        var roleChanged = user.RoleId != request.RoleId;
        var branchChanged = user.BranchId != request.BranchId;

        user.FullName = request.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.RoleId = request.RoleId;
        user.BranchId = request.BranchId;

        // Vai trò nằm trong token => đổi vai trò/chi nhánh thì thu hồi token cũ (phải đăng nhập lại với quyền mới).
        // Tăng TokenVersion trong CÙNG lần lưu với thay đổi quyền + nhật ký: không có lúc quyền đã đổi mà token cũ còn dùng được
        if (roleChanged || branchChanged)
        {
            user.TokenVersion++;
        }

        if (roleChanged)
        {
            var roleName = await _context.Roles.Where(r => r.RoleId == request.RoleId).Select(r => r.RoleName).FirstAsync();
            _audit.Add(GetCurrentUserId(), AuditActions.Role, "USERS", user.Username, $"Đổi vai trò → {roleName}");
        }
        else
        {
            _audit.Add(GetCurrentUserId(), AuditActions.Update, "USERS", user.Username, "Cập nhật người dùng");
        }

        await _context.SaveChangesAsync();
        _cache.Remove(UserStatusCache.Key(id));

        return Ok(new { message = "Cập nhật tài khoản thành công." });
    }

    // PATCH: api/User/1/status
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] UserStatusRequest request)
    {
        var status = request.Status.Trim().ToUpperInvariant();
        if (status != "ACTIVE" && status != "LOCKED")
        {
            return BadRequest(new { message = "Trạng thái chỉ nhận ACTIVE hoặc LOCKED." });
        }

        if (id == GetCurrentUserId())
        {
            return BadRequest(new { message = "Không thể tự khóa tài khoản của chính mình." });
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy tài khoản." });
        }

        // Khóa => thu hồi mọi token đã phát hành (tăng TokenVersion). Trước đây chỉ chặn trong lúc bị khóa,
        // mở khóa xong token cũ (có thể đã lộ) lại dùng được tới khi hết hạn 8 giờ (T04)
        if (status == "LOCKED" && user.Status != "LOCKED")
        {
            user.TokenVersion++;
        }

        user.Status = status;
        _audit.Add(GetCurrentUserId(), AuditActions.Update, "USERS", user.Username,
            status == "ACTIVE" ? "Mở khóa tài khoản" : "Khóa tài khoản");
        await _context.SaveChangesAsync();

        // Token đang dùng của tài khoản này bị từ chối ngay ở request kế tiếp
        _cache.Remove(UserStatusCache.Key(id));

        return Ok(new
        {
            message = status == "ACTIVE" ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản."
        });
    }

    // Vai trò phải tồn tại; Manager/Officer bắt buộc thuộc một chi nhánh đang hoạt động
    private async Task<string?> ValidateRoleAndBranchAsync(int roleId, int? branchId)
    {
        var role = await _context.Roles.FindAsync(roleId);
        if (role == null)
        {
            return "Vai trò không tồn tại.";
        }

        if (branchId.HasValue &&
            !await _context.Branches.AnyAsync(b => b.BranchId == branchId && b.Status == "ACTIVE"))
        {
            return "Chi nhánh không tồn tại.";
        }

        if (!branchId.HasValue && role.RoleName is AppRoles.Manager or AppRoles.Officer)
        {
            return "Quản lý và Nhân viên phải thuộc một chi nhánh.";
        }

        return null;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }
}
