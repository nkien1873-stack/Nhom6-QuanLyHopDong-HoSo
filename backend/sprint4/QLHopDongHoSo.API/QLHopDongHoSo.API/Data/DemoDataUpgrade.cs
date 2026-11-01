using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Models;
using QLHopDongHoSo.API.Services;

namespace QLHopDongHoSo.API.Data;

// Bổ sung dữ liệu cho các cột/tính năng thêm sau (Purpose, Collateral, TermMonths, InterestRate,
// Representative, nhật ký hệ thống) để DB demo đã tạo trước đó vẫn hiển thị đầy đủ.
// Chỉ điền chỗ còn trống => chạy nhiều lần không ghi đè dữ liệu người dùng đã nhập.
public static class DemoDataUpgrade
{
    private record LoanProfile(string[] Purposes, string?[] Collaterals, decimal? Rate);

    private static readonly Dictionary<string, LoanProfile> Profiles = new()
    {
        ["Hợp đồng tín dụng cá nhân"] = new(
            ["Vay tiêu dùng – sửa chữa nhà", "Vay mua ô tô cá nhân", "Vay mua căn hộ để ở"],
            ["Tín chấp", "Xe ô tô hình thành từ vốn vay", "Căn hộ hình thành từ vốn vay"], 10.5m),
        ["Hợp đồng vay vốn doanh nghiệp"] = new(
            ["Bổ sung vốn lưu động", "Đầu tư máy móc thiết bị", "Thanh toán tiền hàng nhập khẩu"],
            ["Hàng tồn kho luân chuyển", "Máy móc thiết bị", "Quyền sử dụng đất nhà xưởng"], 8.5m),
        ["Hợp đồng tiền gửi có kỳ hạn"] = new(
            ["Gửi tiết kiệm có kỳ hạn", "Tiền gửi doanh nghiệp có kỳ hạn"], [null], 5.5m),
        ["Hợp đồng bảo lãnh"] = new(
            ["Bảo lãnh thanh toán cho nhà cung cấp", "Bảo lãnh dự thầu", "Bảo lãnh thực hiện hợp đồng"],
            ["Ký quỹ 20%", "Ký quỹ 30%"], null),
        ["Hợp đồng thế chấp tài sản"] = new(
            ["Thế chấp bảo đảm khoản vay", "Bổ sung tài sản bảo đảm"],
            ["Quyền sử dụng đất và tài sản gắn liền", "Xe ô tô", "Giấy tờ có giá"], null),
        ["Hợp đồng cung cấp dịch vụ CNTT"] = new(["Cung cấp dịch vụ công nghệ thông tin"], [null], null),
        ["Hợp đồng bảo trì hệ thống"] = new(["Bảo trì hệ thống phần mềm"], [null], null)
    };

    private static readonly Dictionary<string, string> Representatives = new()
    {
        ["KH2026002"] = "Phạm Quốc Toàn",
        ["KH2026004"] = "Nguyễn Văn Sao",
        ["KH2026006"] = "Hoàng Đại Việt",
        ["KH2026008"] = "Đặng Hưng Thịnh"
    };

    // fillDemoDetails = true chỉ khi lần khởi động này vừa nạp dữ liệu demo / dữ liệu Sprint 2–3.
    // Các DB đã có từ trước (kể cả DB nộp) đều đã được điền ở lần đầu, nên các lần khởi động sau bỏ qua,
    // không quét/ghi đè hồ sơ người dùng (trước đây 20.595 hồ sơ để trống mục đích bị ghi đè, khởi động 56 s).
    public static async Task ApplyAsync(ApplicationDbContext context, string contentRootPath, bool fillDemoDetails)
    {
        if (fillDemoDetails)
        {
            await FillContractDetailsAsync(context);
            await FillRepresentativesAsync(context);
            await SyncUpdatedAtAsync(context);
        }

        await SeedAuditLogsAsync(context);
        await FillFileSizesAsync(context, contentRootPath);
    }

    // UpdatedAt (thêm sau) = lần đổi trạng thái gần nhất nếu đang nhỏ hơn => danh sách "mới cập nhật" đúng thứ tự
    private static async Task SyncUpdatedAtAsync(ApplicationDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE c SET c.[UpdatedAt] = h.[MaxAt] FROM [CONTRACTS] c " +
            "JOIN (SELECT [ContractID], MAX([ChangedAt]) AS [MaxAt] FROM [CONTRACT_STATUS_HISTORY] GROUP BY [ContractID]) h " +
            "ON h.[ContractID] = c.[ContractID] WHERE c.[UpdatedAt] < h.[MaxAt]");
    }

    // FileSize (thêm sau) cho tài liệu cũ: đọc dung lượng trên đĩa một lần rồi lưu vào DB
    private static async Task FillFileSizesAsync(ApplicationDbContext context, string contentRootPath)
    {
        var documents = await context.Documents.Where(d => d.FileSize == null).ToListAsync();
        foreach (var d in documents)
        {
            var info = new FileInfo(Path.Combine(contentRootPath, d.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString())));
            d.FileSize = info.Exists ? info.Length : 0;
        }

        await context.SaveChangesAsync();
    }

    // Chỉ các hợp đồng demo do backend tự tạo (HD-2026-0001..0026) và 3 hợp đồng Sprint 2–3
    private static readonly string[] DemoContractCodes =
        Enumerable.Range(1, 26).Select(i => $"HD-2026-{i:D4}").Concat(["HD001", "HD002", "HD004"]).ToArray();

    private static async Task FillContractDetailsAsync(ApplicationDbContext context)
    {
        var contracts = await context.Contracts
            .Include(c => c.ContractType)
            .Where(c => DemoContractCodes.Contains(c.ContractCode) && (c.Purpose == null || c.TermMonths == null))
            .ToListAsync();

        foreach (var c in contracts)
        {
            var profile = Profiles.GetValueOrDefault(c.ContractType?.TypeName ?? "")
                ?? new LoanProfile(["Theo nhu cầu khách hàng"], [null], null);
            var i = c.ContractId;

            if (c.TermMonths == null)
            {
                c.TermMonths = c.EffectiveDate.HasValue && c.ExpiryDate.HasValue
                    ? Math.Clamp((int)Math.Round((c.ExpiryDate.Value - c.EffectiveDate.Value).TotalDays / 30.44), 1, 360)
                    : new[] { 12, 24, 36 }[i % 3];
            }

            if (c.Purpose == null)
            {
                c.Purpose = profile.Purposes[i % profile.Purposes.Length];
                c.Collateral ??= profile.Collaterals[i % profile.Collaterals.Length];
                c.InterestRate ??= profile.Rate;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task FillRepresentativesAsync(ApplicationDbContext context)
    {
        var codes = Representatives.Keys.ToList();
        var customers = await context.Customers
            .Where(c => c.CustomerType == "CORPORATE" && c.Representative == null && codes.Contains(c.CustomerCode))
            .ToListAsync();

        foreach (var c in customers)
        {
            c.Representative = Representatives[c.CustomerCode];
        }

        await context.SaveChangesAsync();
    }

    // Nhật ký trống => dựng lại từ lịch sử trạng thái và tài liệu đã có, kèm vài lần đăng nhập
    private static async Task SeedAuditLogsAsync(ApplicationDbContext context)
    {
        if (await context.AuditLogs.AnyAsync())
        {
            return;
        }

        var logs = new List<AuditLog>();

        var history = await context.ContractStatusHistories
            .Include(h => h.Contract)
            .ToListAsync();
        foreach (var h in history)
        {
            var (action, text) = h.NewStatus switch
            {
                ContractStatuses.Draft when h.OldStatus == null => (AuditActions.Create, "Tạo hồ sơ nháp"),
                ContractStatuses.PendingApproval => (AuditActions.Submit, "Gửi phê duyệt"),
                ContractStatuses.Active => (AuditActions.Approve, "Phê duyệt hồ sơ"),
                ContractStatuses.Rejected => (AuditActions.Reject, "Từ chối"),
                _ => (AuditActions.Update, $"Chuyển trạng thái → {h.NewStatus}")
            };
            var detail = action is AuditActions.Approve or AuditActions.Reject && !string.IsNullOrWhiteSpace(h.Reason)
                ? $"{text}: {h.Reason}"
                : text;
            logs.Add(Log(h.ChangedBy, action, "CONTRACTS", h.Contract!.ContractCode, detail, h.ChangedAt, "10.0.1.31"));
        }

        var documents = await context.Documents.ToListAsync();
        var codeByApplication = await context.Contracts
            .GroupBy(c => c.ApplicationId)
            .Select(g => new { g.Key, Code = g.Min(c => c.ContractCode) })
            .ToDictionaryAsync(x => x.Key, x => x.Code);
        foreach (var d in documents)
        {
            logs.Add(Log(d.UploadedBy, AuditActions.Upload, "DOCUMENTS",
                codeByApplication.GetValueOrDefault(d.ApplicationId) ?? $"HS #{d.ApplicationId}",
                $"Tải lên {d.DocumentName}", d.UploadedAt, "10.0.1.31"));
        }

        // Một vài lần đăng nhập gần đây để màn "Người dùng" có cột đăng nhập gần nhất
        var users = await context.Users.Where(u => u.Status == "ACTIVE").ToListAsync();
        var today = DateTime.Today;
        foreach (var (u, i) in users.Select((u, i) => (u, i)))
        {
            logs.Add(Log(u.UserId, AuditActions.Login, "SYSTEM", "Hệ thống", "Đăng nhập thành công",
                today.AddDays(-(i % 3)).AddHours(8).AddMinutes(5 * i), $"10.0.1.{10 + i}"));
        }

        context.AuditLogs.AddRange(logs.OrderBy(l => l.CreatedAt));
        await context.SaveChangesAsync();
    }

    // N09 – chạy MỘT lần trên DB demo (có manager_hn) chưa có tài khoản cấp ngoại lệ:
    // 1) tạo director_hn / director_hcm / director_dn (Giám đốc chi nhánh, mật khẩu demo);
    // 2) hồ sơ demo đã trình/duyệt mà thiếu tài liệu theo quy tắc mới => bổ sung file PDF demo,
    //    riêng hồ sơ đang chờ duyệt thiếu tài liệu "bổ sung sau" => tờ trình nợ chứng từ đã được Giám đốc duyệt
    //    (để demo cảnh báo WARN_DOC_02). Hồ sơ nháp/từ chối giữ nguyên để demo ERR_DOC_01 / ERR_DOC_02.
    public static async Task ApplyDocumentRulesAsync(
        ApplicationDbContext context, string contentRootPath, string storagePath, Func<string> demoPasswordHash)
    {
        var directorRoleId = await context.Roles.Where(r => r.RoleName == AppRoles.Director).Select(r => r.RoleId).FirstAsync();
        if (await context.Users.AnyAsync(u => u.RoleId == directorRoleId) ||
            !await context.Users.AnyAsync(u => u.Username == "manager_hn"))
        {
            return;
        }

        // Một transaction: lỗi giữa chừng thì không còn tài khoản Director nào => lần khởi động sau chạy lại từ đầu
        await using var transaction = await context.Database.BeginTransactionAsync();

        var branches = await context.Branches.ToDictionaryAsync(b => b.BranchCode, b => b.BranchId);
        var hash = demoPasswordHash();
        var directors = new (string Username, string Name, string Branch)[]
        {
            ("director_hn", "Hoàng Minh Đức", "CN-HN01"),
            ("director_hcm", "Vũ Thị Thanh Hà", "CN-HCM01"),
            ("director_dn", "Ngô Quang Vinh", "CN-DN01")
        };
        foreach (var (username, name, branch) in directors)
        {
            if (!branches.TryGetValue(branch, out var branchId) || await context.Users.AnyAsync(u => u.Username == username))
            {
                continue;
            }

            context.Users.Add(new User
            {
                Username = username,
                FullName = name,
                Email = $"{username.Replace('_', '.')}@qlhdhs.vn",
                RoleId = directorRoleId,
                BranchId = branchId,
                PasswordHash = hash,
                Status = "ACTIVE",
                CreatedAt = DateTime.Today.AddMonths(-12)
            });
        }
        await context.SaveChangesAsync();

        var directorByBranch = await context.Users
            .Where(u => u.RoleId == directorRoleId && u.BranchId != null)
            .ToDictionaryAsync(u => u.BranchId!.Value, u => u.UserId);
        if (directorByBranch.Count == 0)
        {
            await transaction.CommitAsync();
            return;
        }

        var contracts = await context.Contracts
            .Include(c => c.Application!).ThenInclude(a => a.Customer)
            .Include(c => c.ContractType)
            .Where(c => DemoContractCodes.Contains(c.ContractCode) &&
                        (c.Status == ContractStatuses.PendingApproval || c.Status == ContractStatuses.Active ||
                         c.Status == ContractStatuses.Expired || c.Status == ContractStatuses.Terminated))
            .ToListAsync();
        var applicationIds = contracts.Select(c => c.ApplicationId).ToList();
        var existing = (await context.Documents
                .Where(d => applicationIds.Contains(d.ApplicationId) && d.VerificationStatus != "REJECTED")
                .Select(d => new { d.ApplicationId, d.DocumentType })
                .ToListAsync())
            .Select(d => (d.ApplicationId, d.DocumentType))
            .ToHashSet();
        var approvals = (await context.Approvals
                .Where(a => applicationIds.Contains(a.ApplicationId) && a.Status == "APPROVED")
                .Select(a => new { a.ApplicationId, a.ApproverId, a.ApprovedAt })
                .ToListAsync())
            .GroupBy(a => a.ApplicationId)
            .ToDictionary(g => g.Key, g => g.First());

        var fullStoragePath = Path.Combine(contentRootPath, storagePath);
        Directory.CreateDirectory(fullStoragePath);

        foreach (var c in contracts)
        {
            var customer = c.Application!.Customer!;
            var requirements = DocumentRequirements.For(customer.CustomerType, c.ContractType?.TypeName, c.Collateral);
            foreach (var (type, requirement) in requirements)
            {
                if (existing.Contains((c.ApplicationId, type)))
                {
                    continue;
                }

                if (c.Status == ContractStatuses.PendingApproval && requirement == DocumentRequirements.Deferred)
                {
                    var requestedAt = c.CreatedAt.AddHours(2);
                    context.DocumentDeferrals.Add(new DocumentDeferral
                    {
                        ContractId = c.ContractId,
                        DocumentType = type,
                        DueDate = DateTime.Today.AddDays(15),
                        Reason = "Khách hàng đang hoàn tất thủ tục đăng ký giao dịch bảo đảm; cam kết bổ sung chứng từ trước thời hạn.",
                        Status = DeferralStatuses.Approved,
                        RequestedBy = c.CreatedBy,
                        RequestedAt = requestedAt,
                        DecidedBy = directorByBranch.GetValueOrDefault(c.BranchId, directorByBranch.Values.First()),
                        DecidedAt = requestedAt.AddHours(1),
                        DecisionNote = "Đồng ý cho nợ chứng từ, Maker theo dõi bổ sung đúng hạn."
                    });
                    continue;
                }

                var code = c.Application.ApplicationCode;
                var storedName = $"seed_{code}_{type}.pdf";
                var pdf = DbInitializer.BuildDemoPdf($"{code} - {type} - {customer.CustomerCode}");
                await File.WriteAllBytesAsync(Path.Combine(fullStoragePath, storedName), pdf);

                var approval = approvals.GetValueOrDefault(c.ApplicationId);
                var uploadedAt = c.CreatedAt.AddHours(1).AddMinutes(30);
                context.Documents.Add(new Document
                {
                    ApplicationId = c.ApplicationId,
                    DocumentType = type,
                    DocumentName = $"{type}_{customer.CustomerCode}.pdf",
                    FilePath = $"{storagePath}/{storedName}".Replace("\\", "/"),
                    FileSize = pdf.Length,
                    UploadedBy = c.CreatedBy,
                    UploadedAt = uploadedAt,
                    VerificationStatus = approval != null ? "VERIFIED" : "PENDING",
                    VerifiedBy = approval?.ApproverId,
                    VerifiedAt = approval?.ApprovedAt ?? (approval != null ? uploadedAt.AddDays(1) : null)
                });
                existing.Add((c.ApplicationId, type));
            }
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static AuditLog Log(int? userId, string action, string type, string target, string detail, DateTime at, string ip) =>
        new()
        {
            UserId = userId,
            Action = action,
            EntityType = type,
            EntityId = target.Length > 50 ? target[..50] : target,
            Description = detail,
            CreatedAt = at,
            IPAddress = ip
        };
}
