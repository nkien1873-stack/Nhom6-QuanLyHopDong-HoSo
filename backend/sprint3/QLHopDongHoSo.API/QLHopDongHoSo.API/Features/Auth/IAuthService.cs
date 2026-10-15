using QLHopDongHoSo.API.DTOs;

namespace QLHopDongHoSo.API.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);

    Task<RegisterResponse?> RegisterAsync(
        RegisterRequest request);
}