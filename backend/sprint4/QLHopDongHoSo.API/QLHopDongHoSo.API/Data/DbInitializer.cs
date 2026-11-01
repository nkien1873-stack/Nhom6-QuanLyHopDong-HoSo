using System.Text;
using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Models;
using QLHopDongHoSo.API.Services;

namespace QLHopDongHoSo.API.Data;

public static class DbInitializer
{
    // Mật khẩu mặc định của các tài khoản demo
    public const string DemoPassword = "Demo@123";

    public static async Task InitializeAsync(
        ApplicationDbContext context,
        string contentRootPath,
        string storagePath)
    {
        await EnsureNotForeignDatabaseAsync(context);

        // Tạo DB / áp dụng migration còn thiếu
        await context.Database.MigrateAsync();

        await SeedRolesAsync(context);

        // Nếu đã có User thì không tạo lại dữ liệu demo
        var seeded = false;
        if (!await context.Users.AnyAsync())
        {
            await SeedDemoDataAsync(context, contentRootPath, storagePath);
            seeded = true;
        }

        // Dữ liệu nhập tay ở DB Sprint 2–3 (HD001, HD002, HD004...), tự bỏ qua nếu đã có
        var imported = await LegacyDataImporter.ImportAsync(context, contentRootPath, storagePath, BuildDemoPdf);

        // Điền mục đích/kỳ hạn/... mẫu CHỈ cho dữ liệu demo vừa tạo trong lần khởi động này.
        // Không bao giờ chạm vào hồ sơ người dùng đã tạo (ô để trống cũng là dữ liệu của họ).
        await DemoDataUpgrade.ApplyAsync(context, contentRootPath, fillDemoDetails: seeded || imported);

        // N09: tài khoản cấp ngoại lệ + bổ sung tài liệu bắt buộc cho hồ sơ demo đã trình/duyệt (chạy một lần)
        await DemoDataUpgrade.ApplyDocumentRulesAsync(context, contentRootPath, storagePath, () => PasswordHasher.Hash(DemoPassword));
    }

    // DB cùng tên nhưng có bảng mà không có lịch sử migration => là DB khác (Sprint 2–3, script Task 2.4...).
    // Dừng với hướng dẫn rõ ràng thay vì để Migrate báo lỗi "bảng đã tồn tại" hoặc ghi lẫn vào DB đó.
    private static async Task EnsureNotForeignDatabaseAsync(ApplicationDbContext context)
    {
        if (!await context.Database.CanConnectAsync())
        {
            return; // chưa có DB => Migrate sẽ tạo mới
        }

        var counts = await context.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory' " +
                "UNION ALL SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME <> '__EFMigrationsHistory'")
            .ToListAsync();

        var hasHistory = counts[0] > 0;
        var hasTables = counts[1] > 0;

        // Có lịch sử migration nhưng không phải của backend này (vd project Sprint 2–3 dùng migration riêng)
        var ownHistory = false;
        if (hasHistory)
        {
            var firstMigration = context.Database.GetMigrations().First();
            ownHistory = await context.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM [__EFMigrationsHistory] WHERE [MigrationId] = {0}", firstMigration)
                .SingleAsync() > 0;
        }

        if (hasTables && !ownHistory)
        {
            var db = context.Database.GetDbConnection().Database;
            throw new InvalidOperationException(
                $"Database '{db}' đã có bảng nhưng không do backend này tạo (ví dụ DB Sprint 2–3 hoặc tạo bằng script Task 2.4). " +
                "Hãy đổi tên/xóa DB đó, hoặc tạo DB bằng docs/database/ContractManagement.sql, " +
                "hoặc đổi tên database trong ConnectionStrings:DefaultConnection (appsettings).");
        }
    }

    private static async Task SeedRolesAsync(ApplicationDbContext context)
    {
        var roles = new (string Name, string Description)[]
        {
            (AppRoles.Admin, "Quản trị viên toàn hệ thống"),
            (AppRoles.Manager, "Quản lý chi nhánh / người phê duyệt"),
            (AppRoles.Officer, "Nhân viên xử lý hồ sơ & tạo hợp đồng"),
            (AppRoles.Auditor, "Kiểm soát viên, chỉ xem dữ liệu"),
            (AppRoles.Director, "Giám đốc chi nhánh / Trưởng phòng QLRR – duyệt ngoại lệ (tờ trình xin nợ chứng từ)")
        };

        foreach (var (name, description) in roles)
        {
            if (!await context.Roles.AnyAsync(r => r.RoleName == name))
            {
                context.Roles.Add(new Role { RoleName = name, Description = description });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedDemoDataAsync(
        ApplicationDbContext context,
        string contentRootPath,
        string storagePath)
    {
        var roleIds = await context.Roles.ToDictionaryAsync(r => r.RoleName, r => r.RoleId);
        var today = DateTime.Today;
        var random = new Random(6); // cố định để số liệu demo lần nào cũng giống nhau

        // ---------- Chi nhánh ----------
        var branches = new List<Branch>
        {
            new() { BranchCode = "CN-HN01", BranchName = "Chi nhánh Hà Nội", Address = "123 Phố Huế, Hai Bà Trưng, Hà Nội", Phone = "02431234567" },
            new() { BranchCode = "CN-HCM01", BranchName = "Chi nhánh TP. Hồ Chí Minh", Address = "456 Lê Duẩn, Quận 1, TP. HCM", Phone = "02839876543" },
            new() { BranchCode = "CN-DN01", BranchName = "Chi nhánh Đà Nẵng", Address = "789 Nguyễn Văn Linh, Hải Châu, Đà Nẵng", Phone = "02363555888" }
        };
        context.Branches.AddRange(branches);
        await context.SaveChangesAsync();

        // ---------- Tài khoản ----------
        var hash = PasswordHasher.Hash(DemoPassword);
        var users = new List<User>
        {
            new() { Username = "admin", FullName = "Quản Trị Viên Hệ Thống", Email = "admin@qlhdhs.vn", RoleId = roleIds[AppRoles.Admin], BranchId = branches[0].BranchId },
            new() { Username = "manager_hn", FullName = "Nguyễn Văn Quản", Email = "manager.hn@qlhdhs.vn", RoleId = roleIds[AppRoles.Manager], BranchId = branches[0].BranchId },
            new() { Username = "manager_hcm", FullName = "Lê Thị Hồng", Email = "manager.hcm@qlhdhs.vn", RoleId = roleIds[AppRoles.Manager], BranchId = branches[1].BranchId },
            new() { Username = "officer_hn01", FullName = "Trần Minh Tuấn", Email = "officer1.hn@qlhdhs.vn", RoleId = roleIds[AppRoles.Officer], BranchId = branches[0].BranchId },
            new() { Username = "officer_hcm01", FullName = "Phạm Thu Trang", Email = "officer1.hcm@qlhdhs.vn", RoleId = roleIds[AppRoles.Officer], BranchId = branches[1].BranchId },
            new() { Username = "officer_dn01", FullName = "Võ Đức Anh", Email = "officer1.dn@qlhdhs.vn", RoleId = roleIds[AppRoles.Officer], BranchId = branches[2].BranchId },
            new() { Username = "auditor", FullName = "Đặng Kiểm Soát", Email = "auditor@qlhdhs.vn", RoleId = roleIds[AppRoles.Auditor], BranchId = branches[0].BranchId }
        };
        foreach (var u in users)
        {
            u.PasswordHash = hash;
            u.Status = "ACTIVE";
            u.CreatedAt = today.AddMonths(-12);
        }
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        var managers = users.Where(u => u.RoleId == roleIds[AppRoles.Manager]).ToList();
        var officers = users.Where(u => u.RoleId == roleIds[AppRoles.Officer]).ToList();

        // ---------- Loại hợp đồng ----------
        var types = new List<ContractType>
        {
            new() { TypeName = "Hợp đồng tín dụng cá nhân", Description = "Vay tiêu dùng, vay mua nhà/xe cho khách hàng cá nhân" },
            new() { TypeName = "Hợp đồng vay vốn doanh nghiệp", Description = "Cấp hạn mức, vay vốn lưu động, vay đầu tư" },
            new() { TypeName = "Hợp đồng tiền gửi có kỳ hạn", Description = "Tiền gửi tiết kiệm có kỳ hạn" },
            new() { TypeName = "Hợp đồng bảo lãnh", Description = "Bảo lãnh thanh toán, dự thầu, thực hiện hợp đồng" },
            new() { TypeName = "Hợp đồng thế chấp tài sản", Description = "Thế chấp bất động sản, phương tiện, giấy tờ có giá" }
        };
        context.ContractTypes.AddRange(types);
        await context.SaveChangesAsync();

        // ---------- Khách hàng ----------
        var customers = new List<Customer>
        {
            new() { CustomerCode = "KH2026001", CustomerType = "INDIVIDUAL", FullName = "Phạm Nhật Minh", Gender = "Nam", DateOfBirth = new DateTime(1990, 5, 15), IdentityNumber = "001090012345", Phone = "0912345678" },
            new() { CustomerCode = "KH2026002", CustomerType = "CORPORATE", FullName = "Công ty CP Công nghệ Toàn Cầu", IdentityNumber = "0102345678", Phone = "0243888999" },
            new() { CustomerCode = "KH2026003", CustomerType = "INDIVIDUAL", FullName = "Nguyễn Thị Lan", Gender = "Nữ", DateOfBirth = new DateTime(1987, 11, 2), IdentityNumber = "079187004321", Phone = "0987654321" },
            new() { CustomerCode = "KH2026004", CustomerType = "CORPORATE", FullName = "Công ty TNHH Thương mại Sao Việt", IdentityNumber = "0309876543", Phone = "0283777666" },
            new() { CustomerCode = "KH2026005", CustomerType = "INDIVIDUAL", FullName = "Trần Quốc Bảo", Gender = "Nam", DateOfBirth = new DateTime(1995, 3, 21), IdentityNumber = "048095001122", Phone = "0905111222" },
            new() { CustomerCode = "KH2026006", CustomerType = "CORPORATE", FullName = "Công ty CP Xây dựng Miền Trung", IdentityNumber = "0401122334", Phone = "02363999000" },
            new() { CustomerCode = "KH2026007", CustomerType = "INDIVIDUAL", FullName = "Lê Hoàng Yến", Gender = "Nữ", DateOfBirth = new DateTime(1992, 8, 9), IdentityNumber = "001192005566", Phone = "0934556677" },
            new() { CustomerCode = "KH2026008", CustomerType = "CORPORATE", FullName = "Công ty CP Logistics Hưng Thịnh", IdentityNumber = "0315566778", Phone = "0283444555" }
        };
        foreach (var c in customers)
        {
            c.Status = "ACTIVE";
            c.CreatedAt = today.AddMonths(-13);
        }
        context.Customers.AddRange(customers);
        await context.SaveChangesAsync();

        // ---------- Kịch bản hợp đồng ----------
        // (trạng thái, ký cách đây bao nhiêu tháng, số ngày còn đến hạn)
        var scenarios = new (string Status, int MonthsAgo, int? DaysToExpiry)[]
        {
            (ContractStatuses.Active, 11, 200), (ContractStatuses.Active, 10, 12),
            (ContractStatuses.Active, 9, 400), (ContractStatuses.Expired, 11, -20),
            (ContractStatuses.Active, 8, 25), (ContractStatuses.Active, 7, 300),
            (ContractStatuses.Terminated, 7, 150), (ContractStatuses.Active, 6, 500),
            (ContractStatuses.Active, 6, 6), (ContractStatuses.Active, 5, 700),
            (ContractStatuses.Rejected, 5, null), (ContractStatuses.Active, 4, 365),
            (ContractStatuses.Active, 4, 18), (ContractStatuses.Active, 3, 900),
            (ContractStatuses.Active, 3, -3), (ContractStatuses.Active, 2, 450),
            (ContractStatuses.Rejected, 2, null), (ContractStatuses.Active, 1, 600),
            (ContractStatuses.Active, 1, 28), (ContractStatuses.PendingApproval, 0, null),
            (ContractStatuses.PendingApproval, 0, null), (ContractStatuses.PendingApproval, 0, null),
            (ContractStatuses.Draft, 0, null), (ContractStatuses.Draft, 0, null),
            (ContractStatuses.Cancelled, 1, null), (ContractStatuses.Active, 0, 730)
        };

        var personalDocTypes = new[] { "CCCD", "APPLICATION_FORM", "INCOME_PROOF", "COLLATERAL" };
        var corporateDocTypes = new[] { "BUSINESS_LICENSE", "APPLICATION_FORM", "COLLATERAL" };
        var fullStoragePath = Path.Combine(contentRootPath, storagePath);
        Directory.CreateDirectory(fullStoragePath);

        for (var i = 0; i < scenarios.Length; i++)
        {
            var (status, monthsAgo, daysToExpiry) = scenarios[i];
            var branch = branches[i % branches.Count];
            var officer = officers.First(o => o.BranchId == branch.BranchId);
            var manager = managers.FirstOrDefault(m => m.BranchId == branch.BranchId) ?? managers[0];
            var customer = customers[i % customers.Count];
            // Cá nhân: tín dụng / tiền gửi / thế chấp; doanh nghiệp: vay vốn / bảo lãnh / thế chấp
            var typeIndexes = customer.CustomerType == "CORPORATE" ? new[] { 1, 3, 4 } : new[] { 0, 2, 4 };
            var type = types[typeIndexes[(i + i / customers.Count) % typeIndexes.Length]];

            var createdAt = today.AddMonths(-monthsAgo).AddDays(-random.Next(0, 20)).AddHours(9);
            // Các bước sau (gửi duyệt, duyệt) cách ngày tạo tối đa 2 ngày => lùi về để không rơi vào tương lai
            var latestCreatedAt = today.AddDays(-3).AddHours(9);
            if (createdAt > latestCreatedAt)
            {
                createdAt = latestCreatedAt;
            }

            // Hồ sơ (Application)
            var application = new Application
            {
                ApplicationCode = $"HS-2026-{i + 1:D4}",
                CustomerId = customer.CustomerId,
                ApplicationType = type.TypeName.Replace("Hợp đồng ", "Hồ sơ "),
                AssignedTo = officer.UserId,
                BranchId = branch.BranchId,
                Status = status == ContractStatuses.Draft ? "DRAFT"
                    : status == ContractStatuses.PendingApproval ? "SUBMITTED"
                    : status is ContractStatuses.Rejected or ContractStatuses.Cancelled ? "REJECTED"
                    : "APPROVED",
                Priority = i % 4 == 0 ? "HIGH" : "NORMAL",
                CreatedAt = createdAt.AddDays(-3),
                SubmittedAt = status == ContractStatuses.Draft ? null : createdAt.AddDays(-2)
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            // Giá trị: cá nhân 200tr–3 tỷ, doanh nghiệp 2–50 tỷ
            var value = customer.CustomerType == "CORPORATE"
                ? random.Next(20, 500) * 100_000_000m
                : random.Next(2, 30) * 100_000_000m;

            var approved = status is ContractStatuses.Active or ContractStatuses.Expired or ContractStatuses.Terminated;
            DateTime? signed = approved ? createdAt.AddDays(2).Date : null;
            DateTime? expiry = daysToExpiry.HasValue ? today.AddDays(daysToExpiry.Value) : null;
            if (!approved && expiry == null && status != ContractStatuses.Cancelled)
            {
                expiry = today.AddMonths(12 + i % 3 * 12);
            }

            var contract = new Contract
            {
                ContractCode = $"HD-2026-{i + 1:D4}",
                ApplicationId = application.ApplicationId,
                ContractTypeId = type.ContractTypeId,
                BranchId = branch.BranchId,
                CreatedBy = officer.UserId,
                SignedDate = signed,
                EffectiveDate = signed?.AddDays(3) ?? today.AddDays(7),
                ExpiryDate = expiry,
                ContractValue = value,
                Status = status,
                CreatedAt = createdAt,
                // DemoDataUpgrade nâng lên thời điểm đổi trạng thái cuối cùng
                UpdatedAt = createdAt
            };
            context.Contracts.Add(contract);
            await context.SaveChangesAsync();

            // Lịch sử trạng thái theo đúng luồng
            var steps = new List<(string? From, string To, User By, DateTime At, string Reason)>
            {
                (null, ContractStatuses.Draft, officer, createdAt, "Khởi tạo hợp đồng")
            };
            if (status != ContractStatuses.Draft && status != ContractStatuses.Cancelled)
            {
                steps.Add((ContractStatuses.Draft, ContractStatuses.PendingApproval, officer, createdAt.AddHours(5), "Gửi duyệt"));
            }
            if (approved)
            {
                steps.Add((ContractStatuses.PendingApproval, ContractStatuses.Active, manager, createdAt.AddDays(2), "Hồ sơ đầy đủ, đồng ý phê duyệt"));
            }
            if (status == ContractStatuses.Rejected)
            {
                steps.Add((ContractStatuses.PendingApproval, ContractStatuses.Rejected, manager, createdAt.AddDays(1), "Thiếu chứng minh thu nhập / tài sản bảo đảm"));
            }
            if (status == ContractStatuses.Expired)
            {
                steps.Add((ContractStatuses.Active, ContractStatuses.Expired, manager, expiry!.Value.AddDays(1), "Hết thời hạn hợp đồng"));
            }
            if (status == ContractStatuses.Terminated)
            {
                steps.Add((ContractStatuses.Active, ContractStatuses.Terminated, manager, createdAt.AddMonths(3), "Khách hàng tất toán trước hạn"));
            }
            if (status == ContractStatuses.Cancelled)
            {
                steps.Add((ContractStatuses.Draft, ContractStatuses.Cancelled, officer, createdAt.AddDays(1), "Khách hàng không tiếp tục giao dịch"));
            }

            foreach (var step in steps)
            {
                context.ContractStatusHistories.Add(new ContractStatusHistory
                {
                    ContractId = contract.ContractId,
                    OldStatus = step.From,
                    NewStatus = step.To,
                    ChangedBy = step.By.UserId,
                    ChangedAt = step.At,
                    Reason = step.Reason
                });
            }

            if (approved || status == ContractStatuses.Rejected)
            {
                context.Approvals.Add(new Approval
                {
                    ApplicationId = application.ApplicationId,
                    ApproverId = manager.UserId,
                    Status = approved ? "APPROVED" : "REJECTED",
                    Comment = approved ? "Đồng ý phê duyệt" : "Thiếu hồ sơ chứng minh",
                    ApprovedAt = createdAt.AddDays(approved ? 2 : 1)
                });
            }

            // 1–3 tài liệu cho mỗi hồ sơ, có file PDF thật để tải về được
            var docCount = 1 + i % 3;
            for (var d = 0; d < docCount; d++)
            {
                var docTypes = customer.CustomerType == "CORPORATE" ? corporateDocTypes : personalDocTypes;
                var docType = docTypes[d % docTypes.Length];
                var storedName = $"seed_{application.ApplicationCode}_{d + 1}.pdf";
                var pdf = BuildDemoPdf($"{application.ApplicationCode} - {docType} - {customer.CustomerCode}");
                await File.WriteAllBytesAsync(Path.Combine(fullStoragePath, storedName), pdf);

                context.Documents.Add(new Document
                {
                    ApplicationId = application.ApplicationId,
                    DocumentType = docType,
                    DocumentName = $"{docType}_{customer.CustomerCode}.pdf",
                    FilePath = $"{storagePath}/{storedName}".Replace("\\", "/"),
                    FileSize = pdf.Length,
                    UploadedBy = officer.UserId,
                    UploadedAt = createdAt.AddHours(1 + d),
                    VerificationStatus = approved ? "VERIFIED" : "PENDING",
                    VerifiedBy = approved ? manager.UserId : null,
                    VerifiedAt = approved ? createdAt.AddDays(1) : null
                });
            }

            await context.SaveChangesAsync();
        }
    }

    // PDF 1 trang tối giản (chỉ ký tự ASCII) để demo tải tài liệu
    internal static byte[] BuildDemoPdf(string text)
    {
        var safe = new string(text.Where(ch => ch < 128 && ch != '(' && ch != ')' && ch != '\\').ToArray());
        var stream = $"BT /F1 18 Tf 72 720 Td (QLHD Nhom 6 - Tai lieu demo) Tj 0 -32 Td /F1 12 Tf ({safe}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(sb.Length);
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = sb.Length;
        sb.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var off in offsets)
        {
            sb.Append($"{off:D10} 00000 n \n");
        }
        sb.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
