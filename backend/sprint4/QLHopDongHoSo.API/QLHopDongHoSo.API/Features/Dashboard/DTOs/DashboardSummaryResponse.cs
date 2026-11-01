namespace QLHopDongHoSo.API.DTOs.Dashboard;

public class DashboardSummaryResponse
{
    public DashboardKpi Kpi { get; set; } = new();

    public List<CountItem> ByStatus { get; set; } = [];

    public List<GroupValueItem> ByContractType { get; set; } = [];

    public List<GroupValueItem> ByBranch { get; set; } = [];

    // Số hợp đồng ký mới và giá trị theo tháng (12 tháng gần nhất)
    public List<MonthlyItem> Monthly { get; set; } = [];

    public List<ExpiringContractItem> ExpiringSoon { get; set; } = [];

    public List<RecentActivityItem> RecentActivities { get; set; } = [];
}

public class DashboardKpi
{
    public int TotalContracts { get; set; }

    public int ActiveContracts { get; set; }

    public int PendingApproval { get; set; }

    public int ExpiringSoon { get; set; }

    // ACTIVE nhưng đã quá ngày hết hạn, cần xử lý
    public int Overdue { get; set; }

    public decimal ActiveValue { get; set; }

    public int TotalApplications { get; set; }

    public int TotalDocuments { get; set; }

    public int PendingDocuments { get; set; }

    public int TotalCustomers { get; set; }

    // Tỉ lệ hợp đồng được duyệt / (được duyệt + bị từ chối), 0–100
    public double ApprovalRate { get; set; }
}

public class CountItem
{
    public string Key { get; set; } = string.Empty;

    public int Count { get; set; }
}

public class GroupValueItem
{
    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }

    public decimal Value { get; set; }
}

public class MonthlyItem
{
    // Định dạng yyyy-MM
    public string Month { get; set; } = string.Empty;

    public int Count { get; set; }

    public decimal Value { get; set; }
}

public class ExpiringContractItem
{
    public int ContractId { get; set; }

    public string ContractCode { get; set; } = string.Empty;

    public string? CustomerName { get; set; }

    public string? BranchName { get; set; }

    public DateTime ExpiryDate { get; set; }

    public int DaysLeft { get; set; }

    public decimal? ContractValue { get; set; }
}

public class RecentActivityItem
{
    public int ContractId { get; set; }

    public string ContractCode { get; set; } = string.Empty;

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = string.Empty;

    public string? ChangedByName { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Reason { get; set; }
}
