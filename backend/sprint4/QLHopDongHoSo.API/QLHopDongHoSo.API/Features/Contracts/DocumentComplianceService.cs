using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.DTOs;
using QLHopDongHoSo.API.DTOs.Contracts;
using QLHopDongHoSo.API.Models;

namespace QLHopDongHoSo.API.Services;

// Ma trận tài liệu theo hồ sơ (N09):
// - BẮT BUỘC (Mandatory): Đơn đề nghị (mọi hồ sơ); CCCD (KH cá nhân) / Giấy phép kinh doanh (KH doanh nghiệp).
//   Thiếu => chặn cứng khi trình duyệt và khi phê duyệt (ERR_DOC_01), không cấp nào được bỏ qua.
// - BỔ SUNG SAU (Deferred): Chứng minh thu nhập (KH cá nhân, hợp đồng tín dụng/vay);
//   Hồ sơ tài sản bảo đảm (khi có TSBĐ – không tính tín chấp / ký quỹ).
//   Thiếu => chỉ được trình khi có Tờ trình xin nợ chứng từ đã được cấp ngoại lệ duyệt (WARN_DOC_02), không thì ERR_DOC_02.
public static class DocumentRequirements
{
    public const string Mandatory = "MANDATORY";
    public const string Deferred = "DEFERRED";

    public static List<(string Type, string Requirement)> For(string? customerType, string? contractTypeName, string? collateral)
    {
        var corporate = string.Equals(customerType, "CORPORATE", StringComparison.OrdinalIgnoreCase);
        var list = new List<(string, string)>
        {
            ("APPLICATION_FORM", Mandatory),
            (corporate ? "BUSINESS_LICENSE" : "CCCD", Mandatory)
        };

        if (!corporate && IsCredit(contractTypeName))
        {
            list.Add(("INCOME_PROOF", Deferred));
        }

        if (IsSecured(collateral))
        {
            list.Add(("COLLATERAL", Deferred));
        }

        return list;
    }

    private static bool IsCredit(string? typeName)
    {
        var name = (typeName ?? "").ToLowerInvariant();
        return name.Contains("tín dụng") || name.Contains("vay");
    }

    // Có tài sản bảo đảm thật (không phải tín chấp / ký quỹ / không có)
    private static bool IsSecured(string? collateral)
    {
        var text = (collateral ?? "").Trim().ToLowerInvariant();
        return text.Length > 0 && !text.StartsWith("tín chấp") && !text.StartsWith("ký quỹ") && !text.StartsWith("không");
    }
}

public static class DeferralStatuses
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";

    // Hết hiệu lực: quá hạn và đã lập tờ trình mới, hoặc hồ sơ đổi TSBĐ / loại hợp đồng sau khi lập
    public const string Cancelled = "CANCELLED";

    public static readonly string[] All = [Pending, Approved, Rejected, Cancelled];

    public static readonly string[] Active = [Pending, Approved];
}

public static class ComplianceCodes
{
    public const string SodSelfApproval = "ERR_SOD_01";
    public const string MissingMandatory = "ERR_DOC_01";
    public const string MissingDeferred = "ERR_DOC_02";
    public const string DocumentDebt = "WARN_DOC_02";
}

public interface IDocumentComplianceService
{
    Task<ContractChecklistResponse?> GetChecklistAsync(int contractId);

    // Trình duyệt: thiếu bắt buộc => ERR_DOC_01; thiếu bổ sung sau chưa có tờ trình duyệt => ERR_DOC_02
    Task EnsureCanSubmitAsync(Contract contract);

    // Phê duyệt / từ chối: Maker không được tự quyết (ERR_SOD_01); phê duyệt còn kiểm lại tài liệu
    Task EnsureCanDecideAsync(Contract contract, int checkerId, bool approving);

    Task<PagedResult<DeferralResponse>> ListDeferralsAsync(DeferralListQuery query);

    Task<DeferralResponse> RequestDeferralAsync(int contractId, DeferralCreateRequest request, int userId);

    Task<DeferralResponse?> DecideDeferralAsync(int deferralId, DeferralDecisionRequest request, int userId);
}

public class DocumentComplianceService : IDocumentComplianceService
{
    // Thời hạn nợ chứng từ tối đa
    public const int MaxDeferralDays = 90;

    private readonly ApplicationDbContext _context;
    private readonly IAuditLogger _audit;

    public DocumentComplianceService(ApplicationDbContext context, IAuditLogger audit)
    {
        _context = context;
        _audit = audit;
    }

    private static readonly System.Linq.Expressions.Expression<Func<DocumentDeferral, DeferralResponse>> ToResponse =
        d => new DeferralResponse
        {
            DeferralId = d.DeferralId,
            ContractId = d.ContractId,
            ContractCode = d.Contract!.ContractCode,
            ContractStatus = d.Contract.Status,
            BranchId = d.Contract.BranchId,
            BranchName = d.Contract.Branch!.BranchName,
            CustomerName = d.Contract.Application!.Customer!.FullName,
            DocumentType = d.DocumentType,
            DueDate = d.DueDate,
            Reason = d.Reason,
            Status = d.Status,
            RequestedBy = d.RequestedBy,
            RequestedByName = d.Requester!.FullName,
            RequestedAt = d.RequestedAt,
            DecidedBy = d.DecidedBy,
            DecidedByName = d.Decider != null ? d.Decider.FullName : null,
            DecidedAt = d.DecidedAt,
            DecisionNote = d.DecisionNote
        };

    // =========================
    // CHECKLIST
    // =========================
    public async Task<ContractChecklistResponse?> GetChecklistAsync(int contractId)
    {
        var info = await _context.Contracts
            .AsNoTracking()
            .Where(c => c.ContractId == contractId)
            .Select(c => new
            {
                c.ContractId,
                c.ContractCode,
                c.ApplicationId,
                CustomerType = c.Application!.Customer!.CustomerType,
                TypeName = c.ContractType!.TypeName,
                c.Collateral
            })
            .FirstOrDefaultAsync();

        if (info == null)
        {
            return null;
        }

        // Tài liệu bị từ chối xác minh coi như chưa có
        var uploaded = await _context.Documents
            .AsNoTracking()
            .Where(d => d.ApplicationId == info.ApplicationId && d.VerificationStatus != "REJECTED")
            .GroupBy(d => d.DocumentType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);

        var deferrals = await _context.DocumentDeferrals
            .AsNoTracking()
            .Where(d => d.ContractId == contractId)
            .OrderByDescending(d => d.RequestedAt)
            .ThenByDescending(d => d.DeferralId)
            .Select(ToResponse)
            .ToListAsync();

        var result = new ContractChecklistResponse { ContractId = info.ContractId, ContractCode = info.ContractCode };

        foreach (var (type, requirement) in DocumentRequirements.For(info.CustomerType, info.TypeName, info.Collateral))
        {
            // Ưu tiên tờ trình còn hiệu lực (chờ duyệt/đã duyệt), không có thì lấy tờ trình gần nhất
            var deferral = deferrals.FirstOrDefault(d => d.DocumentType == type && DeferralStatuses.Active.Contains(d.Status))
                ?? deferrals.FirstOrDefault(d => d.DocumentType == type);

            var item = new ChecklistItem
            {
                DocumentType = type,
                Label = DocumentRequirementLabels.Label(type),
                Requirement = requirement,
                Uploaded = uploaded.GetValueOrDefault(type),
                Deferral = requirement == DocumentRequirements.Deferred ? deferral : null
            };
            result.Items.Add(item);

            if (item.Present)
            {
                continue;
            }

            if (requirement == DocumentRequirements.Mandatory)
            {
                result.MissingMandatory.Add(item.Label);
            }
            else if (deferral is { Status: DeferralStatuses.Approved })
            {
                // Quá hạn: vẫn cảnh báo; không được TRÌNH mới (phải bổ sung hoặc lập tờ trình mới), Checker vẫn quyết định được
                if (deferral.Overdue)
                {
                    result.OverdueDeferred.Add($"{item.Label} (hạn {deferral.DueDate:dd/MM/yyyy})");
                }

                result.Warnings.Add(
                    $"{ComplianceCodes.DocumentDebt}: Hồ sơ đang nợ chứng từ {item.Label}. " +
                    $"Thời hạn hoàn thiện: {deferral.DueDate:dd/MM/yyyy}." +
                    (deferral.Overdue ? " (Đã quá hạn)" : ""));
            }
            else
            {
                result.MissingDeferred.Add(item.Label);
            }
        }

        return result;
    }

    // =========================
    // KIỂM TRA KHI ĐỔI TRẠNG THÁI
    // =========================
    public async Task EnsureCanSubmitAsync(Contract contract)
    {
        var checklist = await GetChecklistAsync(contract.ContractId)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");
        EnsureDocuments(checklist);

        if (checklist.OverdueDeferred.Count > 0)
        {
            throw new BusinessRuleException(
                ComplianceCodes.MissingDeferred,
                $"Tờ trình xin nợ chứng từ {string.Join(", ", checklist.OverdueDeferred)} đã quá thời hạn hoàn thiện. " +
                "Vui lòng upload tài liệu hoặc lập tờ trình mới.");
        }
    }

    public async Task EnsureCanDecideAsync(Contract contract, int checkerId, bool approving)
    {
        // Maker = người tạo hồ sơ và người trình duyệt gần nhất. Không cấp nào được override (kể cả Admin):
        // phải chuyển hồ sơ cho Checker khác theo ma trận phân quyền.
        var lastSubmitter = await _context.ContractStatusHistories
            .AsNoTracking()
            .Where(h => h.ContractId == contract.ContractId && h.NewStatus == ContractStatuses.PendingApproval)
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.HistoryId)
            .Select(h => (int?)h.ChangedBy)
            .FirstOrDefaultAsync();

        if (contract.CreatedBy == checkerId || lastSubmitter == checkerId)
        {
            throw new BusinessRuleException(
                ComplianceCodes.SodSelfApproval,
                "Người tạo hồ sơ không có quyền phê duyệt chính hồ sơ này.");
        }

        if (approving)
        {
            var checklist = await GetChecklistAsync(contract.ContractId)
                ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");
            EnsureDocuments(checklist);
        }
    }

    private static void EnsureDocuments(ContractChecklistResponse checklist)
    {
        if (checklist.MissingMandatory.Count > 0)
        {
            throw new BusinessRuleException(
                ComplianceCodes.MissingMandatory,
                $"Hồ sơ thiếu tài liệu bắt buộc {string.Join(", ", checklist.MissingMandatory)}. " +
                "Vui lòng upload trước khi trình duyệt.");
        }

        if (checklist.MissingDeferred.Count > 0)
        {
            throw new BusinessRuleException(
                ComplianceCodes.MissingDeferred,
                $"Hồ sơ thiếu tài liệu {string.Join(", ", checklist.MissingDeferred)} nhưng chưa có " +
                "Tờ trình xin nợ chứng từ được cấp ngoại lệ (Giám đốc chi nhánh / Trưởng phòng QLRR) phê duyệt. " +
                "Vui lòng upload tài liệu hoặc lập tờ trình.");
        }
    }

    // =========================
    // TỜ TRÌNH XIN NỢ CHỨNG TỪ
    // =========================
    public async Task<PagedResult<DeferralResponse>> ListDeferralsAsync(DeferralListQuery query)
    {
        var (page, pageSize) = PagedResult<DeferralResponse>.Normalize(query.Page, query.PageSize);

        var q = _context.DocumentDeferrals.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToUpperInvariant();
            q = q.Where(d => d.Status == status);
        }

        if (query.BranchId is { } branchId)
        {
            q = q.Where(d => d.Contract!.BranchId == branchId);
        }

        if (query.ContractId is { } contractId)
        {
            q = q.Where(d => d.ContractId == contractId);
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(d => d.RequestedAt)
            .ThenByDescending(d => d.DeferralId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToResponse)
            .ToListAsync();

        return new PagedResult<DeferralResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        };
    }

    public async Task<DeferralResponse> RequestDeferralAsync(int contractId, DeferralCreateRequest request, int userId)
    {
        // Khóa hợp đồng: trình duyệt / sửa TSBĐ chạy cùng lúc phải chờ, trạng thái đọc dưới đây luôn là mới nhất
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.LockByIdAsync(contractId);
        var contract = await _context.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.ContractId == contractId)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

        var actor = await _context.GetActorAsync(userId);
        actor.EnsureCanWriteBranch(contract.BranchId);

        if (contract.Status is not (ContractStatuses.Draft or ContractStatuses.Rejected))
        {
            throw new BusinessRuleException("Chỉ lập tờ trình xin nợ chứng từ cho hồ sơ Nháp hoặc bị Từ chối (trước khi trình duyệt).");
        }

        var type = request.DocumentType.Trim().ToUpperInvariant();
        var checklist = await GetChecklistAsync(contractId)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");
        var item = checklist.Items.FirstOrDefault(i => i.DocumentType == type);

        if (item == null)
        {
            throw new ArgumentException($"Tài liệu {DocumentRequirementLabels.Label(type)} không thuộc danh mục yêu cầu của hồ sơ này.");
        }

        if (item.Requirement == DocumentRequirements.Mandatory)
        {
            throw new BusinessRuleException(
                ComplianceCodes.MissingMandatory,
                $"{item.Label} là tài liệu bắt buộc, không được xin nợ. Vui lòng upload trước khi trình duyệt.");
        }

        if (item.Present)
        {
            throw new BusinessRuleException($"Hồ sơ đã có {item.Label}, không cần xin nợ chứng từ.");
        }

        // Tờ trình đã duyệt nhưng quá hạn => được lập tờ trình mới, tờ trình cũ chuyển "Hết hiệu lực"
        var replaceOverdue = item.Deferral is { Status: DeferralStatuses.Approved, Overdue: true };
        if (!replaceOverdue && item.Deferral is { Status: DeferralStatuses.Pending or DeferralStatuses.Approved } existing)
        {
            throw new BusinessRuleException(existing.Status == DeferralStatuses.Pending
                ? $"Đã có tờ trình xin nợ {item.Label} đang chờ duyệt."
                : $"Tờ trình xin nợ {item.Label} đã được duyệt (hạn {existing.DueDate:dd/MM/yyyy}).");
        }

        var due = request.DueDate!.Value.Date;
        var today = DateTime.Today;
        if (due <= today || due > today.AddDays(MaxDeferralDays))
        {
            throw new ArgumentException($"Thời hạn hoàn thiện phải sau hôm nay và không quá {MaxDeferralDays} ngày.");
        }

        var deferral = new DocumentDeferral
        {
            ContractId = contractId,
            DocumentType = type,
            DueDate = due,
            Reason = request.Reason.Trim(),
            Status = DeferralStatuses.Pending,
            RequestedBy = userId,
            RequestedAt = DateTime.Now
        };
        _context.DocumentDeferrals.Add(deferral);
        _audit.Add(userId, AuditActions.Create, "DEFERRALS", contract.ContractCode,
            $"Lập tờ trình xin nợ {item.Label}, hạn {due:dd/MM/yyyy}");

        if (replaceOverdue)
        {
            var oldId = item.Deferral!.DeferralId;
            await _context.DocumentDeferrals
                .Where(d => d.DeferralId == oldId && d.Status == DeferralStatuses.Approved)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, DeferralStatuses.Cancelled)
                    .SetProperty(d => d.DecisionNote, d => (d.DecisionNote ?? "") + " [Hết hiệu lực: quá hạn, đã lập tờ trình mới]"));
        }

        // Hai người gửi cùng lúc => index unique (đang chờ/đã duyệt) chặn bản thứ hai => 409
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await _context.DocumentDeferrals.AsNoTracking()
            .Where(d => d.DeferralId == deferral.DeferralId)
            .Select(ToResponse)
            .FirstAsync();
    }

    public async Task<DeferralResponse?> DecideDeferralAsync(int deferralId, DeferralDecisionRequest request, int userId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var deferral = await _context.DocumentDeferrals
            .AsNoTracking()
            .Include(d => d.Contract)
            .FirstOrDefaultAsync(d => d.DeferralId == deferralId);

        if (deferral == null)
        {
            return null;
        }

        // Khóa hợp đồng rồi đọc lại trạng thái mới nhất (đang có lệnh trình duyệt chạy song song thì chờ nó xong)
        var locked = await _context.LockByIdAsync(deferral.ContractId);
        var contractStatus = locked.FirstOrDefault()?.Status ?? deferral.Contract!.Status;

        var actor = await _context.GetActorAsync(userId);
        if (!actor.IsDirector())
        {
            throw new UnauthorizedAccessException(
                "Chỉ cấp phê duyệt ngoại lệ (Giám đốc chi nhánh / Trưởng phòng QLRR) được duyệt tờ trình xin nợ chứng từ.");
        }

        if (actor.BranchId != null && actor.BranchId != deferral.Contract!.BranchId)
        {
            throw new UnauthorizedAccessException("Bạn chỉ được duyệt tờ trình của hồ sơ thuộc chi nhánh mình.");
        }

        if (deferral.RequestedBy == userId)
        {
            throw new BusinessRuleException(
                ComplianceCodes.SodSelfApproval,
                "Người lập tờ trình không có quyền phê duyệt chính tờ trình này.");
        }

        var note = request.Note?.Trim();
        if (!request.Approve && string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("Vui lòng nhập lý do khi từ chối tờ trình.");
        }

        if (request.Approve && deferral.DueDate < DateTime.Today)
        {
            throw new BusinessRuleException("Thời hạn hoàn thiện của tờ trình đã qua, Maker cần lập tờ trình mới.");
        }

        // Duyệt chỉ có ý nghĩa khi hồ sơ còn chờ trình (Nháp / Bị từ chối) và tài liệu vẫn còn thiếu;
        // tờ trình "treo" của hồ sơ đã trình / đã hủy / đã đủ tài liệu thì chỉ được từ chối
        if (request.Approve)
        {
            if (contractStatus is not (ContractStatuses.Draft or ContractStatuses.Rejected))
            {
                throw new BusinessRuleException("Hồ sơ không còn ở trạng thái Nháp / Bị từ chối nên không duyệt tờ trình này được (chỉ từ chối).");
            }

            // Đọc lại danh mục hiện tại: tài liệu đã bổ sung, hoặc hồ sơ đã đổi (bỏ TSBĐ, đổi loại hợp đồng / khách hàng)
            // nên loại tài liệu này không còn thuộc "Bổ sung sau" => không còn gì để cho nợ
            var item = (await GetChecklistAsync(deferral.ContractId))?.Items
                .FirstOrDefault(i => i.DocumentType == deferral.DocumentType);
            if (item is not { Requirement: DocumentRequirements.Deferred })
            {
                throw new BusinessRuleException("Tài liệu này không còn thuộc danh mục Bổ sung sau của hồ sơ (chỉ từ chối).");
            }

            if (item.Present)
            {
                throw new BusinessRuleException("Hồ sơ đã có tài liệu này, không cần duyệt nợ chứng từ (chỉ từ chối).");
            }
        }

        var status = request.Approve ? DeferralStatuses.Approved : DeferralStatuses.Rejected;
        var now = DateTime.Now;

        // Chỉ cập nhật khi còn chờ duyệt: hai người duyệt cùng lúc => người sau nhận 409
        var updated = await _context.DocumentDeferrals
            .Where(d => d.DeferralId == deferralId && d.Status == DeferralStatuses.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, status)
                .SetProperty(d => d.DecidedBy, userId)
                .SetProperty(d => d.DecidedAt, now)
                .SetProperty(d => d.DecisionNote, note));

        if (updated == 0)
        {
            throw new BusinessRuleException("Tờ trình đã được xử lý trước đó.");
        }

        _audit.Add(userId, request.Approve ? AuditActions.Approve : AuditActions.Reject, "DEFERRALS", deferral.Contract!.ContractCode,
            $"{(request.Approve ? "Duyệt" : "Từ chối")} tờ trình xin nợ {DocumentRequirementLabels.Label(deferral.DocumentType)}, " +
            $"hạn {deferral.DueDate:dd/MM/yyyy}" + (string.IsNullOrWhiteSpace(note) ? "" : $": {note}"));
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        DataVersion.Bump();

        return await _context.DocumentDeferrals.AsNoTracking()
            .Where(d => d.DeferralId == deferralId)
            .Select(ToResponse)
            .FirstAsync();
    }
}
