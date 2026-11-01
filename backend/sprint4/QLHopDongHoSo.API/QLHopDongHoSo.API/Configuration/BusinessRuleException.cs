namespace QLHopDongHoSo.API.Services;

// Vi phạm quy tắc nghiệp vụ (mã trùng, sai trạng thái, file không hợp lệ...) – controller trả 400/409 kèm thông báo.
// Tách riêng khỏi InvalidOperationException vì SqlClient cũng ném InvalidOperationException khi cạn kết nối:
// lỗi hạ tầng đó phải về ApiExceptionHandler (503, thông báo chung), không được báo thành "trùng dữ liệu".
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }

    // Lỗi có mã nghiệp vụ (ERR_SOD_01, ERR_DOC_01...): thông báo bắt đầu bằng mã, response trả thêm trường "code"
    public BusinessRuleException(string code, string message) : base($"{code}: {message}")
    {
        Code = code;
    }

    public string? Code { get; }

    // Phân tách nhiệm vụ (Maker ≠ Checker) là lỗi phân quyền => 403
    public bool IsForbidden => Code?.StartsWith("ERR_SOD", StringComparison.Ordinal) == true;
}
