using Microsoft.AspNetCore.Http;

namespace QLHopDongHoSo.API.Services;

public interface IFileValidationService
{
    bool IsAllowedExtension(IFormFile file);

    bool IsAllowedSize(IFormFile file);

    string? Validate(IFormFile file);
}