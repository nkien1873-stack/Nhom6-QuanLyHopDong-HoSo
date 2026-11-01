using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QLHopDongHoSo.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BRANCHES",
                columns: table => new
                {
                    BranchID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BRANCHES", x => x.BranchID);
                });

            migrationBuilder.CreateTable(
                name: "CONTRACT_TYPES",
                columns: table => new
                {
                    ContractTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTRACT_TYPES", x => x.ContractTypeID);
                });

            migrationBuilder.CreateTable(
                name: "CUSTOMERS",
                columns: table => new
                {
                    CustomerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IdentityNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUSTOMERS", x => x.CustomerID);
                });

            migrationBuilder.CreateTable(
                name: "ROLES",
                columns: table => new
                {
                    RoleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROLES", x => x.RoleID);
                });

            migrationBuilder.CreateTable(
                name: "USERS",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RoleID = table.Column<int>(type: "int", nullable: false),
                    BranchID = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USERS", x => x.UserID);
                    table.ForeignKey(
                        name: "FK_USERS_BRANCHES_BranchID",
                        column: x => x.BranchID,
                        principalTable: "BRANCHES",
                        principalColumn: "BranchID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_USERS_ROLES_RoleID",
                        column: x => x.RoleID,
                        principalTable: "ROLES",
                        principalColumn: "RoleID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "APPLICATIONS",
                columns: table => new
                {
                    ApplicationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    ApplicationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssignedTo = table.Column<int>(type: "int", nullable: true),
                    BranchID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APPLICATIONS", x => x.ApplicationID);
                    table.ForeignKey(
                        name: "FK_APPLICATIONS_BRANCHES_BranchID",
                        column: x => x.BranchID,
                        principalTable: "BRANCHES",
                        principalColumn: "BranchID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_APPLICATIONS_CUSTOMERS_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "CUSTOMERS",
                        principalColumn: "CustomerID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_APPLICATIONS_USERS_AssignedTo",
                        column: x => x.AssignedTo,
                        principalTable: "USERS",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AUDIT_LOGS",
                columns: table => new
                {
                    LogID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IPAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUDIT_LOGS", x => x.LogID);
                    table.ForeignKey(
                        name: "FK_AUDIT_LOGS_USERS_UserID",
                        column: x => x.UserID,
                        principalTable: "USERS",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "APPROVALS",
                columns: table => new
                {
                    ApprovalID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationID = table.Column<int>(type: "int", nullable: false),
                    ApproverID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APPROVALS", x => x.ApprovalID);
                    table.ForeignKey(
                        name: "FK_APPROVALS_APPLICATIONS_ApplicationID",
                        column: x => x.ApplicationID,
                        principalTable: "APPLICATIONS",
                        principalColumn: "ApplicationID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_APPROVALS_USERS_ApproverID",
                        column: x => x.ApproverID,
                        principalTable: "USERS",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CONTRACTS",
                columns: table => new
                {
                    ContractID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApplicationID = table.Column<int>(type: "int", nullable: false),
                    ContractTypeID = table.Column<int>(type: "int", nullable: false),
                    BranchID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    SignedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTRACTS", x => x.ContractID);
                    table.ForeignKey(
                        name: "FK_CONTRACTS_APPLICATIONS_ApplicationID",
                        column: x => x.ApplicationID,
                        principalTable: "APPLICATIONS",
                        principalColumn: "ApplicationID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CONTRACTS_BRANCHES_BranchID",
                        column: x => x.BranchID,
                        principalTable: "BRANCHES",
                        principalColumn: "BranchID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CONTRACTS_CONTRACT_TYPES_ContractTypeID",
                        column: x => x.ContractTypeID,
                        principalTable: "CONTRACT_TYPES",
                        principalColumn: "ContractTypeID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CONTRACTS_USERS_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "USERS",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DOCUMENTS",
                columns: table => new
                {
                    DocumentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationID = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedBy = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerificationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VerifiedBy = table.Column<int>(type: "int", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DOCUMENTS", x => x.DocumentID);
                    table.ForeignKey(
                        name: "FK_DOCUMENTS_APPLICATIONS_ApplicationID",
                        column: x => x.ApplicationID,
                        principalTable: "APPLICATIONS",
                        principalColumn: "ApplicationID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DOCUMENTS_USERS_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "USERS",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DOCUMENTS_USERS_VerifiedBy",
                        column: x => x.VerifiedBy,
                        principalTable: "USERS",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CONTRACT_STATUS_HISTORY",
                columns: table => new
                {
                    HistoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractID = table.Column<int>(type: "int", nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ChangedBy = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTRACT_STATUS_HISTORY", x => x.HistoryID);
                    table.ForeignKey(
                        name: "FK_CONTRACT_STATUS_HISTORY_CONTRACTS_ContractID",
                        column: x => x.ContractID,
                        principalTable: "CONTRACTS",
                        principalColumn: "ContractID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CONTRACT_STATUS_HISTORY_USERS_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "USERS",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_APPLICATIONS_ApplicationCode",
                table: "APPLICATIONS",
                column: "ApplicationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_APPLICATIONS_AssignedTo",
                table: "APPLICATIONS",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_APPLICATIONS_BranchID",
                table: "APPLICATIONS",
                column: "BranchID");

            migrationBuilder.CreateIndex(
                name: "IX_APPLICATIONS_CustomerID",
                table: "APPLICATIONS",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_APPROVALS_ApplicationID",
                table: "APPROVALS",
                column: "ApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_APPROVALS_ApproverID",
                table: "APPROVALS",
                column: "ApproverID");

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOGS_UserID",
                table: "AUDIT_LOGS",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_BRANCHES_BranchCode",
                table: "BRANCHES",
                column: "BranchCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACT_STATUS_HISTORY_ChangedBy",
                table: "CONTRACT_STATUS_HISTORY",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACT_STATUS_HISTORY_ContractID",
                table: "CONTRACT_STATUS_HISTORY",
                column: "ContractID");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACT_TYPES_TypeName",
                table: "CONTRACT_TYPES",
                column: "TypeName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACTS_ApplicationID",
                table: "CONTRACTS",
                column: "ApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACTS_BranchID",
                table: "CONTRACTS",
                column: "BranchID");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACTS_ContractCode",
                table: "CONTRACTS",
                column: "ContractCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACTS_ContractTypeID",
                table: "CONTRACTS",
                column: "ContractTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRACTS_CreatedBy",
                table: "CONTRACTS",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMERS_CustomerCode",
                table: "CUSTOMERS",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENTS_ApplicationID",
                table: "DOCUMENTS",
                column: "ApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENTS_UploadedBy",
                table: "DOCUMENTS",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENTS_VerifiedBy",
                table: "DOCUMENTS",
                column: "VerifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ROLES_RoleName",
                table: "ROLES",
                column: "RoleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USERS_BranchID",
                table: "USERS",
                column: "BranchID");

            migrationBuilder.CreateIndex(
                name: "IX_USERS_RoleID",
                table: "USERS",
                column: "RoleID");

            migrationBuilder.CreateIndex(
                name: "IX_USERS_Username",
                table: "USERS",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "APPROVALS");

            migrationBuilder.DropTable(
                name: "AUDIT_LOGS");

            migrationBuilder.DropTable(
                name: "CONTRACT_STATUS_HISTORY");

            migrationBuilder.DropTable(
                name: "DOCUMENTS");

            migrationBuilder.DropTable(
                name: "CONTRACTS");

            migrationBuilder.DropTable(
                name: "APPLICATIONS");

            migrationBuilder.DropTable(
                name: "CONTRACT_TYPES");

            migrationBuilder.DropTable(
                name: "CUSTOMERS");

            migrationBuilder.DropTable(
                name: "USERS");

            migrationBuilder.DropTable(
                name: "BRANCHES");

            migrationBuilder.DropTable(
                name: "ROLES");
        }
    }
}
