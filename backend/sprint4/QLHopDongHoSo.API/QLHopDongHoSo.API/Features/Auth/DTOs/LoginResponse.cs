namespace QLHopDongHoSo.API.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    // Chi nhánh của người dùng (null với Admin/Auditor không gán chi nhánh)
    public int? BranchId { get; set; }

    public string? BranchName { get; set; }
}