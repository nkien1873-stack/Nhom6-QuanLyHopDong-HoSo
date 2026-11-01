using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Services;

namespace QLHopDongHoSo.API.Configuration;

// Lỗi chưa được controller bắt => trả mã 4xx đúng nghĩa thay vì 500, kèm { message } như các API khác.
// - Hai người cùng duyệt/sửa/xóa một hồ sơ (rowversion lệch)  -> 409
// - Trùng khóa duy nhất (mã, CCCD/MST, username) khi thêm cùng lúc -> 409
// - Tham số sai kiểu/quá dài                                    -> 400
public class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Deadlock (1205) có thể nằm sâu trong lớp bọc: DbUpdateException, hoặc InvalidOperationException của EF
        // ("An exception has been raised that is likely due to a transient failure") => trước đây rơi xuống 500 (T05)
        var sql = FindSqlException(exception);

        var (status, message) = exception switch
        {
            _ when sql is { Number: 1205 } => (StatusCodes.Status409Conflict,
                "Hệ thống đang bận xử lý yêu cầu khác trên cùng dữ liệu. Vui lòng thử lại."),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                "Dữ liệu vừa được người khác thay đổi. Vui lòng tải lại trang rồi thử lại."),
            // Ghi file thất bại (đầy đĩa, mất quyền ghi thư mục lưu trữ...) – không phải lỗi người dùng (T06)
            IOException and not BadHttpRequestException => (StatusCodes.Status503ServiceUnavailable,
                "Máy chủ không lưu được tệp (ổ đĩa đầy hoặc lỗi ghi). Vui lòng thử lại sau hoặc báo quản trị viên."),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (StatusCodes.Status409Conflict,
                "Dữ liệu bị trùng (mã, CCCD/MST hoặc tên đăng nhập đã tồn tại)."),
            DbUpdateException { InnerException: SqlException { Number: 1205 } } => (StatusCodes.Status409Conflict,
                "Hệ thống đang bận xử lý yêu cầu khác trên cùng dữ liệu. Vui lòng thử lại."),
            SqlException { Number: 8152 or 2628 } or DbUpdateException { InnerException: SqlException { Number: 8152 or 2628 } } =>
                (StatusCodes.Status400BadRequest, "Dữ liệu nhập quá dài."),
            // Quá tải / truy vấn quá lâu: 503 + thông báo chung (không lộ nguyên văn lỗi SqlClient)
            SqlException { Number: -2 } or DbUpdateException { InnerException: SqlException { Number: -2 } } =>
                (StatusCodes.Status503ServiceUnavailable,
                 "Truy vấn mất quá nhiều thời gian. Vui lòng thu hẹp điều kiện tìm (ví dụ khoảng thời gian) rồi thử lại."),
            _ when sql is { Number: -2 } => (StatusCodes.Status503ServiceUnavailable,
                 "Truy vấn mất quá nhiều thời gian. Vui lòng thu hẹp điều kiện tìm (ví dụ khoảng thời gian) rồi thử lại."),
            InvalidOperationException { Source: "Microsoft.Data.SqlClient" } or SqlException => (StatusCodes.Status503ServiceUnavailable,
                "Hệ thống đang quá tải, vui lòng thử lại sau ít phút."),
            InvalidOperationException pool when pool.Message.Contains("connection from the pool") => (StatusCodes.Status503ServiceUnavailable,
                "Hệ thống đang quá tải, vui lòng thử lại sau ít phút."),
            // Lỗi SQL khác bị EF bọc trong InvalidOperationException => 503 thông báo chung, không 500
            InvalidOperationException when sql != null => (StatusCodes.Status503ServiceUnavailable,
                "Hệ thống đang quá tải, vui lòng thử lại sau ít phút."),
            BusinessRuleException => (StatusCodes.Status409Conflict, exception.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, exception.Message),
            BadHttpRequestException bad => (bad.StatusCode, "Yêu cầu không hợp lệ."),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "Yêu cầu đã bị hủy."),
            _ => (StatusCodes.Status500InternalServerError, "Lỗi hệ thống. Vui lòng thử lại sau.")
        };

        if (status >= 500)
        {
            _logger.LogError(exception, "Lỗi không xử lý: {Path}", context.Request.Path);
        }
        else
        {
            _logger.LogWarning("{Status} {Path}: {Message}", status, context.Request.Path, exception.Message);
        }

        if (context.Response.HasStarted)
        {
            return false;
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { message }, cancellationToken);
        return true;
    }

    private static SqlException? FindSqlException(Exception? ex)
    {
        for (var depth = 0; ex != null && depth < 10; ex = ex.InnerException, depth++)
        {
            if (ex is SqlException sql)
            {
                return sql;
            }
        }

        return null;
    }
}
