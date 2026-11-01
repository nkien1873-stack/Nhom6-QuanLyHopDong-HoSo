using System.ComponentModel.DataAnnotations;

namespace QLHopDongHoSo.API.DTOs.Documents;

public class DocumentListQuery
{
    // Tài liệu của một hồ sơ (trang chi tiết)
    public int? ApplicationId { get; set; }

    // Tìm theo tên tệp, mã hợp đồng, tên khách hàng
    [MinLength(2, ErrorMessage = "Từ khóa tìm kiếm cần ít nhất 2 ký tự.")]
    [MaxLength(100, ErrorMessage = "Từ khóa tìm kiếm tối đa 100 ký tự.")]
    public string? Q { get; set; }

    // ALL | PDF | DOC | XLS | IMG
    [MaxLength(10)]
    public string? Kind { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public class DocumentPagedResult : PagedResult<DocumentResponse>
{
    // Tổng dung lượng (byte) của các tài liệu khớp bộ lọc tìm kiếm
    public long TotalSize { get; set; }
}
