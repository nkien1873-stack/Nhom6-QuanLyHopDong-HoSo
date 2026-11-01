using QLHopDongHoSo.API.DTOs.Contracts;

namespace QLHopDongHoSo.API.DTOs.Dashboard;

// Số hợp đồng ký mới trong tháng, tách Tín dụng / Tiền gửi & dịch vụ (biểu đồ cột)
public class MonthlyNewItem
{
    public string Label { get; set; } = string.Empty;
    public int Credit { get; set; }
    public int Deposit { get; set; }
}

// GET /api/Dashboard/overview – màn Tổng quan
public class DashboardOverview
{
    public int Total { get; set; }
    public int Customers { get; set; }
    public int Pending { get; set; }

    // Hồ sơ chờ duyệt thuộc chi nhánh của tôi (Admin: tất cả)
    public int MyQueue { get; set; }

    // Chờ duyệt quá 48 giờ
    public int OverduePending { get; set; }
    public int Expiring { get; set; }

    // Hiệu lực và chưa quá hạn (gồm sắp hết hạn)
    public int ActiveCount { get; set; }
    public decimal ActiveValue { get; set; }

    // Theo trạng thái hiển thị: DRAFT, PENDING, ACTIVE (chưa tới 30 ngày cuối), EXPIRING, REJECTED, EXPIRED, TERMINATED, CANCELLED
    public Dictionary<string, int> StatusCounts { get; set; } = [];
    public List<MonthlyNewItem> Monthly { get; set; } = [];
    public List<ContractListItem> Recent { get; set; } = [];
    public List<ContractListItem> Queue { get; set; } = [];
    public List<ContractListItem> ExpiringSoon { get; set; } = [];
    public List<ContractListItem> MyDrafts { get; set; } = [];
}

// GET /api/Dashboard/approvals – thẻ thống kê màn Phê duyệt (theo chi nhánh của người duyệt)
public class ApprovalStats
{
    public int Pending { get; set; }
    public int Late { get; set; }
    public int ApprovedToday { get; set; }
    public decimal PendingValue { get; set; }
    public int Decided { get; set; }
}

// GET /api/Dashboard/notifications – chuông thông báo
public class NotificationSummary
{
    public int Count { get; set; }
    public List<ContractListItem> Items { get; set; } = [];
}

public class NameValueItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class StaffReportRow
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Active { get; set; }
    public int Pending { get; set; }
    public int Rejected { get; set; }
    public decimal Value { get; set; }
}

// GET /api/Dashboard/report?branchId=&days= – màn Báo cáo
public class ReportResponse
{
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
    public int Approved { get; set; }
    public int Decided { get; set; }
    public int ApprovalRate { get; set; }

    // Thời gian từ lúc gửi duyệt tới lúc có quyết định (giờ)
    public double AvgApprovalHours { get; set; }
    public List<MonthlyNewItem> Monthly { get; set; } = [];
    public Dictionary<string, int> StatusCounts { get; set; } = [];
    public List<NameValueItem> ByType { get; set; } = [];
    public List<NameValueItem> ByBranch { get; set; } = [];
    public List<StaffReportRow> Staff { get; set; } = [];
}
