using QLHopDongHoSo.API.DTOs;

namespace QLHopDongHoSo.API.Services;

public interface IAuthService
{
    // clientIp: khóa tạm khi sai nhiều lần áp theo cặp (tài khoản, IP)
    Task<LoginResponse?> LoginAsync(LoginRequest request, string clientIp);

    Task<RegisterResponse?> RegisterAsync(
        RegisterRequest request);
}