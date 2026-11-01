using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QLHopDongHoSo.API.Migrations
{
    // T01 – bật READ_COMMITTED_SNAPSHOT: câu đọc (danh sách, chi tiết, dashboard...) đọc phiên bản đã commit
    // thay vì chờ khóa của câu ghi => nhiều người cùng ghi không còn làm câu đọc bị deadlock / 503.
    // Lệnh trình duyệt / xóa tài liệu... vẫn khóa đúng chỗ bằng UPDLOCK (Data/ContractLocks.cs).
    // ALTER DATABASE không chạy được trong transaction => suppressTransaction; ROLLBACK IMMEDIATE ngắt các
    // phiên khác đang mở trên DB (chỉ xảy ra một lần, lúc nâng cấp).
    /// <inheritdoc />
    public partial class EnableReadCommittedSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID()) = 0 " +
                "EXEC(N'ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE');",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID()) = 1 " +
                "EXEC(N'ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE');",
                suppressTransaction: true);
        }
    }
}
