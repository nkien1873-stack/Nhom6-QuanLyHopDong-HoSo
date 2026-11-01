using QLHopDongHoSo.API.DTOs;
using QLHopDongHoSo.API.DTOs.Contracts;

namespace QLHopDongHoSo.API.Services;

public interface IContractService
{
    // Danh sách có lọc + phân trang + số đếm theo tab trạng thái
    Task<PagedResult<ContractListItem>> ListAsync(ContractListQuery query, int userId);

    // Xuất Excel theo bộ lọc hiện tại (tối đa ExportLimit dòng)
    Task<List<ContractListItem>> ExportAsync(ContractListQuery query, int userId);

    Task<ContractResponse?> GetByIdAsync(int id);

    Task<ContractResponse> CreateAsync(
        ContractCreateRequest request,
        int userId);

    Task<ContractResponse?> UpdateAsync(
        int id,
        ContractUpdateRequest request,
        int userId);

    Task<ContractResponse?> ChangeStatusAsync(
        int id,
        ContractStatusChangeRequest request,
        int userId);

    Task<List<ContractStatusHistoryResponse>> GetHistoryAsync(int id);

    // Các quyết định duyệt/từ chối, mới nhất trước (mine = chỉ của tôi)
    Task<PagedResult<ContractDecisionItem>> GetDecisionsAsync(bool mine, int userId, int page, int pageSize);

    // Trả về mã hợp đồng đã xóa, null nếu không tìm thấy
    Task<string?> DeleteAsync(int id, int userId, string storageRoot);

    Task<ContractSearchResponse> SearchAsync(
        ContractSearchRequest request);
}
