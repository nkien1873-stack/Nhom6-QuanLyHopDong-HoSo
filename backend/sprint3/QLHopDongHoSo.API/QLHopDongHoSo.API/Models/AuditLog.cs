namespace QLHopDongHoSo.API.Models;

public class AuditLog
{
    public long LogId { get; set; }

    public int? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? IPAddress { get; set; }

    // Navigation properties
    public User? User { get; set; }
}
