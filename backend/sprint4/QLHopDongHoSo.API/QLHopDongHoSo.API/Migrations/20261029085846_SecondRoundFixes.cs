using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QLHopDongHoSo.API.Migrations
{
    /// <inheritdoc />
    public partial class SecondRoundFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CONTRACTS_CreatedBy",
                table: "CONTRACTS");

            migrationBuilder.AddColumn<int>(
                name: "TokenVersion",
                table: "USERS",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CUSTOMERS",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACTS_CreatedBy_Status",
                table: "CONTRACTS",
                columns: new[] { "CreatedBy", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CONTRACTS_CreatedBy_Status",
                table: "CONTRACTS");

            migrationBuilder.DropColumn(
                name: "TokenVersion",
                table: "USERS");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CUSTOMERS");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACTS_CreatedBy",
                table: "CONTRACTS",
                column: "CreatedBy");
        }
    }
}
