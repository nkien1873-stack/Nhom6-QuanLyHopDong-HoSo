using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QLHopDongHoSo.API.Configuration;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.DTOs;

namespace QLHopDongHoSo.API.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly LoginAttemptTracker _loginAttempts;

    public AuthService(
        ApplicationDbContext context,
        IConfiguration configuration,
        LoginAttemptTracker loginAttempts)
    {
        _context = context;
        _configuration = configuration;
        _loginAttempts = loginAttempts;
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
        var passwordHash = PasswordHasher.Hash(request.Password);

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

    // Hash giả (cùng work factor) để user không tồn tại cũng tốn thời gian như user có thật
    // => không dò được tên đăng nhập qua thời gian phản hồi
    private static readonly Lazy<string> DummyHash =
        new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), PasswordHasher.WorkFactor));

    // ĐĂNG NHẬP
    // Khóa tạm theo cặp (tài khoản, IP): sai N lần từ một máy thì chỉ máy đó bị khóa tạm với tài khoản đó.
    // Người lạ đoán sai không khóa được chủ tài khoản ở máy khác; dò mật khẩu từ một máy vẫn bị chặn
    // (kèm giới hạn 20 lần/phút/IP ở AuthController).
    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        string clientIp)
    {
        var username = request.Username.Trim();
        var maxFailed = _configuration.GetValue("Security:MaxFailedLogins", 5);
        var lockout = TimeSpan.FromMinutes(_configuration.GetValue("Security:LockoutMinutes", 15.0));
        var attempts = _loginAttempts.For(username, clientIp, lockout);

        // Đang bị khóa tạm do sai nhiều lần: không kiểm tra mật khẩu (kể cả đúng), vẫn tốn thời gian như thường
        if (attempts.LockedUntil > DateTime.UtcNow)
        {
            BCrypt.Net.BCrypt.Verify(request.Password, DummyHash.Value);
            var minutes = Math.Max(1, (int)Math.Ceiling((attempts.LockedUntil.Value - DateTime.UtcNow).TotalMinutes));
            throw new LoginLockedException(
                $"Tài khoản tạm khóa trên máy này do đăng nhập sai {maxFailed} lần. Vui lòng thử lại sau {minutes} phút.");
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.Username == username);

        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyHash.Value);

        if (user == null || !passwordValid)
        {
            attempts.RegisterFailure(maxFailed, lockout);
            return null;
        }

        // Đúng mật khẩu nhưng tài khoản bị khóa => báo riêng cho người dùng biết
        if (user.Status != "ACTIVE")
        {
            throw new UnauthorizedAccessException(
                "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
        }

        _loginAttempts.Clear(username, clientIp);

        // Hash cũ dùng work factor cao hơn (chậm hơn) => băm lại theo cấu hình hiện tại
        if (PasswordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = PasswordHasher.Hash(request.Password);
        }

        if (_context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync();
        }

        var token = GenerateJwtToken(
            user.UserId,
            user.Username,
            user.Role!.RoleName,
            user.TokenVersion);

        return new LoginResponse
        {
            Token = token,
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role.RoleName,
            BranchId = user.BranchId,
            BranchName = user.Branch?.BranchName
        };
    }

    // TẠO JWT
    private string GenerateJwtToken(
        int userId,
        string username,
        string role,
        int tokenVersion)
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
                role),

            // Đăng xuất / đổi quyền => TokenVersion trong DB tăng => token này hết hiệu lực (Program.cs)
            new Claim(
                UserStatusCache.TokenVersionClaim,
                tokenVersion.ToString())
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