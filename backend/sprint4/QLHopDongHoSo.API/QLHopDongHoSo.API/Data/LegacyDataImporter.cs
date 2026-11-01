using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Models;
using QLHopDongHoSo.API.Services;

namespace QLHopDongHoSo.API.Data;

// Chuyển dữ liệu đã nhập ở DB Sprint 2–3 (schema cũ: Users/Contracts/Documents)
// sang schema mới. Chạy được nhiều lần: đã có HD001 thì bỏ qua.
public static class LegacyDataImporter
{
    // Thư mục chứa file PDF thật đã upload ở Sprint 3 (copy từ Storage/Documents cũ)
    private const string LegacyFilesFolder = "Data/SeedFiles/Legacy";

    // Ghi chú cho người đọc: dữ liệu cũ không có khách hàng, chi nhánh, hồ sơ, giá trị.
    // Gán vào khách hàng KH2026002 (công ty công nghệ) và Chi nhánh Hà Nội.
    private const string LegacyCustomerCode = "KH2026002";
    private const string LegacyBranchCode = "CN-HN01";

    private record LegacyUser(string Username, string FullName, string Email, string Role, string PasswordHash, DateTime CreatedAt);

    private record LegacyContract(
        string Code, string Name, string Description, DateTime Start, DateTime End,
        string CreatedBy, DateTime CreatedAt, string TypeName);

    private record LegacyDocument(
        string ContractCode, string FileName, string DocumentType, string? SourceFile,
        string UploadedBy, DateTime UploadedAt);

    // Mật khẩu giữ nguyên hash BCrypt cũ => đăng nhập bằng mật khẩu đã đặt ở Sprint 2–3.
    // Tài khoản admin cũ trùng username với admin demo nên dùng admin demo.
    private static readonly LegacyUser[] Users =
    [
        new("nhanvien01", "Nguyễn Văn A", "nhanvien01@example.com", AppRoles.Officer,
            "$2a$11$ZHIQOQRRFtEmWWOJtmBoMeqL4nlTIfZnZAV4aO35nk3v1PLTIqbla", new DateTime(2026, 10, 1, 21, 9, 53)),
        new("quanly01", "Trần Văn B", "quanly01@example.com", AppRoles.Manager,
            "$2a$11$ZHIQOQRRFtEmWWOJtmBoMeqL4nlTIfZnZAV4aO35nk3v1PLTIqbla", new DateTime(2026, 10, 1, 21, 9, 53))
    ];

    private const string TypeIt = "Hợp đồng cung cấp dịch vụ CNTT";
    private const string TypeMaintenance = "Hợp đồng bảo trì hệ thống";

    private static readonly LegacyContract[] Contracts =
    [
        new("HD001", "Hợp đồng cung cấp dịch vụ CNTT", "Hợp đồng dịch vụ công nghệ thông tin",
            new DateTime(2026, 9, 1), new DateTime(2027, 9, 1), "admin", new DateTime(2026, 10, 1, 21, 9, 53), TypeIt),
        new("HD002", "Hợp đồng bảo trì hệ thống", "Hợp đồng bảo trì hệ thống phần mềm",
            new DateTime(2026, 9, 15), new DateTime(2027, 9, 15), "nhanvien01", new DateTime(2026, 10, 1, 21, 9, 53), TypeMaintenance),
        new("HD004", "Hợp đồng kiểm thử Task 3.7", "Test validation trạng thái",
            new DateTime(2026, 10, 2), new DateTime(2027, 10, 2), "admin", new DateTime(2026, 10, 2, 16, 36, 35), TypeIt)
    ];

    // SourceFile = null: DB cũ chỉ có metadata, không có file => tạo PDF minh họa
    private static readonly LegacyDocument[] Documents =
    [
        new("HD001", "hopdong_HD001.pdf", "CONTRACT_SIGNED", null, "admin", new DateTime(2026, 10, 1, 21, 9, 53)),
        new("HD001", "phuluc_HD001.pdf", "ATTACHMENT", null, "nhanvien01", new DateTime(2026, 10, 1, 21, 9, 53)),
        new("HD002", "hopdong_HD002.pdf", "CONTRACT_SIGNED", null, "quanly01", new DateTime(2026, 10, 1, 21, 9, 53)),
        new("HD001", "test_hopdong_hople.pdf", "ATTACHMENT", "bed0078b1d8d4e0dbdbc65b266921076.pdf", "admin", new DateTime(2026, 10, 7, 11, 46, 10)),
        new("HD001", "test_hopdong_hople.pdf", "ATTACHMENT", "13afb744efed4eae9b65032d42e2e746.pdf", "admin", new DateTime(2026, 10, 7, 12, 32, 50)),
        new("HD001", "test_hopdong_hople.pdf", "ATTACHMENT", "2f42b30c1eea493a97d1c9da05e5ddad.pdf", "admin", new DateTime(2026, 10, 7, 13, 17, 6))
    ];

    // Trả về true nếu lần này có chuyển dữ liệu (để bước điền dữ liệu demo chỉ chạy cho dữ liệu vừa tạo)
    public static async Task<bool> ImportAsync(
        ApplicationDbContext context,
        string contentRootPath,
        string storagePath,
        Func<string, byte[]> buildPlaceholderPdf)
    {
        if (await context.Contracts.AnyAsync(c => c.ContractCode == "HD001"))
        {
            return false;
        }

        var branch = await context.Branches.FirstOrDefaultAsync(b => b.BranchCode == LegacyBranchCode);
        var customer = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == LegacyCustomerCode);
        if (branch == null || customer == null)
        {
            // Thiếu dữ liệu demo nền thì không chuyển được
            return false;
        }

        // ---------- Tài khoản ----------
        var roleIds = await context.Roles.ToDictionaryAsync(r => r.RoleName, r => r.RoleId);
        foreach (var u in Users)
        {
            if (await context.Users.AnyAsync(x => x.Username == u.Username)) continue;

            context.Users.Add(new User
            {
                Username = u.Username,
                PasswordHash = u.PasswordHash,
                FullName = u.FullName,
                Email = u.Email,
                RoleId = roleIds[u.Role],
                BranchId = branch.BranchId,
                Status = "ACTIVE",
                CreatedAt = u.CreatedAt
            });
        }
        await context.SaveChangesAsync();

        var usernames = Users.Select(u => u.Username).Append("admin").ToList();
        var userIds = await context.Users
            .Where(x => usernames.Contains(x.Username))
            .ToDictionaryAsync(x => x.Username, x => x.UserId);

        // ---------- Loại hợp đồng ----------
        var typeDescriptions = new Dictionary<string, string>
        {
            [TypeIt] = "Dịch vụ phần mềm, triển khai hệ thống CNTT",
            [TypeMaintenance] = "Bảo trì, hỗ trợ vận hành hệ thống phần mềm"
        };
        foreach (var (name, description) in typeDescriptions)
        {
            if (!await context.ContractTypes.AnyAsync(t => t.TypeName == name))
            {
                context.ContractTypes.Add(new ContractType { TypeName = name, Description = description });
            }
        }
        await context.SaveChangesAsync();
        var typeIds = await context.ContractTypes.ToDictionaryAsync(t => t.TypeName, t => t.ContractTypeId);

        // ---------- Hồ sơ + hợp đồng ----------
        var applicationIds = new Dictionary<string, int>();
        foreach (var c in Contracts)
        {
            var creatorId = userIds[c.CreatedBy];

            var application = new Application
            {
                ApplicationCode = $"HS-2026-{c.Code}",
                CustomerId = customer.CustomerId,
                ApplicationType = c.Name, // giữ lại tên hợp đồng cũ (schema mới không còn cột ContractName)
                AssignedTo = creatorId,
                BranchId = branch.BranchId,
                Status = "APPROVED",
                Priority = "NORMAL",
                CreatedAt = c.CreatedAt,
                SubmittedAt = c.CreatedAt,
                CompletedAt = c.CreatedAt
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();
            applicationIds[c.Code] = application.ApplicationId;

            // "Đang hiệu lực" ở DB cũ = ACTIVE; DB cũ không có ngày ký và giá trị nên để trống
            var contract = new Contract
            {
                ContractCode = c.Code,
                ApplicationId = application.ApplicationId,
                ContractTypeId = typeIds[c.TypeName],
                BranchId = branch.BranchId,
                CreatedBy = creatorId,
                EffectiveDate = c.Start,
                ExpiryDate = c.End,
                Status = ContractStatuses.Active,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.CreatedAt
            };
            context.Contracts.Add(contract);
            await context.SaveChangesAsync();

            context.ContractStatusHistories.Add(new ContractStatusHistory
            {
                ContractId = contract.ContractId,
                OldStatus = null,
                NewStatus = ContractStatuses.Active,
                ChangedBy = creatorId,
                ChangedAt = c.CreatedAt,
                Reason = $"Chuyển từ DB Sprint 2–3. {c.Description}"
            });
        }
        await context.SaveChangesAsync();

        // ---------- Tài liệu ----------
        var fullStoragePath = Path.Combine(contentRootPath, storagePath);
        Directory.CreateDirectory(fullStoragePath);

        var index = 0;
        foreach (var d in Documents)
        {
            index++;
            var storedName = $"legacy_{d.ContractCode}_{index}.pdf";
            var target = Path.Combine(fullStoragePath, storedName);
            var source = d.SourceFile == null
                ? null
                : Path.Combine(contentRootPath, LegacyFilesFolder, d.SourceFile);

            if (source != null && File.Exists(source))
            {
                File.Copy(source, target, overwrite: true);
            }
            else
            {
                await File.WriteAllBytesAsync(target, buildPlaceholderPdf($"{d.ContractCode} - {d.FileName}"));
            }

            context.Documents.Add(new Document
            {
                ApplicationId = applicationIds[d.ContractCode],
                DocumentType = d.DocumentType,
                DocumentName = d.FileName,
                FilePath = $"{storagePath}/{storedName}".Replace("\\", "/"),
                FileSize = new FileInfo(target).Length,
                UploadedBy = userIds[d.UploadedBy],
                UploadedAt = d.UploadedAt,
                VerificationStatus = "PENDING"
            });
        }
        await context.SaveChangesAsync();
        return true;
    }
}
