namespace QLHopDongHoSo.API.Data;

// Tìm "chứa từ khóa" (LIKE '%k%') trên bảng lớn: so sánh nhị phân trên chữ in hoa
// (UPPER(cột) COLLATE Latin1_General_100_BIN2) nhanh hơn ~3 lần so với collation Unicode mặc định,
// vẫn không phân biệt hoa/thường. Dùng cho mã, tên tệp, nhật ký; tên khách hàng (bảng nhỏ) vẫn tìm không dấu.
public static class SearchText
{
    public const string Binary = "Latin1_General_100_BIN2";

    // Từ khóa đưa về chữ in hoa để so với UPPER(cột).
    // Trong LINQ viết: EF.Functions.Collate(x.Col.ToUpper(), SearchText.Binary).Contains(key)
    // => UPPER([Col]) COLLATE Latin1_General_100_BIN2 LIKE '%KEY%'
    public static string Key(string keyword) => keyword.Trim().ToUpperInvariant();
}
