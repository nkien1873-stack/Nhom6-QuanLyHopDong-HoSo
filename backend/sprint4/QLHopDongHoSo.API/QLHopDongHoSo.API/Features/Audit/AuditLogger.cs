using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.Models;

namespace QLHopDongHoSo.API.Services;

// Mã hành động ghi vào AUDIT_LOGS.Action (FE hiển thị nhãn tiếng Việt theo mã này)
public static class AuditActions
{
    public const string Login = "LOGIN";
    public const string Logout = "LOGOUT";
    public const string LoginFail = "LOGIN_FAIL";
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Submit = "SUBMIT";
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
    public const string Upload = "UPLOAD";
    public const string Delete = "DELETE";
    public const string Role = "ROLE";
}

public interface IAuditLogger
{
    // Ghi ngay (lần lưu riêng) – dùng cho sự kiện không kèm thay đổi dữ liệu (đăng nhập, đăng xuất...)
    Task LogAsync(int? userId, string action, string entityType, string entityId, string? description);

    // Chỉ thêm vào DbContext, KHÔNG lưu: được ghi cùng SaveChanges (cùng transaction) với thao tác chính
    // => thao tác thành công thì chắc chắn có nhật ký, thao tác lỗi / tiến trình dừng giữa chừng thì cả hai cùng không có
    void Add(int? userId, string action, string entityType, string entityId, string? description);
}

// Ghi nhật ký thao tác kèm IP người dùng. Lỗi ghi log không được làm hỏng thao tác chính.
public class AuditLogger : IAuditLogger
{
    public void Add(int? userId, string action, string entityType, string entityId, string? description) =>
        _context.AuditLogs.Add(Build(userId, action, entityType, entityId, description));

    private AuditLog Build(int? userId, string action, string entityType, string entityId, string? description)
    {
        var ip = _http.HttpContext?.Connection.RemoteIpAddress;
        return new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = Truncate(entityId, 50),
            Description = Truncate(description, 1000),
            CreatedAt = DateTime.Now,
            IPAddress = ip == null ? null : (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString()
        };
    }

    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(ApplicationDbContext context, IHttpContextAccessor http, ILogger<AuditLogger> logger)
    {
        _context = context;
        _http = http;
        _logger = logger;
    }

    public async Task LogAsync(int? userId, string action, string entityType, string entityId, string? description)
    {
        try
        {
            _context.AuditLogs.Add(Build(userId, action, entityType, entityId, description));
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không ghi được audit log {Action} {Entity}", action, entityId);
        }
    }

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value ?? string.Empty : value.Length <= max ? value : value[..max];
}
