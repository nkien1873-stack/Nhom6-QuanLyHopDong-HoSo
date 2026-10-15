using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.DTOs;

namespace QLHopDongHoSo.API.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // ĐĂNG KÝ
    public async Task<RegisterResponse?> RegisterAsync(
        RegisterRequest request)
    {
        var username = request.Username.Trim();

        // 1. Kiểm tra username đã tồn tại
        var existingUser = await _context.Users
            .AnyAsync(u => u.Username == username);

        if (existingUser)
        {
            return null;
        }

        // 2. Lấy role mặc định từ cấu hình
        var defaultRoleName =
            _configuration["Auth:DefaultRoleName"];

        if (string.IsNullOrWhiteSpace(defaultRoleName))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình Auth:DefaultRoleName.");
        }

        var role = await _context.Roles
            .FirstOrDefaultAsync(r =>
                r.RoleName == defaultRoleName);

        if (role == null)
        {
            throw new InvalidOperationException(
                $"Không tìm thấy role '{defaultRoleName}'.");
        }

        // 3. Mã hóa password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(
            request.Password);

        // 4. Tạo user mới
        var user = new Models.User
        {
            Username = username,
            PasswordHash = passwordHash,
            FullName = request.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email)
                ? null
                : request.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone)
                ? null
                : request.Phone.Trim(),

            RoleId = role.RoleId,
            Status = "ACTIVE",
            CreatedAt = DateTime.Now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // 5. Trả thông tin tài khoản
        return new RegisterResponse
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Role = role.RoleName,
            Message = "Đăng ký tài khoản thành công."
        };
    }

    // ĐĂNG NHẬP
    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request)
    {
        var username = request.Username.Trim();

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u =>
                u.Username == username &&
                u.Status == "ACTIVE");

        if (user == null)
        {
            return null;
        }

        var passwordValid = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash);

        if (!passwordValid)
        {
            return null;
        }

        var token = GenerateJwtToken(
            user.UserId,
            user.Username,
            user.Role!.RoleName);

        return new LoginResponse
        {
            Token = token,
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.RoleName
        };
    }

    // TẠO JWT
    private string GenerateJwtToken(
        int userId,
        string username,
        string role)
    {
        var jwtKey = _configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException(
                "JWT Key chưa được cấu hình.");
        }

        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        var duration = _configuration.GetValue<int>(
            "Jwt:DurationInMinutes");

        if (duration <= 0)
        {
            throw new InvalidOperationException(
                "Jwt:DurationInMinutes phải lớn hơn 0.");
        }

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                userId.ToString()),

            new Claim(
                ClaimTypes.Name,
                username),

            new Claim(
                ClaimTypes.Role,
                role)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(duration),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}