using QLHopDongHoSo.API.DTOs.Contracts;

namespace QLHopDongHoSo.API.Services;

public interface IContractService
{
    Task<List<ContractResponse>> GetAllAsync();

    Task<ContractResponse?> GetByIdAsync(int id);

    Task<ContractResponse> CreateAsync(
        ContractCreateRequest request,
        int userId);

    Task<ContractResponse?> UpdateAsync(
        int id,
        ContractUpdateRequest request);

    Task<bool> DeleteAsync(int id);
    Task<ContractSearchResponse> SearchAsync(
    ContractSearchRequest request);
}