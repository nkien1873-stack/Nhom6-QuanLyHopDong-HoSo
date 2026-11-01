using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace QLHopDongHoSo.API.Data;

// Cấp mã tự động không trùng khi nhiều người tạo cùng lúc.
// Trước đây đọc mã lớn nhất rồi +1 => hai yêu cầu đồng thời lấy cùng một số và một bên lỗi 500.
// Nay tăng bộ đếm CODE_COUNTERS bằng một câu UPDATE/MERGE nguyên tử trên kết nối RIÊNG (tự commit ngay),
// giống SEQUENCE của SQL Server: mỗi yêu cầu nhận một số riêng và khóa bộ đếm được nhả ngay,
// không bắt các lệnh tạo khác chờ tới khi transaction tạo hồ sơ kết thúc. Lệnh tạo lỗi thì bỏ trống số đó.
public static class CodeGenerator
{
    // HD-2026-0001 (hợp đồng), HS-2026-0001 (hồ sơ)
    public static async Task<string> NextContractCodeAsync(this ApplicationDbContext context) =>
        await NextAsync(context, "HD", $"HD-{DateTime.Now.Year}-", 4,
            "SELECT [ContractCode] FROM [CONTRACTS] WHERE [ContractCode] LIKE @start + '%'",
            code => context.Contracts.AnyAsync(c => c.ContractCode == code));

    public static async Task<string> NextApplicationCodeAsync(this ApplicationDbContext context) =>
        await NextAsync(context, "HS", $"HS-{DateTime.Now.Year}-", 4,
            "SELECT [ApplicationCode] FROM [APPLICATIONS] WHERE [ApplicationCode] LIKE @start + '%'",
            code => context.Applications.AnyAsync(a => a.ApplicationCode == code));

    // KH2026001 (khách hàng)
    public static async Task<string> NextCustomerCodeAsync(this ApplicationDbContext context) =>
        await NextAsync(context, "KH", $"KH{DateTime.Now.Year}", 3,
            "SELECT [CustomerCode] FROM [CUSTOMERS] WHERE [CustomerCode] LIKE @start + '%'",
            code => context.Customers.AnyAsync(c => c.CustomerCode == code));

    private static async Task<string> NextAsync(
        ApplicationDbContext context,
        string prefix,
        string start,
        int digits,
        string existingCodesSql,
        Func<string, Task<bool>> exists)
    {
        var year = DateTime.Now.Year;

        // Mã nhập tay có thể đã chiếm số tiếp theo => bỏ qua tới số còn trống
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var number = await IncrementAsync(context, prefix, year, start, existingCodesSql);
            var code = $"{start}{number.ToString().PadLeft(digits, '0')}";
            if (!await exists(code))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Không cấp được mã mới, vui lòng thử lại.");
    }

    private static async Task<int> IncrementAsync(
        ApplicationDbContext context,
        string prefix,
        int year,
        string start,
        string existingCodesSql)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync();

        var updated = await ExecuteScalarAsync(connection,
            "UPDATE [CODE_COUNTERS] SET [LastValue] = [LastValue] + 1 OUTPUT inserted.[LastValue] " +
            "WHERE [Prefix] = @prefix AND [Year] = @year;",
            ("@prefix", prefix), ("@year", year));
        if (updated.HasValue)
        {
            return updated.Value;
        }

        // Lần đầu trong năm: khởi tạo bộ đếm từ mã lớn nhất đang có trong bảng
        var seed = await MaxExistingAsync(connection, existingCodesSql, start);
        var merged = await ExecuteScalarAsync(connection,
            "MERGE [CODE_COUNTERS] WITH (HOLDLOCK) AS t " +
            "USING (SELECT @prefix AS [Prefix], @year AS [Year]) AS s ON t.[Prefix] = s.[Prefix] AND t.[Year] = s.[Year] " +
            "WHEN MATCHED THEN UPDATE SET [LastValue] = t.[LastValue] + 1 " +
            "WHEN NOT MATCHED THEN INSERT ([Prefix], [Year], [LastValue]) VALUES (@prefix, @year, @seed + 1) " +
            "OUTPUT inserted.[LastValue];",
            ("@prefix", prefix), ("@year", year), ("@seed", seed));

        return merged ?? throw new InvalidOperationException("Không cập nhật được bộ đếm mã.");
    }

    private static async Task<int> MaxExistingAsync(SqlConnection connection, string sql, string start)
    {
        var max = 0;
        await using var command = CreateCommand(connection, sql, ("@start", start));
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = reader.GetString(0);
            if (int.TryParse(code[start.Length..], out var n) && n > max)
            {
                max = n;
            }
        }

        return max;
    }

    private static async Task<int?> ExecuteScalarAsync(
        SqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, sql, parameters);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    private static SqlCommand CreateCommand(
        SqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command;
    }
}
