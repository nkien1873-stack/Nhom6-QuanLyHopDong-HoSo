using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.DTOs.Contracts;
using QLHopDongHoSo.API.Models;

namespace QLHopDongHoSo.API.Services;

public class ContractService : IContractService
{
    private readonly ApplicationDbContext _context;

    public ContractService(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<ContractResponse>> GetAllAsync()
    {
        return await _context.Contracts
            .Include(c => c.Creator)
            .Include(c => c.Branch)
            .Include(c => c.ContractType)
            .Include(c => c.Application)
                .ThenInclude(a => a!.Customer)
            .Select(c => MapToResponse(c))
            .ToListAsync();
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<ContractResponse?> GetByIdAsync(int id)
    {
        var contract = await _context.Contracts
            .Include(c => c.Creator)
            .Include(c => c.Branch)
            .Include(c => c.ContractType)
            .Include(c => c.Application)
                .ThenInclude(a => a!.Customer)
            .FirstOrDefaultAsync(c => c.ContractId == id);

        return contract != null ? MapToResponse(contract) : null;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<ContractResponse> CreateAsync(
        ContractCreateRequest request,
        int userId)
    {
        // 1. Kiểm tra mã hợp đồng đã tồn tại
        var existingContract = await _context.Contracts
            .AnyAsync(c => c.ContractCode == request.ContractCode);

        if (existingContract)
        {
            throw new InvalidOperationException("Mã hợp đồng đã tồn tại.");
        }

        // 2. Validate ApplicationId & BranchId
        var applicationId = request.ApplicationId;
        if (applicationId == 0)
        {
            var defaultApp = await _context.Applications.FirstOrDefaultAsync();
            if (defaultApp == null)
            {
                throw new InvalidOperationException("Cần có hồ sơ (Application) để tạo hợp đồng.");
            }
            applicationId = defaultApp.ApplicationId;
        }

        var contractTypeId = request.ContractTypeId;
        if (contractTypeId == 0)
        {
            var defaultType = await _context.ContractTypes.FirstOrDefaultAsync();
            contractTypeId = defaultType?.ContractTypeId ?? 1;
        }

        var branchId = request.BranchId;
        if (branchId == 0)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            branchId = user?.BranchId ?? 1;
        }

        var effectiveDate = request.EffectiveDate ?? request.StartDate;
        var expiryDate = request.ExpiryDate ?? request.EndDate;

        var contract = new Contract
        {
            ContractCode = request.ContractCode,
            ApplicationId = applicationId,
            ContractTypeId = contractTypeId,
            BranchId = branchId,
            CreatedBy = userId,
            SignedDate = request.SignedDate,
            EffectiveDate = effectiveDate,
            ExpiryDate = expiryDate,
            ContractValue = request.ContractValue,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "DRAFT" : request.Status,
            CreatedAt = DateTime.UtcNow
        };

        _context.Contracts.Add(contract);
        await _context.SaveChangesAsync();

        // Ghi lại lịch sử trạng thái
        _context.ContractStatusHistories.Add(new ContractStatusHistory
        {
            ContractId = contract.ContractId,
            OldStatus = null,
            NewStatus = contract.Status,
            ChangedBy = userId,
            ChangedAt = DateTime.UtcNow,
            Reason = "Khởi tạo hợp đồng"
        });

        await _context.SaveChangesAsync();

        return await GetByIdAsync(contract.ContractId)
            ?? throw new Exception("Không thể lấy hợp đồng vừa tạo.");
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<ContractResponse?> UpdateAsync(
        int id,
        ContractUpdateRequest request)
    {
        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.ContractId == id);

        if (contract == null)
        {
            return null;
        }

        // Kiểm tra mã hợp đồng bị trùng
        var duplicateCode = await _context.Contracts
            .AnyAsync(c => c.ContractCode == request.ContractCode && c.ContractId != id);

        if (duplicateCode)
        {
            throw new InvalidOperationException("Mã hợp đồng đã tồn tại.");
        }

        var oldStatus = contract.Status;
        var newStatus = string.IsNullOrWhiteSpace(request.Status) ? oldStatus : request.Status;

        if (contract.Status != newStatus)
        {
            _context.ContractStatusHistories.Add(new ContractStatusHistory
            {
                ContractId = contract.ContractId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangedBy = contract.CreatedBy,
                ChangedAt = DateTime.UtcNow,
                Reason = "Cập nhật trạng thái hợp đồng"
            });
        }

        contract.ContractCode = request.ContractCode;
        if (request.ContractTypeId > 0) contract.ContractTypeId = request.ContractTypeId;
        if (request.BranchId > 0) contract.BranchId = request.BranchId;
        contract.SignedDate = request.SignedDate;
        contract.EffectiveDate = request.EffectiveDate ?? request.StartDate;
        contract.ExpiryDate = request.ExpiryDate ?? request.EndDate;
        contract.ContractValue = request.ContractValue ?? contract.ContractValue;
        contract.Status = newStatus;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    // =========================
    // DELETE
    // =========================
    public async Task<bool> DeleteAsync(int id)
    {
        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.ContractId == id);

        if (contract == null)
        {
            return false;
        }

        _context.Contracts.Remove(contract);
        await _context.SaveChangesAsync();

        return true;
    }

    // =========================
    // SEARCH & PAGINATION
    // =========================
    public async Task<ContractSearchResponse> SearchAsync(ContractSearchRequest request)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : (request.PageSize > 100 ? 100 : request.PageSize);

        var query = _context.Contracts
            .Include(c => c.Creator)
            .Include(c => c.Branch)
            .Include(c => c.ContractType)
            .Include(c => c.Application)
                .ThenInclude(a => a!.Customer)
            .AsQueryable();

        // 1. Keyword search (ContractCode, TypeName, CustomerName)
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(c =>
                c.ContractCode.Contains(keyword) ||
                (c.ContractType != null && c.ContractType.TypeName.Contains(keyword)) ||
                (c.Application != null && c.Application.Customer != null && c.Application.Customer.FullName.Contains(keyword)));
        }

        // 2. Status filter
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(c => c.Status == status);
        }

        // 3. Branch filter
        if (request.BranchId.HasValue && request.BranchId > 0)
        {
            query = query.Where(c => c.BranchId == request.BranchId.Value);
        }

        // 4. ContractType filter
        if (request.ContractTypeId.HasValue && request.ContractTypeId > 0)
        {
            query = query.Where(c => c.ContractTypeId == request.ContractTypeId.Value);
        }

        // 5. Date filters
        if (request.StartDate.HasValue)
        {
            query = query.Where(c => c.EffectiveDate >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(c => c.ExpiryDate <= request.EndDate.Value);
        }

        var totalItems = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.ContractId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapToResponse(c))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return new ContractSearchResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    private static ContractResponse MapToResponse(Contract c)
    {
        return new ContractResponse
        {
            ContractId = c.ContractId,
            ContractCode = c.ContractCode,
            ApplicationId = c.ApplicationId,
            ApplicationCode = c.Application != null ? c.Application.ApplicationCode : null,
            CustomerName = c.Application != null && c.Application.Customer != null ? c.Application.Customer.FullName : null,
            ContractTypeId = c.ContractTypeId,
            ContractTypeName = c.ContractType != null ? c.ContractType.TypeName : null,
            BranchId = c.BranchId,
            BranchName = c.Branch != null ? c.Branch.BranchName : null,
            CreatedBy = c.CreatedBy,
            CreatedByUsername = c.Creator != null ? c.Creator.Username : null,
            SignedDate = c.SignedDate,
            EffectiveDate = c.EffectiveDate,
            ExpiryDate = c.ExpiryDate,
            ContractValue = c.ContractValue,
            Status = c.Status,
            CreatedAt = c.CreatedAt
        };
    }
}