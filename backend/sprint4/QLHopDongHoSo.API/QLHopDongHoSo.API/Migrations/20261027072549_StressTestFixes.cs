using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QLHopDongHoSo.API.Migrations
{
    /// <inheritdoc />
    public partial class StressTestFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_APPLICATIONS_CustomerID",
                table: "APPLICATIONS");

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "USERS",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEnd",
                table: "USERS",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "DOCUMENTS",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CONTRACTS",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CONTRACTS",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            // Hợp đồng có sẵn: lần cập nhật = lần đổi trạng thái cuối (không có lịch sử thì = ngày tạo)
            migrationBuilder.Sql(
                "UPDATE c SET c.[UpdatedAt] = COALESCE(" +
                "(SELECT MAX(h.[ChangedAt]) FROM [CONTRACT_STATUS_HISTORY] h WHERE h.[ContractID] = c.[ContractID]), c.[CreatedAt]) " +
                "FROM [CONTRACTS] c;");

            // Tài liệu có sẵn: FileSize để NULL, backend đọc dung lượng trên đĩa một lần lúc khởi động (DemoDataUpgrade)

            migrationBuilder.AddColumn<string>(
                name: "FileKind",
                table: "DOCUMENTS",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: false,
                computedColumnSql: "CASE WHEN [DocumentName] LIKE N'%.pdf' THEN N'PDF' WHEN [DocumentName] LIKE N'%.doc' OR [DocumentName] LIKE N'%.docx' THEN N'DOC' WHEN [DocumentName] LIKE N'%.xls' OR [DocumentName] LIKE N'%.xlsx' OR [DocumentName] LIKE N'%.csv' THEN N'XLS' WHEN [DocumentName] LIKE N'%.jpg' OR [DocumentName] LIKE N'%.jpeg' OR [DocumentName] LIKE N'%.png' OR [DocumentName] LIKE N'%.gif' OR [DocumentName] LIKE N'%.webp' THEN N'IMG' ELSE N'FILE' END",
                stored: true);

            migrationBuilder.CreateTable(
                name: "CODE_COUNTERS",
                columns: table => new
                {
                    Prefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LastValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CODE_COUNTERS", x => new { x.Prefix, x.Year });
                });

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENTS_FileKind",
                table: "DOCUMENTS",
                column: "FileKind");

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENTS_UploadedAt",
                table: "DOCUMENTS",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMERS_IdentityNumber",
                table: "CUSTOMERS",
                column: "IdentityNumber",
                unique: true,
                filter: "[IdentityNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACTS_ExpiryDate",
                table: "CONTRACTS",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACTS_Status",
                table: "CONTRACTS",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACTS_UpdatedAt",
                table: "CONTRACTS",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACT_STATUS_HISTORY_NewStatus_ChangedAt",
                table: "CONTRACT_STATUS_HISTORY",
                columns: new[] { "NewStatus", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOGS_CreatedAt_LogID",
                table: "AUDIT_LOGS",
                columns: new[] { "CreatedAt", "LogID" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_APPLICATIONS_CustomerID_CreatedAt",
                table: "APPLICATIONS",
                columns: new[] { "CustomerID", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CODE_COUNTERS");

            migrationBuilder.DropIndex(
                name: "IX_DOCUMENTS_FileKind",
                table: "DOCUMENTS");

            migrationBuilder.DropIndex(
                name: "IX_DOCUMENTS_UploadedAt",
                table: "DOCUMENTS");

            migrationBuilder.DropIndex(
                name: "IX_CUSTOMERS_IdentityNumber",
                table: "CUSTOMERS");

            migrationBuilder.DropIndex(
                name: "IX_CONTRACTS_ExpiryDate",
                table: "CONTRACTS");

            migrationBuilder.DropIndex(
                name: "IX_CONTRACTS_Status",
                table: "CONTRACTS");

            migrationBuilder.DropIndex(
                name: "IX_CONTRACTS_UpdatedAt",
                table: "CONTRACTS");

            migrationBuilder.DropIndex(
                name: "IX_CONTRACT_STATUS_HISTORY_NewStatus_ChangedAt",
                table: "CONTRACT_STATUS_HISTORY");

            migrationBuilder.DropIndex(
                name: "IX_AUDIT_LOGS_CreatedAt_LogID",
                table: "AUDIT_LOGS");

            migrationBuilder.DropIndex(
                name: "IX_APPLICATIONS_CustomerID_CreatedAt",
                table: "APPLICATIONS");

            migrationBuilder.DropColumn(
                name: "FileKind",
                table: "DOCUMENTS");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "USERS");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "USERS");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "DOCUMENTS");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CONTRACTS");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CONTRACTS");

            migrationBuilder.CreateIndex(
                name: "IX_APPLICATIONS_CustomerID",
                table: "APPLICATIONS",
                column: "CustomerID");
        }
    }
}
