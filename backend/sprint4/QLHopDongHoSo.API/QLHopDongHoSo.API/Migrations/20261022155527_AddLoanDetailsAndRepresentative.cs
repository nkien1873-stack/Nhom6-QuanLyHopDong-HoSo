using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QLHopDongHoSo.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanDetailsAndRepresentative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Representative",
                table: "CUSTOMERS",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Collateral",
                table: "CONTRACTS",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InterestRate",
                table: "CONTRACTS",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "CONTRACTS",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TermMonths",
                table: "CONTRACTS",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Representative",
                table: "CUSTOMERS");

            migrationBuilder.DropColumn(
                name: "Collateral",
                table: "CONTRACTS");

            migrationBuilder.DropColumn(
                name: "InterestRate",
                table: "CONTRACTS");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "CONTRACTS");

            migrationBuilder.DropColumn(
                name: "TermMonths",
                table: "CONTRACTS");
        }
    }
}
