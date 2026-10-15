/* =====================================================================
   DATABASE: ContractManagement
   Project: Quản lý Hợp đồng & Hồ sơ
   Schema Version: 2.0 (11 Bảng đầy đủ theo đặc tả)
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Tạo Database nếu chưa tồn tại
IF DB_ID('ContractManagement') IS NULL
BEGIN
    CREATE DATABASE ContractManagement COLLATE Vietnamese_CI_AS;
END
GO

USE ContractManagement;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 2. Xóa các khóa ngoại & bảng cũ nếu tồn tại (hỗ trợ chạy lại script an toàn)
DECLARE @dropFkSql NVARCHAR(MAX) = N'';
SELECT @dropFkSql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + 
                     N' DROP CONSTRAINT ' + QUOTENAME(f.name) + N';' + CHAR(13)
FROM sys.foreign_keys f
JOIN sys.tables t ON f.parent_object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id;
IF @dropFkSql <> N'' EXEC sp_executesql @dropFkSql;

DECLARE @dropTableSql NVARCHAR(MAX) = N'';
SELECT @dropTableSql += N'DROP TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';' + CHAR(13)
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name <> '__EFMigrationsHistory';
IF @dropTableSql <> N'' EXEC sp_executesql @dropTableSql;
GO

/* =====================================================================
   3. BẢNG ROLES (Vai trò)
   ===================================================================== */
CREATE TABLE ROLES
(
    RoleID INT IDENTITY(1,1) NOT NULL,
    RoleName VARCHAR(50) NOT NULL,
    Description NVARCHAR(255) NULL,

    CONSTRAINT PK_ROLES PRIMARY KEY (RoleID),
    CONSTRAINT UQ_ROLES_RoleName UNIQUE (RoleName)
);
GO

/* =====================================================================
   4. BẢNG BRANCHES (Chi nhánh)
   ===================================================================== */
CREATE TABLE BRANCHES
(
    BranchID INT IDENTITY(1,1) NOT NULL,
    BranchCode VARCHAR(20) NOT NULL,
    BranchName NVARCHAR(150) NOT NULL,
    Address NVARCHAR(255) NULL,
    Phone VARCHAR(20) NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',

    CONSTRAINT PK_BRANCHES PRIMARY KEY (BranchID),
    CONSTRAINT UQ_BRANCHES_BranchCode UNIQUE (BranchCode)
);
GO

/* =====================================================================
   5. BẢNG USERS (Tài khoản người dùng)
   ===================================================================== */
CREATE TABLE USERS
(
    UserID INT IDENTITY(1,1) NOT NULL,
    Username VARCHAR(50) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    FullName NVARCHAR(150) NOT NULL,
    Email VARCHAR(150) NULL,
    Phone VARCHAR(20) NULL,
    RoleID INT NOT NULL,
    BranchID INT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),

    CONSTRAINT PK_USERS PRIMARY KEY (UserID),
    CONSTRAINT UQ_USERS_Username UNIQUE (Username),
    CONSTRAINT FK_USERS_ROLES FOREIGN KEY (RoleID) REFERENCES ROLES(RoleID),
    CONSTRAINT FK_USERS_BRANCHES FOREIGN KEY (BranchID) REFERENCES BRANCHES(BranchID)
);
GO

/* =====================================================================
   6. BẢNG CUSTOMERS (Khách hàng)
   ===================================================================== */
CREATE TABLE CUSTOMERS
(
    CustomerID INT IDENTITY(1,1) NOT NULL,
    CustomerCode VARCHAR(30) NOT NULL,
    CustomerType VARCHAR(20) NOT NULL, -- INDIVIDUAL / CORPORATE
    FullName NVARCHAR(150) NOT NULL,
    DateOfBirth DATE NULL,
    Gender NVARCHAR(20) NULL,
    IdentityNumber VARCHAR(30) NULL,
    Phone VARCHAR(20) NULL,
    Email VARCHAR(150) NULL,
    Address NVARCHAR(255) NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),

    CONSTRAINT PK_CUSTOMERS PRIMARY KEY (CustomerID),
    CONSTRAINT UQ_CUSTOMERS_CustomerCode UNIQUE (CustomerCode)
);
GO

CREATE NONCLUSTERED INDEX IX_CUSTOMERS_IdentityNumber 
ON CUSTOMERS(IdentityNumber) 
WHERE IdentityNumber IS NOT NULL;
GO

/* =====================================================================
   7. BẢNG CONTRACT_TYPES (Loại hợp đồng)
   ===================================================================== */
CREATE TABLE CONTRACT_TYPES
(
    ContractTypeID INT IDENTITY(1,1) NOT NULL,
    TypeName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(255) NULL,

    CONSTRAINT PK_CONTRACT_TYPES PRIMARY KEY (ContractTypeID),
    CONSTRAINT UQ_CONTRACT_TYPES_TypeName UNIQUE (TypeName)
);
GO

/* =====================================================================
   8. BẢNG APPLICATIONS (Hồ sơ)
   ===================================================================== */
CREATE TABLE APPLICATIONS
(
    ApplicationID INT IDENTITY(1,1) NOT NULL,
    ApplicationCode VARCHAR(30) NOT NULL,
    CustomerID INT NOT NULL,
    ApplicationType NVARCHAR(50) NOT NULL,
    AssignedTo INT NULL,
    BranchID INT NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'DRAFT',
    Priority VARCHAR(20) NULL DEFAULT 'NORMAL',
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    SubmittedAt DATETIME NULL,
    CompletedAt DATETIME NULL,

    CONSTRAINT PK_APPLICATIONS PRIMARY KEY (ApplicationID),
    CONSTRAINT UQ_APPLICATIONS_ApplicationCode UNIQUE (ApplicationCode),
    CONSTRAINT FK_APPLICATIONS_CUSTOMERS FOREIGN KEY (CustomerID) REFERENCES CUSTOMERS(CustomerID),
    CONSTRAINT FK_APPLICATIONS_USERS FOREIGN KEY (AssignedTo) REFERENCES USERS(UserID),
    CONSTRAINT FK_APPLICATIONS_BRANCHES FOREIGN KEY (BranchID) REFERENCES BRANCHES(BranchID)
);
GO

/* =====================================================================
   9. BẢNG DOCUMENTS (Tài liệu đính kèm hồ sơ)
   ===================================================================== */
CREATE TABLE DOCUMENTS
(
    DocumentID INT IDENTITY(1,1) NOT NULL,
    ApplicationID INT NOT NULL,
    DocumentType VARCHAR(50) NOT NULL,
    DocumentName NVARCHAR(255) NOT NULL,
    FilePath NVARCHAR(500) NOT NULL,
    UploadedBy INT NOT NULL,
    UploadedAt DATETIME NOT NULL DEFAULT GETDATE(),
    VerificationStatus VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    VerifiedBy INT NULL,
    VerifiedAt DATETIME NULL,

    CONSTRAINT PK_DOCUMENTS PRIMARY KEY (DocumentID),
    CONSTRAINT FK_DOCUMENTS_APPLICATIONS FOREIGN KEY (ApplicationID) REFERENCES APPLICATIONS(ApplicationID) ON DELETE CASCADE,
    CONSTRAINT FK_DOCUMENTS_USERS_UploadedBy FOREIGN KEY (UploadedBy) REFERENCES USERS(UserID),
    CONSTRAINT FK_DOCUMENTS_USERS_VerifiedBy FOREIGN KEY (VerifiedBy) REFERENCES USERS(UserID)
);
GO

/* =====================================================================
   10. BẢNG CONTRACTS (Hợp đồng)
   ===================================================================== */
CREATE TABLE CONTRACTS
(
    ContractID INT IDENTITY(1,1) NOT NULL,
    ContractCode VARCHAR(30) NOT NULL,
    ApplicationID INT NOT NULL,
    ContractTypeID INT NOT NULL,
    BranchID INT NOT NULL,
    CreatedBy INT NOT NULL,
    SignedDate DATE NULL,
    EffectiveDate DATE NULL,
    ExpiryDate DATE NULL,
    ContractValue DECIMAL(18,2) NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'DRAFT',
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),

    CONSTRAINT PK_CONTRACTS PRIMARY KEY (ContractID),
    CONSTRAINT UQ_CONTRACTS_ContractCode UNIQUE (ContractCode),
    CONSTRAINT FK_CONTRACTS_APPLICATIONS FOREIGN KEY (ApplicationID) REFERENCES APPLICATIONS(ApplicationID),
    CONSTRAINT FK_CONTRACTS_CONTRACT_TYPES FOREIGN KEY (ContractTypeID) REFERENCES CONTRACT_TYPES(ContractTypeID),
    CONSTRAINT FK_CONTRACTS_BRANCHES FOREIGN KEY (BranchID) REFERENCES BRANCHES(BranchID),
    CONSTRAINT FK_CONTRACTS_USERS FOREIGN KEY (CreatedBy) REFERENCES USERS(UserID)
);
GO

/* =====================================================================
   11. BẢNG CONTRACT_STATUS_HISTORY (Lịch sử trạng thái hợp đồng)
   ===================================================================== */
CREATE TABLE CONTRACT_STATUS_HISTORY
(
    HistoryID INT IDENTITY(1,1) NOT NULL,
    ContractID INT NOT NULL,
    OldStatus VARCHAR(30) NULL,
    NewStatus VARCHAR(30) NOT NULL,
    ChangedBy INT NOT NULL,
    ChangedAt DATETIME NOT NULL DEFAULT GETDATE(),
    Reason NVARCHAR(500) NULL,

    CONSTRAINT PK_CONTRACT_STATUS_HISTORY PRIMARY KEY (HistoryID),
    CONSTRAINT FK_CONTRACT_STATUS_HISTORY_CONTRACTS FOREIGN KEY (ContractID) REFERENCES CONTRACTS(ContractID) ON DELETE CASCADE,
    CONSTRAINT FK_CONTRACT_STATUS_HISTORY_USERS FOREIGN KEY (ChangedBy) REFERENCES USERS(UserID)
);
GO

/* =====================================================================
   12. BẢNG APPROVALS (Lịch sử / Quyết định phê duyệt)
   ===================================================================== */
CREATE TABLE APPROVALS
(
    ApprovalID INT IDENTITY(1,1) NOT NULL,
    ApplicationID INT NOT NULL,
    ApproverID INT NOT NULL,
    Status VARCHAR(30) NOT NULL,
    Comment NVARCHAR(500) NULL,
    ApprovedAt DATETIME NULL,

    CONSTRAINT PK_APPROVALS PRIMARY KEY (ApprovalID),
    CONSTRAINT FK_APPROVALS_APPLICATIONS FOREIGN KEY (ApplicationID) REFERENCES APPLICATIONS(ApplicationID) ON DELETE CASCADE,
    CONSTRAINT FK_APPROVALS_USERS FOREIGN KEY (ApproverID) REFERENCES USERS(UserID)
);
GO

/* =====================================================================
   13. BẢNG AUDIT_LOGS (Nhật ký thao tác)
   ===================================================================== */
CREATE TABLE AUDIT_LOGS
(
    LogID BIGINT IDENTITY(1,1) NOT NULL,
    UserID INT NULL,
    Action VARCHAR(50) NOT NULL,
    EntityType VARCHAR(50) NOT NULL,
    EntityID VARCHAR(50) NOT NULL,
    Description NVARCHAR(1000) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    IPAddress VARCHAR(45) NULL,

    CONSTRAINT PK_AUDIT_LOGS PRIMARY KEY (LogID),
    CONSTRAINT FK_AUDIT_LOGS_USERS FOREIGN KEY (UserID) REFERENCES USERS(UserID)
);
GO

/* =====================================================================
   14. DỮ LIỆU KHỞI TẠO MẪU (SEED DATA)
   ===================================================================== */

-- 14.1. ROLES
INSERT INTO ROLES (RoleName, Description) VALUES
('Admin', N'Quản trị viên toàn hệ thống'),
('Manager', N'Quản lý chi nhánh / người phê duyệt'),
('Officer', N'Nhân viên xử lý hồ sơ & tạo hợp đồng'),
('Auditor', N'Kiểm toán viên tra cứu nhật ký và dữ liệu');

-- 14.2. BRANCHES
INSERT INTO BRANCHES (BranchCode, BranchName, Address, Phone, Status) VALUES
('CN-HN01', N'Chi nhánh Hà Nội', N'123 Phố Huế, Q. Hai Bà Trưng, Hà Nội', '02431234567', 'ACTIVE'),
('CN-HCM01', N'Chi nhánh TP. Hồ Chí Minh', N'456 Lê Duẩn, Q. 1, TP. Hồ Chí Minh', '02839876543', 'ACTIVE'),
('CN-DN01', N'Chi nhánh Đà Nẵng', N'789 Nguyễn Văn Linh, Q. Hải Châu, TP. Đà Nẵng', '02363555888', 'ACTIVE');

-- 14.3. USERS (Mật khẩu mặc định: 123456)
INSERT INTO USERS (Username, PasswordHash, FullName, Email, Phone, RoleID, BranchID, Status) VALUES
('admin', '$2a$11$z9f51I3f4LXcOYeq9Defyu1bp.jdhhZiCUmE0IJtnmX3g3NDLTDUC', N'Quản Trị Viên Hệ Thống', 'admin@qlhdhs.vn', '0901000001', 1, 1, 'ACTIVE'),
('manager_hn', '$2a$11$z9f51I3f4LXcOYeq9Defyu1bp.jdhhZiCUmE0IJtnmX3g3NDLTDUC', N'Nguyễn Quản Lý HN', 'manager.hn@qlhdhs.vn', '0902000002', 2, 1, 'ACTIVE'),
('officer_hn01', '$2a$11$z9f51I3f4LXcOYeq9Defyu1bp.jdhhZiCUmE0IJtnmX3g3NDLTDUC', N'Trần Nhân Viên', 'officer1.hn@qlhdhs.vn', '0903000003', 3, 1, 'ACTIVE'),
('officer_hcm01', '$2a$11$z9f51I3f4LXcOYeq9Defyu1bp.jdhhZiCUmE0IJtnmX3g3NDLTDUC', N'Lê Chuyên Viên HCM', 'officer1.hcm@qlhdhs.vn', '0904000004', 3, 2, 'ACTIVE');

-- 14.4. CUSTOMERS
INSERT INTO CUSTOMERS (CustomerCode, CustomerType, FullName, DateOfBirth, Gender, IdentityNumber, Phone, Email, Address, Status) VALUES
('KH2026001', 'INDIVIDUAL', N'Phạm Nhật Minh', '1990-05-15', N'Nam', '001090012345', '0912345678', 'minh.pn@gmail.com', N'Số 15 ngõ 20 Cầu Giấy, Hà Nội', 'ACTIVE'),
('KH2026002', 'CORPORATE', N'Công ty Cổ phần Công nghệ Toàn Cầu', NULL, NULL, '0102345678', '0243888999', 'contact@globallink.vn', N'Tầng 8 Tòa nhà Vincom, Q. 1, TP. HCM', 'ACTIVE');

-- 14.5. CONTRACT_TYPES
INSERT INTO CONTRACT_TYPES (TypeName, Description) VALUES
(N'Hợp đồng Cung cấp Dịch vụ CNTT', N'Dịch vụ phần mềm, triển khai hệ thống'),
(N'Hợp đồng Mua bán Hàng hóa', N'Cung ứng trang thiết bị vật tư'),
(N'Hợp đồng Thuê văn phòng', N'Thuê mặt bằng và cơ sở hạ tầng'),
(N'Hợp đồng Lao động', N'Hợp đồng nhân sự chính thức');

-- 14.6. APPLICATIONS
INSERT INTO APPLICATIONS (ApplicationCode, CustomerID, ApplicationType, AssignedTo, BranchID, Status, Priority, CreatedAt, SubmittedAt) VALUES
('HS-2026-0001', 1, N'Đăng ký dịch vụ điện toán đám mây', 3, 1, 'SUBMITTED', 'HIGH', DATEADD(DAY, -5, GETDATE()), DATEADD(DAY, -4, GETDATE())),
('HS-2026-0002', 2, N'Hồ sơ đấu thầu cung cấp thiết bị', 4, 2, 'APPROVED', 'NORMAL', DATEADD(DAY, -10, GETDATE()), DATEADD(DAY, -8, GETDATE()));

-- 14.7. DOCUMENTS
INSERT INTO DOCUMENTS (ApplicationID, DocumentType, DocumentName, FilePath, UploadedBy, VerificationStatus, VerifiedBy, VerifiedAt) VALUES
(1, 'CCCD', N'CCCD_PhamNhatMinh.pdf', '/storage/documents/2026/04/cccd_001.pdf', 3, 'VERIFIED', 2, DATEADD(DAY, -3, GETDATE())),
(1, 'APPLICATION_FORM', N'DonDangKyDichVu.pdf', '/storage/documents/2026/04/don_dk_001.pdf', 3, 'VERIFIED', 2, DATEADD(DAY, -3, GETDATE())),
(2, 'BUSINESS_LICENSE', N'GiayPhepKinhDoanh_ToanCau.pdf', '/storage/documents/2026/04/gpkd_002.pdf', 4, 'VERIFIED', 2, DATEADD(DAY, -7, GETDATE()));

-- 14.8. CONTRACTS
INSERT INTO CONTRACTS (ContractCode, ApplicationID, ContractTypeID, BranchID, CreatedBy, SignedDate, EffectiveDate, ExpiryDate, ContractValue, Status) VALUES
('HD-2026-0001', 2, 1, 2, 4, '2026-04-01', '2026-04-05', '2027-04-05', 150000000.00, 'ACTIVE');

-- 14.9. CONTRACT_STATUS_HISTORY
INSERT INTO CONTRACT_STATUS_HISTORY (ContractID, OldStatus, NewStatus, ChangedBy, ChangedAt, Reason) VALUES
(1, 'DRAFT', 'ACTIVE', 2, GETDATE(), N'Hợp đồng đã được ký kết và nghiệm thu điều khoản');

-- 14.10. APPROVALS
INSERT INTO APPROVALS (ApplicationID, ApproverID, Status, Comment, ApprovedAt) VALUES
(1, 2, 'PENDING', N'Đang thẩm định hồ sơ khách hàng cá nhân', NULL),
(2, 2, 'APPROVED', N'Hồ sơ doanh nghiệp đầy đủ giấy phép kinh doanh hợp lệ', DATEADD(DAY, -7, GETDATE()));

-- 14.11. AUDIT_LOGS
INSERT INTO AUDIT_LOGS (UserID, Action, EntityType, EntityID, Description, IPAddress) VALUES
(1, 'CREATE', 'BRANCHES', '1', N'Khởi tạo chi nhánh Hà Nội', '127.0.0.1'),
(3, 'SUBMIT', 'APPLICATIONS', '1', N'Nộp hồ sơ khách hàng Phạm Nhật Minh', '192.168.1.15'),
(2, 'APPROVE', 'APPLICATIONS', '2', N'Phê duyệt hồ sơ đấu thầu cung cấp thiết bị', '192.168.1.20');
GO