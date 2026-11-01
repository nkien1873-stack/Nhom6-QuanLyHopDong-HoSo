using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Models;

namespace QLHopDongHoSo.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ContractType> ContractTypes => Set<ContractType>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractStatusHistory> ContractStatusHistories => Set<ContractStatusHistory>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CodeCounter> CodeCounters => Set<CodeCounter>();
    public DbSet<DocumentDeferral> DocumentDeferrals => Set<DocumentDeferral>();

    // Ghi thành công hồ sơ/hợp đồng/khách hàng/tài liệu... => tăng DataVersion để số liệu tổng hợp đang cache
    // được tính lại. Nhật ký và tài khoản (đăng nhập ghi 2 bảng này) không nằm trong số liệu cache nên bỏ qua.
    private bool ChangesCachedData() =>
        ChangeTracker.Entries().Any(e =>
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
            e.Entity is not AuditLog and not User);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var bump = ChangesCachedData();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (bump && result > 0)
        {
            DataVersion.Bump();
        }

        return result;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var bump = ChangesCachedData();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        if (bump && result > 0)
        {
            DataVersion.Bump();
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =====================================================
        // ROLES
        // =====================================================
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("ROLES");
            entity.HasKey(r => r.RoleId);
            entity.Property(r => r.RoleId).HasColumnName("RoleID");
            entity.Property(r => r.RoleName).HasColumnName("RoleName").HasMaxLength(50).IsRequired();
            entity.Property(r => r.Description).HasColumnName("Description").HasMaxLength(255);
            entity.HasIndex(r => r.RoleName).IsUnique();
        });

        // =====================================================
        // BRANCHES
        // =====================================================
        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("BRANCHES");
            entity.HasKey(b => b.BranchId);
            entity.Property(b => b.BranchId).HasColumnName("BranchID");
            entity.Property(b => b.BranchCode).HasColumnName("BranchCode").HasMaxLength(20).IsRequired();
            entity.Property(b => b.BranchName).HasColumnName("BranchName").HasMaxLength(150).IsRequired();
            entity.Property(b => b.Address).HasColumnName("Address").HasMaxLength(255);
            entity.Property(b => b.Phone).HasColumnName("Phone").HasMaxLength(20);
            entity.Property(b => b.Status).HasColumnName("Status").HasMaxLength(20).IsRequired();
            entity.HasIndex(b => b.BranchCode).IsUnique();
        });

        // =====================================================
        // USERS
        // =====================================================
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("USERS");
            entity.HasKey(u => u.UserId);
            entity.Property(u => u.UserId).HasColumnName("UserID");
            entity.Property(u => u.Username).HasColumnName("Username").HasMaxLength(50).IsRequired();
            entity.Property(u => u.PasswordHash).HasColumnName("PasswordHash").HasMaxLength(255).IsRequired();
            entity.Property(u => u.FullName).HasColumnName("FullName").HasMaxLength(150).IsRequired();
            entity.Property(u => u.Email).HasColumnName("Email").HasMaxLength(150);
            entity.Property(u => u.Phone).HasColumnName("Phone").HasMaxLength(20);
            entity.Property(u => u.RoleId).HasColumnName("RoleID").IsRequired();
            entity.Property(u => u.BranchId).HasColumnName("BranchID");
            entity.Property(u => u.Status).HasColumnName("Status").HasMaxLength(20).IsRequired();
            entity.Property(u => u.CreatedAt).HasColumnName("CreatedAt").IsRequired();
            entity.Property(u => u.FailedLoginCount).HasColumnName("FailedLoginCount").HasDefaultValue(0);
            entity.Property(u => u.LockoutEnd).HasColumnName("LockoutEnd");
            entity.Property(u => u.TokenVersion).HasColumnName("TokenVersion").HasDefaultValue(0);
            entity.Ignore(u => u.IsActive);

            entity.HasIndex(u => u.Username).IsUnique();

            entity.HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.Branch)
                .WithMany(b => b.Users)
                .HasForeignKey(u => u.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // CUSTOMERS
        // =====================================================
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("CUSTOMERS");
            entity.HasKey(c => c.CustomerId);
            entity.Property(c => c.CustomerId).HasColumnName("CustomerID");
            entity.Property(c => c.CustomerCode).HasColumnName("CustomerCode").HasMaxLength(30).IsRequired();
            entity.Property(c => c.CustomerType).HasColumnName("CustomerType").HasMaxLength(20).IsRequired();
            entity.Property(c => c.FullName).HasColumnName("FullName").HasMaxLength(150).IsRequired();
            entity.Property(c => c.Representative).HasColumnName("Representative").HasMaxLength(150);
            entity.Property(c => c.DateOfBirth).HasColumnName("DateOfBirth");
            entity.Property(c => c.Gender).HasColumnName("Gender").HasMaxLength(20);
            entity.Property(c => c.IdentityNumber).HasColumnName("IdentityNumber").HasMaxLength(30);
            entity.Property(c => c.Phone).HasColumnName("Phone").HasMaxLength(20);
            entity.Property(c => c.Email).HasColumnName("Email").HasMaxLength(150);
            entity.Property(c => c.Address).HasColumnName("Address").HasMaxLength(255);
            entity.Property(c => c.Status).HasColumnName("Status").HasMaxLength(20).IsRequired();
            entity.Property(c => c.CreatedAt).HasColumnName("CreatedAt").IsRequired();
            entity.Property(c => c.RowVersion).HasColumnName("RowVersion").IsRowVersion();

            entity.HasIndex(c => c.CustomerCode).IsUnique();
            // Một CCCD/MST chỉ thuộc một khách hàng (kể cả khi hai người thêm cùng lúc)
            entity.HasIndex(c => c.IdentityNumber).IsUnique().HasFilter("[IdentityNumber] IS NOT NULL");
        });

        // =====================================================
        // CONTRACT_TYPES
        // =====================================================
        modelBuilder.Entity<ContractType>(entity =>
        {
            entity.ToTable("CONTRACT_TYPES");
            entity.HasKey(ct => ct.ContractTypeId);
            entity.Property(ct => ct.ContractTypeId).HasColumnName("ContractTypeID");
            entity.Property(ct => ct.TypeName).HasColumnName("TypeName").HasMaxLength(100).IsRequired();
            entity.Property(ct => ct.Description).HasColumnName("Description").HasMaxLength(255);

            entity.HasIndex(ct => ct.TypeName).IsUnique();
        });

        // =====================================================
        // APPLICATIONS
        // =====================================================
        modelBuilder.Entity<Application>(entity =>
        {
            entity.ToTable("APPLICATIONS");
            entity.HasKey(a => a.ApplicationId);
            entity.Property(a => a.ApplicationId).HasColumnName("ApplicationID");
            entity.Property(a => a.ApplicationCode).HasColumnName("ApplicationCode").HasMaxLength(30).IsRequired();
            entity.Property(a => a.CustomerId).HasColumnName("CustomerID").IsRequired();
            entity.Property(a => a.ApplicationType).HasColumnName("ApplicationType").HasMaxLength(50).IsRequired();
            entity.Property(a => a.AssignedTo).HasColumnName("AssignedTo");
            entity.Property(a => a.BranchId).HasColumnName("BranchID").IsRequired();
            entity.Property(a => a.Status).HasColumnName("Status").HasMaxLength(30).IsRequired();
            entity.Property(a => a.Priority).HasColumnName("Priority").HasMaxLength(20);
            entity.Property(a => a.CreatedAt).HasColumnName("CreatedAt").IsRequired();
            entity.Property(a => a.SubmittedAt).HasColumnName("SubmittedAt");
            entity.Property(a => a.CompletedAt).HasColumnName("CompletedAt");

            entity.HasIndex(a => a.ApplicationCode).IsUnique();
            // "Chi nhánh của hồ sơ gần nhất" của khách hàng: tìm thẳng theo (khách hàng, ngày tạo)
            entity.HasIndex(a => new { a.CustomerId, a.CreatedAt });

            entity.HasOne(a => a.Customer)
                .WithMany(c => c.Applications)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Assignee)
                .WithMany(u => u.AssignedApplications)
                .HasForeignKey(a => a.AssignedTo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Branch)
                .WithMany(b => b.Applications)
                .HasForeignKey(a => a.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // DOCUMENTS
        // =====================================================
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("DOCUMENTS");
            entity.HasKey(d => d.DocumentId);
            entity.Property(d => d.DocumentId).HasColumnName("DocumentID");
            entity.Property(d => d.ApplicationId).HasColumnName("ApplicationID").IsRequired();
            entity.Property(d => d.DocumentType).HasColumnName("DocumentType").HasMaxLength(50).IsRequired();
            entity.Property(d => d.DocumentName).HasColumnName("DocumentName").HasMaxLength(255).IsRequired();
            entity.Property(d => d.FilePath).HasColumnName("FilePath").HasMaxLength(500).IsRequired();
            entity.Property(d => d.FileSize).HasColumnName("FileSize");
            entity.Property(d => d.FileKind).HasColumnName("FileKind").HasMaxLength(4)
                .HasComputedColumnSql(
                    "CASE WHEN [DocumentName] LIKE N'%.pdf' THEN N'PDF' " +
                    "WHEN [DocumentName] LIKE N'%.doc' OR [DocumentName] LIKE N'%.docx' THEN N'DOC' " +
                    "WHEN [DocumentName] LIKE N'%.xls' OR [DocumentName] LIKE N'%.xlsx' OR [DocumentName] LIKE N'%.csv' THEN N'XLS' " +
                    "WHEN [DocumentName] LIKE N'%.jpg' OR [DocumentName] LIKE N'%.jpeg' OR [DocumentName] LIKE N'%.png' " +
                    "OR [DocumentName] LIKE N'%.gif' OR [DocumentName] LIKE N'%.webp' THEN N'IMG' ELSE N'FILE' END",
                    stored: true);
            entity.Property(d => d.UploadedBy).HasColumnName("UploadedBy").IsRequired();
            entity.Property(d => d.UploadedAt).HasColumnName("UploadedAt").IsRequired();
            entity.Property(d => d.VerificationStatus).HasColumnName("VerificationStatus").HasMaxLength(30).IsRequired();
            entity.Property(d => d.VerifiedBy).HasColumnName("VerifiedBy");
            entity.Property(d => d.VerifiedAt).HasColumnName("VerifiedAt");
            entity.Ignore(d => d.FileName);

            entity.HasIndex(d => d.UploadedAt);
            entity.HasIndex(d => d.FileKind);

            entity.HasOne(d => d.Application)
                .WithMany(a => a.Documents)
                .HasForeignKey(d => d.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Uploader)
                .WithMany(u => u.UploadedDocuments)
                .HasForeignKey(d => d.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Verifier)
                .WithMany()
                .HasForeignKey(d => d.VerifiedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // CONTRACTS
        // =====================================================
        modelBuilder.Entity<Contract>(entity =>
        {
            entity.ToTable("CONTRACTS");
            entity.HasKey(c => c.ContractId);
            entity.Property(c => c.ContractId).HasColumnName("ContractID");
            entity.Property(c => c.ContractCode).HasColumnName("ContractCode").HasMaxLength(30).IsRequired();
            entity.Property(c => c.ApplicationId).HasColumnName("ApplicationID").IsRequired();
            entity.Property(c => c.ContractTypeId).HasColumnName("ContractTypeID").IsRequired();
            entity.Property(c => c.BranchId).HasColumnName("BranchID").IsRequired();
            entity.Property(c => c.CreatedBy).HasColumnName("CreatedBy").IsRequired();
            entity.Property(c => c.SignedDate).HasColumnName("SignedDate");
            entity.Property(c => c.EffectiveDate).HasColumnName("EffectiveDate");
            entity.Property(c => c.ExpiryDate).HasColumnName("ExpiryDate");
            entity.Property(c => c.ContractValue).HasColumnName("ContractValue").HasPrecision(18, 2);
            entity.Property(c => c.TermMonths).HasColumnName("TermMonths");
            entity.Property(c => c.InterestRate).HasColumnName("InterestRate").HasPrecision(5, 2);
            entity.Property(c => c.Purpose).HasColumnName("Purpose").HasMaxLength(500);
            entity.Property(c => c.Collateral).HasColumnName("Collateral").HasMaxLength(255);
            entity.Property(c => c.Status).HasColumnName("Status").HasMaxLength(30).IsRequired();
            entity.Property(c => c.CreatedAt).HasColumnName("CreatedAt").IsRequired();
            entity.Property(c => c.UpdatedAt).HasColumnName("UpdatedAt").IsRequired();
            entity.Property(c => c.RowVersion).HasColumnName("RowVersion").IsRowVersion();

            entity.HasIndex(c => c.ContractCode).IsUnique();
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.UpdatedAt);
            entity.HasIndex(c => c.ExpiryDate);
            // "Hồ sơ nháp của tôi" (Tổng quan) tìm thẳng theo người tạo + trạng thái
            entity.HasIndex(c => new { c.CreatedBy, c.Status });

            entity.HasOne(c => c.Application)
                .WithMany(a => a.Contracts)
                .HasForeignKey(c => c.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.ContractType)
                .WithMany(ct => ct.Contracts)
                .HasForeignKey(c => c.ContractTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Branch)
                .WithMany(b => b.Contracts)
                .HasForeignKey(c => c.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Creator)
                .WithMany(u => u.CreatedContracts)
                .HasForeignKey(c => c.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // CONTRACT_STATUS_HISTORY
        // =====================================================
        modelBuilder.Entity<ContractStatusHistory>(entity =>
        {
            entity.ToTable("CONTRACT_STATUS_HISTORY");
            entity.HasKey(h => h.HistoryId);
            entity.Property(h => h.HistoryId).HasColumnName("HistoryID");
            entity.Property(h => h.ContractId).HasColumnName("ContractID").IsRequired();
            entity.Property(h => h.OldStatus).HasColumnName("OldStatus").HasMaxLength(30);
            entity.Property(h => h.NewStatus).HasColumnName("NewStatus").HasMaxLength(30).IsRequired();
            entity.Property(h => h.ChangedBy).HasColumnName("ChangedBy").IsRequired();
            entity.Property(h => h.ChangedAt).HasColumnName("ChangedAt").IsRequired();
            entity.Property(h => h.Reason).HasColumnName("Reason").HasMaxLength(500);

            // Màn Phê duyệt "đã xử lý" lọc theo trạng thái mới, mới nhất trước
            entity.HasIndex(h => new { h.NewStatus, h.ChangedAt });

            entity.HasOne(h => h.Contract)
                .WithMany(c => c.StatusHistories)
                .HasForeignKey(h => h.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.Changer)
                .WithMany()
                .HasForeignKey(h => h.ChangedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // APPROVALS
        // =====================================================
        modelBuilder.Entity<Approval>(entity =>
        {
            entity.ToTable("APPROVALS");
            entity.HasKey(a => a.ApprovalId);
            entity.Property(a => a.ApprovalId).HasColumnName("ApprovalID");
            entity.Property(a => a.ApplicationId).HasColumnName("ApplicationID").IsRequired();
            entity.Property(a => a.ApproverId).HasColumnName("ApproverID").IsRequired();
            entity.Property(a => a.Status).HasColumnName("Status").HasMaxLength(30).IsRequired();
            entity.Property(a => a.Comment).HasColumnName("Comment").HasMaxLength(500);
            entity.Property(a => a.ApprovedAt).HasColumnName("ApprovedAt");

            entity.HasOne(a => a.Application)
                .WithMany(app => app.Approvals)
                .HasForeignKey(a => a.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Approver)
                .WithMany(u => u.Approvals)
                .HasForeignKey(a => a.ApproverId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // AUDIT_LOGS
        // =====================================================
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AUDIT_LOGS");
            entity.HasKey(l => l.LogId);
            entity.Property(l => l.LogId).HasColumnName("LogID");
            entity.Property(l => l.UserId).HasColumnName("UserID");
            entity.Property(l => l.Action).HasColumnName("Action").HasMaxLength(50).IsRequired();
            entity.Property(l => l.EntityType).HasColumnName("EntityType").HasMaxLength(50).IsRequired();
            entity.Property(l => l.EntityId).HasColumnName("EntityID").HasMaxLength(50).IsRequired();
            entity.Property(l => l.Description).HasColumnName("Description").HasMaxLength(1000);
            entity.Property(l => l.CreatedAt).HasColumnName("CreatedAt").IsRequired();
            entity.Property(l => l.IPAddress).HasColumnName("IPAddress").HasMaxLength(45);

            // Nhật ký luôn đọc mới nhất trước => index giảm dần, không phải quét + sắp xếp cả bảng
            entity.HasIndex(l => new { l.CreatedAt, l.LogId }).IsDescending(true, true);

            entity.HasOne(l => l.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // CODE_COUNTERS
        // =====================================================
        modelBuilder.Entity<CodeCounter>(entity =>
        {
            entity.ToTable("CODE_COUNTERS");
            entity.HasKey(c => new { c.Prefix, c.Year });
            entity.Property(c => c.Prefix).HasColumnName("Prefix").HasMaxLength(10);
            entity.Property(c => c.Year).HasColumnName("Year");
            entity.Property(c => c.LastValue).HasColumnName("LastValue");
        });

        // =====================================================
        // DOCUMENT_DEFERRALS (Tờ trình xin nợ chứng từ)
        // =====================================================
        modelBuilder.Entity<DocumentDeferral>(entity =>
        {
            entity.ToTable("DOCUMENT_DEFERRALS");
            entity.HasKey(d => d.DeferralId);
            entity.Property(d => d.DeferralId).HasColumnName("DeferralID");
            entity.Property(d => d.ContractId).HasColumnName("ContractID").IsRequired();
            entity.Property(d => d.DocumentType).HasColumnName("DocumentType").HasMaxLength(50).IsRequired();
            entity.Property(d => d.DueDate).HasColumnName("DueDate").HasColumnType("date").IsRequired();
            entity.Property(d => d.Reason).HasColumnName("Reason").HasMaxLength(500).IsRequired();
            entity.Property(d => d.Status).HasColumnName("Status").HasMaxLength(20).IsRequired();
            entity.Property(d => d.RequestedBy).HasColumnName("RequestedBy").IsRequired();
            entity.Property(d => d.RequestedAt).HasColumnName("RequestedAt").IsRequired();
            entity.Property(d => d.DecidedBy).HasColumnName("DecidedBy");
            entity.Property(d => d.DecidedAt).HasColumnName("DecidedAt");
            entity.Property(d => d.DecisionNote).HasColumnName("DecisionNote").HasMaxLength(500);

            // Mỗi loại tài liệu của một hợp đồng chỉ có tối đa 1 tờ trình đang chờ hoặc đã duyệt
            // (hai người bấm gửi cùng lúc => vi phạm unique => 409)
            entity.HasIndex(d => new { d.ContractId, d.DocumentType })
                .IsUnique()
                .HasFilter("[Status] IN ('PENDING', 'APPROVED')");

            // Màn duyệt ngoại lệ: lọc theo trạng thái, mới nhất trước
            entity.HasIndex(d => new { d.Status, d.RequestedAt });

            entity.HasOne(d => d.Contract)
                .WithMany()
                .HasForeignKey(d => d.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Requester)
                .WithMany()
                .HasForeignKey(d => d.RequestedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Decider)
                .WithMany()
                .HasForeignKey(d => d.DecidedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}