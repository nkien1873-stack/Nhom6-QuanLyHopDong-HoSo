using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.DTOs.Contracts;
using QLHopDongHoSo.API.DTOs.Dashboard;
using QLHopDongHoSo.API.Models;

namespace QLHopDongHoSo.API.Services;

// Mọi chỉ số được tính bằng COUNT/SUM/GROUP BY trong SQL Server, chỉ trả về con số
// (trước đây nạp mọi hợp đồng vào bộ nhớ rồi mới đếm => chậm dần theo khối lượng dữ liệu).
public class DashboardService : IDashboardService
{
    private const int LateHours = 48;

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public DashboardService(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    private IQueryable<Contract> Contracts(int? branchId)
    {
        var query = _context.Contracts.AsNoTracking();
        return branchId is > 0 ? query.Where(c => c.BranchId == branchId) : query;
    }

    // Người duyệt chỉ xử lý hồ sơ chi nhánh mình; Admin (hoặc tài khoản không gán chi nhánh) thấy tất cả
    private async Task<(User Actor, int? ScopeBranchId)> ScopeAsync(int userId)
    {
        var actor = await _context.GetActorAsync(userId);
        return (actor, actor.IsAdmin() ? null : actor.BranchId);
    }

    private async Task<Dictionary<string, int>> StatusCountsAsync(IQueryable<Contract> contracts)
    {
        var today = DateTime.Today;
        var rows = await contracts
            .GroupBy(ContractService.DisplayKey(today, today.AddDays(ContractService.ExpiringDays)))
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();
        var map = rows.ToDictionary(r => r.Key, r => r.Count);

        return new Dictionary<string, int>
        {
            ["DRAFT"] = map.GetValueOrDefault(ContractStatuses.Draft),
            ["PENDING"] = map.GetValueOrDefault(ContractStatuses.PendingApproval),
            ["ACTIVE"] = map.GetValueOrDefault(ContractStatuses.Active),
            ["EXPIRING"] = map.GetValueOrDefault("EXPIRING"),
            ["REJECTED"] = map.GetValueOrDefault(ContractStatuses.Rejected),
            ["EXPIRED"] = map.GetValueOrDefault(ContractStatuses.Expired),
            ["TERMINATED"] = map.GetValueOrDefault(ContractStatuses.Terminated),
            ["CANCELLED"] = map.GetValueOrDefault(ContractStatuses.Cancelled)
        };
    }

    // Hợp đồng ký mới theo tháng (n tháng gần nhất) – chỉ tính hợp đồng đã ký
    private async Task<List<MonthlyNewItem>> MonthlyNewAsync(IQueryable<Contract> contracts, int months = 6)
    {
        var today = DateTime.Today;
        var first = new DateTime(today.Year, today.Month, 1).AddMonths(-(months - 1));

        // Loại "tiền gửi" lấy từ bảng danh mục nhỏ, rồi gom nhóm theo mã loại (không so chuỗi trên từng hợp đồng)
        var depositTypes = await _context.ContractTypes.AsNoTracking()
            .Where(t => t.TypeName.Contains("tiền gửi"))
            .Select(t => t.ContractTypeId)
            .ToListAsync();

        var rows = await contracts
            .Where(c => c.SignedDate >= first)
            .GroupBy(c => new { c.SignedDate!.Value.Year, c.SignedDate.Value.Month, c.ContractTypeId })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.ContractTypeId, Count = g.Count() })
            .ToListAsync();

        return Enumerable.Range(0, months)
            .Select(i => first.AddMonths(i))
            .Select(m => new MonthlyNewItem
            {
                Label = $"T{m.Month}",
                Credit = rows.Where(r => r.Year == m.Year && r.Month == m.Month && !depositTypes.Contains(r.ContractTypeId)).Sum(r => r.Count),
                Deposit = rows.Where(r => r.Year == m.Year && r.Month == m.Month && depositTypes.Contains(r.ContractTypeId)).Sum(r => r.Count)
            })
            .ToList();
    }

    // =====================================================
    // TỔNG QUAN (màn Dashboard)
    // =====================================================
    // Số liệu Tổng quan tách 3 phần để không phải tính lại riêng cho TỪNG người dùng sau mỗi lần ghi:
    // - phần chung toàn hệ thống: 1 khóa cache cho mọi người
    // - hàng chờ duyệt theo chi nhánh: 1 khóa / chi nhánh (Admin = toàn hệ thống)
    // - "hồ sơ nháp của tôi": truy vấn nhỏ theo index (CreatedBy, Status), không cache
    // (trước đây 74 người dùng = 74 lần quét 100.000 hồ sơ mỗi khi dữ liệu đổi => Tổng quan 6,3 s)
    private sealed record OverviewShared(
        int Total, int Customers, int Pending, int OverduePending, int Expiring, int ActiveCount, decimal ActiveValue,
        Dictionary<string, int> StatusCounts, List<MonthlyNewItem> Monthly, List<ContractListItem> Recent,
        List<ContractListItem> ExpiringSoon);

    private sealed record OverviewBranch(int MyQueue, List<ContractListItem> Queue);

    public async Task<DashboardOverview> GetOverviewAsync(int userId)
    {
        var (_, scope) = await ScopeAsync(userId);
        var shared = await _cache.GetOrCreateAsync("overview-shared", BuildOverviewSharedAsync);
        var branch = await _cache.GetOrCreateAsync($"overview-branch:{scope}", () => BuildOverviewBranchAsync(scope));

        var myDrafts = await _context.Contracts.AsNoTracking()
            .Where(c => c.CreatedBy == userId && c.Status == ContractStatuses.Draft)
            .OrderByDescending(c => c.UpdatedAt).Take(2)
            .Select(ContractService.ToListItem).ToListAsync();

        return new DashboardOverview
        {
            Total = shared.Total,
            Customers = shared.Customers,
            Pending = shared.Pending,
            MyQueue = branch.MyQueue,
            OverduePending = shared.OverduePending,
            Expiring = shared.Expiring,
            ActiveCount = shared.ActiveCount,
            ActiveValue = shared.ActiveValue,
            StatusCounts = shared.StatusCounts,
            Monthly = shared.Monthly,
            Recent = shared.Recent,
            Queue = branch.Queue,
            ExpiringSoon = shared.ExpiringSoon,
            MyDrafts = myDrafts
        };
    }

    private async Task<OverviewShared> BuildOverviewSharedAsync()
    {
        var today = DateTime.Today;
        var limit = today.AddDays(ContractService.ExpiringDays);
        var lateBefore = DateTime.Now.AddHours(-LateHours);

        var all = Contracts(null);
        var active = all.Where(c => c.Status == ContractStatuses.Active && (c.ExpiryDate == null || c.ExpiryDate >= today));
        var expiring = all.Where(c => c.Status == ContractStatuses.Active && c.ExpiryDate >= today && c.ExpiryDate <= limit);

        // Tổng, chờ duyệt, sắp hết hạn, hiệu lực suy ra từ một câu GROUP BY trạng thái
        var statusCounts = await StatusCountsAsync(all);

        return new OverviewShared(
            Total: statusCounts.Values.Sum(),
            Customers: await all.Select(c => c.Application!.CustomerId).Distinct().CountAsync(),
            Pending: statusCounts["PENDING"],
            OverduePending: await all.CountAsync(c => c.Status == ContractStatuses.PendingApproval && c.UpdatedAt < lateBefore),
            Expiring: statusCounts["EXPIRING"],
            ActiveCount: statusCounts["ACTIVE"] + statusCounts["EXPIRING"],
            ActiveValue: await active.SumAsync(c => c.ContractValue ?? 0),
            StatusCounts: statusCounts,
            Monthly: await MonthlyNewAsync(all),
            Recent: await all.OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.ContractId)
                .Take(6).Select(ContractService.ToListItem).ToListAsync(),
            ExpiringSoon: await expiring.OrderBy(c => c.ExpiryDate)
                .Take(2).Select(ContractService.ToListItem).ToListAsync());
    }

    private async Task<OverviewBranch> BuildOverviewBranchAsync(int? scope)
    {
        var queue = Contracts(scope).Where(c => c.Status == ContractStatuses.PendingApproval);
        return new OverviewBranch(
            await queue.CountAsync(),
            await queue.OrderBy(c => c.UpdatedAt).Take(3).Select(ContractService.ToListItem).ToListAsync());
    }

    // =====================================================
    // PHÊ DUYỆT – thẻ thống kê
    // =====================================================
    // Phần theo chi nhánh cache chung (1 khóa / chi nhánh); "đã duyệt hôm nay" của riêng tôi là truy vấn nhỏ
    public async Task<ApprovalStats> GetApprovalStatsAsync(int userId)
    {
        var (_, scope) = await ScopeAsync(userId);
        var today = DateTime.Today;

        // Hàng chờ của người duyệt không gồm hồ sơ do chính họ tạo (Maker không tự duyệt – ERR_SOD_01)
        // => cache theo chi nhánh + người duyệt (người duyệt ít nên số khóa cache nhỏ)
        var branch = await _cache.GetOrCreateAsync($"approvals-branch:{scope}:{userId}", async () =>
        {
            var lateBefore = DateTime.Now.AddHours(-LateHours);
            var pending = Contracts(scope).Where(c => c.Status == ContractStatuses.PendingApproval && c.CreatedBy != userId);
            return new ApprovalStats
            {
                Pending = await pending.CountAsync(),
                Late = await pending.CountAsync(c => c.UpdatedAt < lateBefore),
                PendingValue = await pending.SumAsync(c => c.ContractValue ?? 0),
                Decided = await Decisions().CountAsync()
            };
        });

        return new ApprovalStats
        {
            Pending = branch.Pending,
            Late = branch.Late,
            PendingValue = branch.PendingValue,
            Decided = branch.Decided,
            ApprovedToday = await _context.ContractStatusHistories.AsNoTracking().CountAsync(h =>
                h.NewStatus == ContractStatuses.Active && h.ChangedAt >= today && h.ChangedBy == userId)
        };
    }

    private IQueryable<ContractStatusHistory> Decisions() =>
        _context.ContractStatusHistories.AsNoTracking()
            .Where(h => h.NewStatus == ContractStatuses.Active || h.NewStatus == ContractStatuses.Rejected);

    // =====================================================
    // THÔNG BÁO (chuông trên thanh trên cùng)
    // =====================================================
    public async Task<NotificationSummary> GetNotificationsAsync(int userId)
    {
        var (actor, scope) = await ScopeAsync(userId);

        // Người duyệt: hồ sơ chờ duyệt của chi nhánh mình, trừ hồ sơ do chính họ tạo (không được tự duyệt);
        // người khác: mọi hồ sơ đang chờ duyệt
        var approver = actor.IsApprover();
        var branch = approver ? scope : null;
        var exclude = approver ? userId : 0;
        return await _cache.GetOrCreateAsync($"notifications:{branch}:{exclude}", async () =>
        {
            var pending = Contracts(branch).Where(c => c.Status == ContractStatuses.PendingApproval && c.CreatedBy != exclude);
            return new NotificationSummary
            {
                Count = await pending.CountAsync(),
                Items = await pending.OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.ContractId)
                    .Take(5).Select(ContractService.ToListItem).ToListAsync()
            };
        });
    }

    // =====================================================
    // BÁO CÁO
    // =====================================================
    public async Task<ReportResponse> GetReportAsync(int? branchId, int days) =>
        await _cache.GetOrCreateAsync($"report:{branchId}:{days}", () => BuildReportAsync(branchId, days));

    private async Task<ReportResponse> BuildReportAsync(int? branchId, int days)
    {
        var list = Contracts(branchId);
        if (days > 0)
        {
            var from = DateTime.Now.AddDays(-days);
            list = list.Where(c => c.CreatedAt >= from);
        }

        string[] decidedStatuses = [ContractStatuses.Active, ContractStatuses.Rejected, ContractStatuses.Expired, ContractStatuses.Terminated];
        string[] approvedStatuses = [ContractStatuses.Active, ContractStatuses.Expired, ContractStatuses.Terminated];

        var count = await list.CountAsync();
        var decided = await list.CountAsync(c => decidedStatuses.Contains(c.Status));
        var approved = await list.CountAsync(c => approvedStatuses.Contains(c.Status));

        // Từ lần "gửi duyệt" đầu tiên tới quyết định đầu tiên (phút), trung bình trong SQL
        var contractIds = list.Select(c => c.ContractId);
        var avgMinutes = await _context.ContractStatusHistories.AsNoTracking()
            .Where(h => contractIds.Contains(h.ContractId))
            .GroupBy(h => h.ContractId)
            .Select(g => new
            {
                Sub = g.Where(h => h.NewStatus == ContractStatuses.PendingApproval).Min(h => (DateTime?)h.ChangedAt),
                Dec = g.Where(h => h.NewStatus == ContractStatuses.Active || h.NewStatus == ContractStatuses.Rejected).Min(h => (DateTime?)h.ChangedAt)
            })
            .Where(x => x.Sub != null && x.Dec != null && x.Dec >= x.Sub)
            .Select(x => (double?)EF.Functions.DateDiffMinute(x.Sub, x.Dec))
            .AverageAsync();

        var byType = await list
            .GroupBy(c => c.ContractType!.TypeName)
            .Select(g => new NameValueItem { Name = g.Key, Value = g.Sum(c => c.ContractValue ?? 0) })
            .ToListAsync();

        var branchCounts = await list
            .GroupBy(c => c.BranchId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();
        var branches = await _context.Branches.AsNoTracking()
            .Where(b => b.Status == "ACTIVE")
            .OrderBy(b => b.BranchCode)
            .Select(b => new { b.BranchId, b.BranchName })
            .ToListAsync();

        var staff = await list
            .GroupBy(c => new { c.CreatedBy, c.Creator!.FullName })
            .Select(g => new StaffReportRow
            {
                UserId = g.Key.CreatedBy,
                Name = g.Key.FullName,
                Total = g.Count(),
                Active = g.Count(c => c.Status == ContractStatuses.Active),
                Pending = g.Count(c => c.Status == ContractStatuses.PendingApproval),
                Rejected = g.Count(c => c.Status == ContractStatuses.Rejected),
                Value = g.Sum(c => c.Status == ContractStatuses.Active ? c.ContractValue ?? 0 : 0)
            })
            .ToListAsync();

        return new ReportResponse
        {
            Count = count,
            TotalValue = await list.SumAsync(c => c.ContractValue ?? 0),
            Approved = approved,
            Decided = decided,
            ApprovalRate = decided == 0 ? 0 : (int)Math.Round(approved * 100.0 / decided),
            AvgApprovalHours = (avgMinutes ?? 0) / 60.0,
            Monthly = await MonthlyNewAsync(list),
            StatusCounts = await StatusCountsAsync(list),
            ByType = byType.Where(t => t.Value > 0).OrderByDescending(t => t.Value).ToList(),
            ByBranch = branches
                .Select(b => new NameValueItem
                {
                    Name = b.BranchName,
                    Value = branchCounts.Where(x => x.Key == b.BranchId).Sum(x => x.Count)
                })
                .ToList(),
            Staff = staff.OrderByDescending(s => s.Value).ThenByDescending(s => s.Total).ToList()
        };
    }

    // =====================================================
    // SUMMARY (API cũ /api/Dashboard/summary, giữ nguyên dạng trả về)
    // =====================================================
    public async Task<DashboardSummaryResponse> GetSummaryAsync(
        int? branchId,
        int expiringDays) =>
        await _cache.GetOrCreateAsync($"summary:{branchId}:{expiringDays}", () => BuildSummaryAsync(branchId, expiringDays));

    private async Task<DashboardSummaryResponse> BuildSummaryAsync(
        int? branchId,
        int expiringDays)
    {
        var today = DateTime.Today;
        var expiringLimit = today.AddDays(expiringDays);

        var contracts = Contracts(branchId);

        var byStatusRaw = await contracts
            .GroupBy(c => c.Status)
            .Select(g => new { g.Key, Count = g.Count(), Value = g.Sum(c => c.ContractValue ?? 0) })
            .ToListAsync();
        int CountOf(string s) => byStatusRaw.Where(x => x.Key == s).Sum(x => x.Count);

        var expiringQuery = contracts
            .Where(c => c.Status == ContractStatuses.Active &&
                        c.ExpiryDate >= today &&
                        c.ExpiryDate <= expiringLimit);

        var approvedCount = CountOf(ContractStatuses.Active) + CountOf(ContractStatuses.Expired) + CountOf(ContractStatuses.Terminated);
        var rejectedCount = CountOf(ContractStatuses.Rejected);

        var applicationQuery = _context.Applications.AsNoTracking();
        var documentQuery = _context.Documents.AsNoTracking();
        if (branchId is > 0)
        {
            applicationQuery = applicationQuery.Where(a => a.BranchId == branchId);
            documentQuery = documentQuery.Where(d => d.Application != null && d.Application.BranchId == branchId);
        }

        var kpi = new DashboardKpi
        {
            TotalContracts = byStatusRaw.Sum(x => x.Count),
            ActiveContracts = CountOf(ContractStatuses.Active),
            PendingApproval = CountOf(ContractStatuses.PendingApproval),
            ExpiringSoon = await expiringQuery.CountAsync(),
            Overdue = await contracts.CountAsync(c => c.Status == ContractStatuses.Active && c.ExpiryDate < today),
            ActiveValue = byStatusRaw.Where(x => x.Key == ContractStatuses.Active).Sum(x => x.Value),
            TotalApplications = await applicationQuery.CountAsync(),
            TotalDocuments = await documentQuery.CountAsync(),
            PendingDocuments = await documentQuery.CountAsync(d => d.VerificationStatus == "PENDING"),
            TotalCustomers = await applicationQuery.Select(a => a.CustomerId).Distinct().CountAsync(),
            ApprovalRate = approvedCount + rejectedCount == 0
                ? 0
                : Math.Round(approvedCount * 100.0 / (approvedCount + rejectedCount), 1)
        };

        // Theo trạng thái: luôn trả đủ các trạng thái để biểu đồ ổn định
        var byStatus = ContractStatuses.All
            .Select(s => new CountItem { Key = s, Count = CountOf(s) })
            .ToList();

        var byType = await contracts
            .GroupBy(c => c.ContractType != null ? c.ContractType.TypeName : "Khác")
            .Select(g => new GroupValueItem { Name = g.Key, Count = g.Count(), Value = g.Sum(c => c.ContractValue ?? 0) })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

        var byBranch = await contracts
            .GroupBy(c => c.Branch != null ? c.Branch.BranchName : "Khác")
            .Select(g => new GroupValueItem { Name = g.Key, Count = g.Count(), Value = g.Sum(c => c.ContractValue ?? 0) })
            .OrderByDescending(g => g.Value)
            .ToListAsync();

        // Chỉ tính hợp đồng đã ký (có ngày ký); nháp/chờ duyệt/từ chối/hủy chưa ký nên không tính
        var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
        var monthlyRows = await contracts
            .Where(c => c.SignedDate >= firstMonth)
            .GroupBy(c => new { c.SignedDate!.Value.Year, c.SignedDate.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count(), Value = g.Sum(c => c.ContractValue ?? 0) })
            .ToListAsync();
        var monthly = Enumerable.Range(0, 12)
            .Select(i => firstMonth.AddMonths(i))
            .Select(m => new MonthlyItem
            {
                Month = m.ToString("yyyy-MM"),
                Count = monthlyRows.Where(r => r.Year == m.Year && r.Month == m.Month).Sum(r => r.Count),
                Value = monthlyRows.Where(r => r.Year == m.Year && r.Month == m.Month).Sum(r => r.Value)
            })
            .ToList();

        var expiringItems = (await expiringQuery
            .OrderBy(c => c.ExpiryDate)
            .Take(10)
            .Select(c => new
            {
                c.ContractId,
                c.ContractCode,
                CustomerName = c.Application!.Customer!.FullName,
                BranchName = c.Branch!.BranchName,
                c.ExpiryDate,
                c.ContractValue
            })
            .ToListAsync())
            .Select(c => new ExpiringContractItem
            {
                ContractId = c.ContractId,
                ContractCode = c.ContractCode,
                CustomerName = c.CustomerName,
                BranchName = c.BranchName,
                ExpiryDate = c.ExpiryDate!.Value,
                DaysLeft = (c.ExpiryDate.Value.Date - today).Days,
                ContractValue = c.ContractValue
            })
            .ToList();

        var historyQuery = _context.ContractStatusHistories.AsNoTracking();
        if (branchId is > 0)
        {
            historyQuery = historyQuery.Where(h => h.Contract!.BranchId == branchId);
        }

        var recent = await historyQuery
            .OrderByDescending(h => h.ChangedAt)
            .Take(8)
            .Select(h => new RecentActivityItem
            {
                ContractId = h.ContractId,
                ContractCode = h.Contract != null ? h.Contract.ContractCode : string.Empty,
                OldStatus = h.OldStatus,
                NewStatus = h.NewStatus,
                ChangedByName = h.Changer != null ? h.Changer.FullName : null,
                ChangedAt = h.ChangedAt,
                Reason = h.Reason
            })
            .ToListAsync();

        return new DashboardSummaryResponse
        {
            Kpi = kpi,
            ByStatus = byStatus,
            ByContractType = byType,
            ByBranch = byBranch,
            Monthly = monthly,
            ExpiringSoon = expiringItems,
            RecentActivities = recent
        };
    }
}
