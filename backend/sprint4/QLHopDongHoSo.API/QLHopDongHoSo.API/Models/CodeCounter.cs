namespace QLHopDongHoSo.API.Models;

// Bộ đếm cấp mã tự động (HD-2026-0001, HS-2026-0001, KH2026001) theo tiền tố + năm.
// Tăng bằng một câu lệnh SQL nguyên tử nên nhiều người tạo cùng lúc không bị trùng mã.
public class CodeCounter
{
    public string Prefix { get; set; } = string.Empty;

    public int Year { get; set; }

    public int LastValue { get; set; }
}
