using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.DTOs;
using QLHopDongHoSo.API.Services;

namespace QLHopDongHoSo.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // POST: api/auth/register
    [HttpPost("register")]
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

        var result = await _authService.LoginAsync(request);

        if (result == null)
        {
            return Unauthorized(new
            {
                message = "Username hoặc password không đúng."
            });
        }

        return Ok(result);
    }
}