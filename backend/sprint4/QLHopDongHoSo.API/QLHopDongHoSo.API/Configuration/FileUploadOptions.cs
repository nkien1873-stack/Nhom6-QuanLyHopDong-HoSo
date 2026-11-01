namespace QLHopDongHoSo.API.Configuration;

public class FileUploadOptions
{
    public string StoragePath { get; set; } = "Storage/Documents";

    public int MaxFileSizeMB { get; set; } = 10;

    public string[] AllowedExtensions { get; set; } = [];
}