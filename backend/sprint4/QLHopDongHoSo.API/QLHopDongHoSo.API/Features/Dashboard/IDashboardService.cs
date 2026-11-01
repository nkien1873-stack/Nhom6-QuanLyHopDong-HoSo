using QLHopDongHoSo.API.DTOs.Dashboard;

namespace QLHopDongHoSo.API.Services;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(
        int? branchId,
        int expiringDays);

    Task<DashboardOverview> GetOverviewAsync(int userId);

    Task<ApprovalStats> GetApprovalStatsAsync(int userId);

    Task<NotificationSummary> GetNotificationsAsync(int userId);

    Task<ReportResponse> GetReportAsync(int? branchId, int days);
}
