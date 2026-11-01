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

        if (!HasMatchingSignature(file))
        {
            return "Nội dung file không khớp với đuôi " +
                   $"{Path.GetExtension(file.FileName).ToLowerInvariant()} (file có thể bị đổi tên).";
        }

        return null;
    }

    // Chữ ký đầu file (magic bytes) theo từng đuôi: chặn file .exe đổi tên thành .pdf...
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];                            // docx, xlsx (Office Open XML)
    private static readonly byte[] Ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];    // doc, xls (Office 97–2003)
    private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = [Pdf],
        [".docx"] = [Zip],
        [".xlsx"] = [Zip],
        [".doc"] = [Ole],
        [".xls"] = [Ole],
        [".jpg"] = [Jpg],
        [".jpeg"] = [Jpg],
        [".png"] = [Png]
    };

    public static bool HasMatchingSignature(IFormFile file)
    {
        if (!Signatures.TryGetValue(Path.GetExtension(file.FileName), out var expected))
        {
            return true; // đuôi được cấu hình thêm nhưng chưa có chữ ký => chỉ kiểm tra đuôi
        }

        Span<byte> head = stackalloc byte[8];
        using var stream = file.OpenReadStream();
        var read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        var data = head[..read];

        foreach (var sig in expected)
        {
            if (data.StartsWith(sig))
            {
                return true;
            }
        }

        return false;
    }
}