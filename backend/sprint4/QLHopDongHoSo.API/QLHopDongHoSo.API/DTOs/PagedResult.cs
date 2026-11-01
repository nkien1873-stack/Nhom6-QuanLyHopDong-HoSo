namespace QLHopDongHoSo.API.DTOs;

// Kết quả phân trang dùng chung: danh sách 1 trang + tổng số + số đếm theo nhóm (tab) nếu có
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

    // Ví dụ số hồ sơ theo từng tab trạng thái, số tài liệu theo loại tệp...
    public Dictionary<string, int>? Counts { get; set; }

    // page rất lớn (vd 2147483647) => (page - 1) * pageSize tràn int ở Skip(): kẹp page để phép nhân luôn hợp lệ
    public static (int Page, int PageSize) Normalize(int page, int pageSize, int defaultSize = 20, int maxSize = 100)
    {
        var size = pageSize < 1 ? defaultSize : Math.Min(pageSize, maxSize);
        var maxPage = int.MaxValue / size;
        return (Math.Clamp(page, 1, maxPage), size);
    }
}
