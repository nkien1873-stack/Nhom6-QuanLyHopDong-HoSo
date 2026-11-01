using System.ComponentModel.DataAnnotations;

namespace QLHopDongHoSo.API.DTOs.Users;

public class UserResponse
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public int RoleId { get; set; }

    public string? RoleName { get; set; }

    public int? BranchId { get; set; }

    public string? BranchName { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
}

public class UserCreateRequest
{
    [Required(ErrorMessage = "Username không được để trống.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username phải từ 3 đến 50 ký tự.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password không được để trống.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password phải có ít nhất 8 ký tự.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ tên không được để trống.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn vai trò.")]
    public int RoleId { get; set; }

    public int? BranchId { get; set; }
}

public class UserUpdateRequest
{
    [Required(ErrorMessage = "Họ tên không được để trống.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn vai trò.")]
    public int RoleId { get; set; }

    public int? BranchId { get; set; }
}

public class UserStatusRequest
{
    // ACTIVE hoặc LOCKED
    public string Status { get; set; } = string.Empty;
}
