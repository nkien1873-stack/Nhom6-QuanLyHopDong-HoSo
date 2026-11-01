namespace QLHopDongHoSo.API.Models;

public class Customer
{
    public int CustomerId { get; set; }

    public string CustomerCode { get; set; } = string.Empty;

    public string CustomerType { get; set; } = string.Empty; // INDIVIDUAL / CORPORATE

    public string FullName { get; set; } = string.Empty;

    // Người đại diện (khách hàng doanh nghiệp)
    public string? Representative { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? IdentityNumber { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string Status { get; set; } = "ACTIVE";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // rowversion: hai người sửa cùng lúc thì người lưu sau nhận 409 thay vì ghi đè
    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public ICollection<Application> Applications { get; set; } = new List<Application>();
}
