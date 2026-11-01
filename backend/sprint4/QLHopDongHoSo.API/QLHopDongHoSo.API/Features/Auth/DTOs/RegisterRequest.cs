using System.ComponentModel.DataAnnotations;

namespace QLHopDongHoSo.API.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "Username không được để trống.")]
    [StringLength(50, MinimumLength = 3,
        ErrorMessage = "Username phải từ 3 đến 50 ký tự.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password không được để trống.")]
    [StringLength(100, MinimumLength = 8,
        ErrorMessage = "Password phải có ít nhất 8 ký tự.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ tên không được để trống.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }
}