using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QLHopDongHoSo.API.Migrations
{
    /// <inheritdoc />
    public partial class DocumentRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DOCUMENT_DEFERRALS",
                columns: table => new
                {
                    DeferralID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractID = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedBy = table.Column<int>(type: "int", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DOCUMENT_DEFERRALS", x => x.DeferralID);
                    table.ForeignKey(
                        name: "FK_DOCUMENT_DEFERRALS_CONTRACTS_ContractID",
                        column: x => x.ContractID,
                        principalTable: "CONTRACTS",
                        principalColumn: "ContractID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DOCUMENT_DEFERRALS_USERS_DecidedBy",
                        column: x => x.DecidedBy,
                        principalTable: "USERS",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DOCUMENT_DEFERRALS_USERS_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "USERS",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENT_DEFERRALS_ContractID_DocumentType",
                table: "DOCUMENT_DEFERRALS",
                columns: new[] { "ContractID", "DocumentType" },
                unique: true,
                filter: "[Status] IN ('PENDING', 'APPROVED')");

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENT_DEFERRALS_DecidedBy",
                table: "DOCUMENT_DEFERRALS",
                column: "DecidedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENT_DEFERRALS_RequestedBy",
                table: "DOCUMENT_DEFERRALS",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENT_DEFERRALS_Status_RequestedAt",
                table: "DOCUMENT_DEFERRALS",
                columns: new[] { "Status", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DOCUMENT_DEFERRALS");
        }
    }
}
