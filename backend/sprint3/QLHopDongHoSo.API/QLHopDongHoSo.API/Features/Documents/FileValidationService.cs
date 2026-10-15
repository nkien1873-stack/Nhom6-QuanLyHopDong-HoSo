using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using QLHopDongHoSo.API.Configuration;

namespace QLHopDongHoSo.API.Services;

public class FileValidationService : IFileValidationService
{
    private readonly FileUploadOptions _options;

    public FileValidationService(
        IOptions<FileUploadOptions> options)
    {
        _options = options.Value;
    }

    public bool IsAllowedExtension(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);

        return _options.AllowedExtensions
            .Contains(
                extension,
                StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAllowedSize(IFormFile file)
    {
        var maxSize =
            (long)_options.MaxFileSizeMB * 1024 * 1024;

        return file.Length <= maxSize;
    }

    public string? Validate(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return "File không được để trống.";
        }

        if (!IsAllowedExtension(file))
        {
            var allowed = string.Join(
                ", ",
                _options.AllowedExtensions);

            return $"Loại file không được hỗ trợ. " +
                   $"Chỉ cho phép: {allowed}";
        }

        if (!IsAllowedSize(file))
        {
            return $"Dung lượng file vượt quá " +
                   $"{_options.MaxFileSizeMB} MB.";
        }

        return null;
    }
}