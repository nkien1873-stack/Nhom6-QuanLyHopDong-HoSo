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
            entity.Property(c => c.DateOfBirth).HasColumnName("DateOfBirth");
            entity.Property(c => c.Gender).HasColumnName("Gender").HasMaxLength(20);
            entity.Property(c => c.IdentityNumber).HasColumnName("IdentityNumber").HasMaxLength(30);
            entity.Property(c => c.Phone).HasColumnName("Phone").HasMaxLength(20);
            entity.Property(c => c.Email).HasColumnName("Email").HasMaxLength(150);
            entity.Property(c => c.Address).HasColumnName("Address").HasMaxLength(255);
            entity.Property(c => c.Status).HasColumnName("Status").HasMaxLength(20).IsRequired();
            entity.Property(c => c.CreatedAt).HasColumnName("CreatedAt").IsRequired();

            entity.HasIndex(c => c.CustomerCode).IsUnique();
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
            entity.Property(d => d.UploadedBy).HasColumnName("UploadedBy").IsRequired();
            entity.Property(d => d.UploadedAt).HasColumnName("UploadedAt").IsRequired();
            entity.Property(d => d.VerificationStatus).HasColumnName("VerificationStatus").HasMaxLength(30).IsRequired();
            entity.Property(d => d.VerifiedBy).HasColumnName("VerifiedBy");
            entity.Property(d => d.VerifiedAt).HasColumnName("VerifiedAt");
            entity.Ignore(d => d.FileName);

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
            entity.Property(c => c.Status).HasColumnName("Status").HasMaxLength(30).IsRequired();
            entity.Property(c => c.CreatedAt).HasColumnName("CreatedAt").IsRequired();

            entity.HasIndex(c => c.ContractCode).IsUnique();

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

            entity.HasOne(l => l.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}