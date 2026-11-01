using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.DTOs;
using QLHopDongHoSo.API.DTOs.Contracts;
using QLHopDongHoSo.API.Models;

namespace QLHopDongHoSo.API.Services;

public class ContractService : IContractService
{
    // Hợp đồng hiệu lực còn ≤ 30 ngày => "Sắp hết hạn" (khớp giao diện)
    public const int ExpiringDays = 30;

    public const int ExportLimit = 10_000;

    // So khớp tên không phân biệt dấu: "nguyen" tìm được "Nguyễn"
    private const string AccentInsensitive = "Latin1_General_CI_AI";

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IDocumentComplianceService _compliance;
    private readonly IAuditLogger _audit;

    public ContractService(ApplicationDbContext context, IMemoryCache cache, IDocumentComplianceService compliance, IAuditLogger audit)
    {
        _context = context;
        _cache = cache;
        _compliance = compliance;
        _audit = audit;
    }

    // Chỉ lấy đúng các cột cần hiển thị (SQL tự JOIN), không nạp lịch sử/tài liệu vào bộ nhớ
    // Trạng thái hiển thị: Hiệu lực còn ≤ 30 ngày => EXPIRING, quá ngày hết hạn => EXPIRED (tính trong SQL)
    internal static Expression<Func<Contract, string>> DisplayKey(DateTime today, DateTime limit) =>
        c => c.Status == ContractStatuses.Active
            ? (c.ExpiryDate < today ? ContractStatuses.Expired : c.ExpiryDate <= limit ? "EXPIRING" : ContractStatuses.Active)
            : c.Status;

    internal static readonly Expression<Func<Contract, ContractListItem>> ToListItem = c => new ContractListItem
    {
        ContractId = c.ContractId,
        ContractCode = c.ContractCode,
        ApplicationId = c.ApplicationId,
        CustomerId = c.Application!.CustomerId,
        CustomerName = c.Application.Customer!.FullName,
        CustomerKind = c.Application.Customer.CustomerType == "CORPORATE" ? "DN" : "CN",
        CustomerIdNo = c.Application.Customer.IdentityNumber,
        ContractTypeId = c.ContractTypeId,
        ContractTypeName = c.ContractType!.TypeName,
        BranchId = c.BranchId,
        BranchName = c.Branch!.BranchName,
        CreatedBy = c.CreatedBy,
        CreatedByName = c.Creator!.FullName,
        ContractValue = c.ContractValue,
        TermMonths = c.TermMonths,
        SignedDate = c.SignedDate,
        EffectiveDate = c.EffectiveDate,
        ExpiryDate = c.ExpiryDate,
        Status = c.Status,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        DocumentCount = c.Application.Documents.Count()
    };

    private static readonly Expression<Func<Contract, ContractResponse>> ToResponse = c => new ContractResponse
    {
        ContractId = c.ContractId,
        ContractCode = c.ContractCode,
        ApplicationId = c.ApplicationId,
        ApplicationCode = c.Application!.ApplicationCode,
        CustomerId = c.Application.CustomerId,
        CustomerName = c.Application.Customer!.FullName,
        CustomerKind = c.Application.Customer.CustomerType == "CORPORATE" ? "DN" : "CN",
        CustomerIdNo = c.Application.Customer.IdentityNumber,
        CustomerRepresentative = c.Application.Customer.Representative,
        CustomerPhone = c.Application.Customer.Phone,
        CustomerEmail = c.Application.Customer.Email,
        CustomerAddress = c.Application.Customer.Address,
        ContractTypeId = c.ContractTypeId,
        ContractTypeName = c.ContractType!.TypeName,
        BranchId = c.BranchId,
        BranchName = c.Branch!.BranchName,
        CreatedBy = c.CreatedBy,
        CreatedByUsername = c.Creator!.Username,
        CreatedByName = c.Creator.FullName,
        SignedDate = c.SignedDate,
        EffectiveDate = c.EffectiveDate,
        ExpiryDate = c.ExpiryDate,
        ContractValue = c.ContractValue,
        TermMonths = c.TermMonths,
        InterestRate = c.InterestRate,
        Purpose = c.Purpose,
        Collateral = c.Collateral,
        Status = c.Status,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        DocumentCount = c.Application.Documents.Count(),
        RowVersionBytes = c.RowVersion
    };

    // =========================
    // LIST (lọc + phân trang)
    // =========================
    public async Task<PagedResult<ContractListItem>> ListAsync(ContractListQuery query, int userId)
    {
        var (page, pageSize) = PagedResult<ContractListItem>.Normalize(query.Page, query.PageSize);
        var today = DateTime.Today;
        var limit = today.AddDays(ExpiringDays);

        var filtered = await FilteredAsync(query, userId);

        // Số hồ sơ theo từng tab (cùng bộ lọc, chưa lọc tab) – một câu GROUP BY, cache tới khi có thao tác ghi
        var countsKey = $"contract-counts:{query.Q?.Trim()}:{query.TypeId}:{query.BranchId}:{query.CustomerId}:{(query.Mine ? userId : 0)}:{(query.NotMine ? -userId : 0)}:{today:yyyyMMdd}";
        var counts = await _cache.GetOrCreateAsync(countsKey, async () =>
        {
            var groups = await filtered
                .GroupBy(DisplayKey(today, limit))
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync();
            var byKey = groups.ToDictionary(g => g.Key, g => g.Count);
            int Get(string key) => byKey.GetValueOrDefault(key);

            return new Dictionary<string, int>
            {
                ["ALL"] = groups.Sum(g => g.Count),
                ["PENDING"] = Get(ContractStatuses.PendingApproval),
                ["ACTIVE"] = Get("ACTIVE") + Get("EXPIRING"),
                ["EXPIRING"] = Get("EXPIRING"),
                ["DRAFT"] = Get(ContractStatuses.Draft),
                ["REJECTED"] = Get(ContractStatuses.Rejected),
                ["EXPIRED"] = Get("EXPIRED"),
                ["TERMINATED"] = Get(ContractStatuses.Terminated),
                ["CANCELLED"] = Get(ContractStatuses.Cancelled)
            };
        });

        var tabbed = ApplyTab(filtered, query.Tab, today, limit);
        // Tổng của tab lấy luôn từ số đếm ở trên (đỡ một lần quét bảng)
        var tabKey = (query.Tab ?? "ALL").Trim().ToUpperInvariant();
        var total = counts.TryGetValue(tabKey == ContractStatuses.PendingApproval ? "PENDING" : tabKey == "" ? "ALL" : tabKey, out var n)
            ? n
            : await tabbed.CountAsync();

        var items = await Sorted(tabbed, query.Sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToListItem)
            .ToListAsync();

        return new PagedResult<ContractListItem>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            Counts = counts
        };
    }

    public async Task<List<ContractListItem>> ExportAsync(ContractListQuery query, int userId)
    {
        var today = DateTime.Today;
        var tabbed = ApplyTab(await FilteredAsync(query, userId), query.Tab, today, today.AddDays(ExpiringDays));

        return await Sorted(tabbed, query.Sort)
            .Take(ExportLimit)
            .Select(ToListItem)
            .ToListAsync();
    }

    private async Task<IQueryable<Contract>> FilteredAsync(ContractListQuery q, int userId)
    {
        var query = _context.Contracts.AsNoTracking();

        if (q.TypeId is > 0)
        {
            query = query.Where(c => c.ContractTypeId == q.TypeId);
        }

        if (q.BranchId is > 0)
        {
            query = query.Where(c => c.BranchId == q.BranchId);
        }

        if (q.CustomerId is > 0)
        {
            query = query.Where(c => c.Application!.CustomerId == q.CustomerId);
        }

        if (q.Mine)
        {
            query = query.Where(c => c.CreatedBy == userId);
        }

        if (q.NotMine)
        {
            query = query.Where(c => c.CreatedBy != userId);
        }

        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var keyword = q.Q.Trim();
            // Tìm khách hàng khớp trước (bảng nhỏ) rồi ghép 2 truy vấn đơn giản bằng UNION thay vì một điều kiện OR:
            // so khớp không dấu chỉ chạy trên bảng khách hàng, không chạy lại trên từng dòng hợp đồng
            // (20.000 hợp đồng: 1,6 s => vài chục ms)
            var customerIds = await MatchingCustomerIds(_context, keyword).ToListAsync();
            var key = SearchText.Key(keyword);
            var byCode = query.Where(c => EF.Functions.Collate(c.ContractCode.ToUpper(), SearchText.Binary).Contains(key));
            // Không có khách hàng nào khớp => chỉ tìm theo mã (bỏ nhánh UNION rỗng làm SQL chọn kế hoạch chậm)
            query = customerIds.Count == 0
                ? byCode
                : byCode.Union(query.Where(c => customerIds.Contains(c.Application!.CustomerId)));
        }

        return query;
    }

    // Khách hàng có tên (không dấu) hoặc CCCD/MST chứa từ khóa – dùng làm subquery IN (...)
    internal static IQueryable<int> MatchingCustomerIds(ApplicationDbContext context, string keyword) =>
        context.Customers
            .Where(cu => EF.Functions.Collate(cu.FullName, AccentInsensitive).Contains(keyword) ||
                         (cu.IdentityNumber != null && cu.IdentityNumber.Contains(keyword)))
            .Select(cu => cu.CustomerId);

    private static IQueryable<Contract> ApplyTab(IQueryable<Contract> query, string? tab, DateTime today, DateTime limit)
    {
        var key = (tab ?? "ALL").Trim().ToUpperInvariant();
        return key switch
        {
            "" or "ALL" => query,
            "PENDING" or ContractStatuses.PendingApproval => query.Where(c => c.Status == ContractStatuses.PendingApproval),
            "ACTIVE" => query.Where(c => c.Status == ContractStatuses.Active && (c.ExpiryDate == null || c.ExpiryDate >= today)),
            "EXPIRING" => query.Where(c => c.Status == ContractStatuses.Active && c.ExpiryDate >= today && c.ExpiryDate <= limit),
            "EXPIRED" => query.Where(c => c.Status == ContractStatuses.Expired ||
                                          (c.Status == ContractStatuses.Active && c.ExpiryDate < today)),
            _ when ContractStatuses.IsValid(key) => query.Where(c => c.Status == key),
            _ => throw new ArgumentException($"Tab trạng thái '{tab}' không hợp lệ.")
        };
    }

    private static IQueryable<Contract> Sorted(IQueryable<Contract> query, string? sort) =>
        (sort ?? "updated").Trim().ToLowerInvariant() switch
        {
            "created" => query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.ContractId),
            "amount" => query.OrderBy(c => c.ContractValue == null).ThenByDescending(c => c.ContractValue).ThenByDescending(c => c.ContractId),
            "oldest" => query.OrderBy(c => c.UpdatedAt).ThenBy(c => c.ContractId),
            _ => query.OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.ContractId)
        };

    // =========================
    // GET BY ID
    // =========================
    public async Task<ContractResponse?> GetByIdAsync(int id)
    {
        return await _context.Contracts
            .AsNoTracking()
            .Where(c => c.ContractId == id)
            .Select(ToResponse)
            .FirstOrDefaultAsync();
    }

    // =========================
    // CREATE
    // =========================
    // Hồ sơ (Application), hợp đồng và lịch sử khởi tạo lưu trong MỘT transaction:
    // lỗi giữa chừng thì không còn "hồ sơ mồ côi" không có hợp đồng.
    public async Task<ContractResponse> CreateAsync(
        ContractCreateRequest request,
        int userId)
    {
        var actor = await _context.GetActorAsync(userId);

        var manualCode = string.IsNullOrWhiteSpace(request.ContractCode) ? null : request.ContractCode.Trim();
        ValidateCommon(manualCode ?? "HD-AUTO", request.ContractValue, request.TermMonths, request.InterestRate);
        ValidateText(Clean(request.Purpose), Clean(request.Collateral));

        if (manualCode != null && await _context.Contracts.AnyAsync(c => c.ContractCode == manualCode))
        {
            throw new BusinessRuleException("Mã hợp đồng đã tồn tại.");
        }

        var contractType = request.ContractTypeId > 0
            ? await _context.ContractTypes.FindAsync(request.ContractTypeId)
            : null;
        if (contractType == null)
        {
            throw new ArgumentException("Loại hợp đồng không tồn tại.");
        }

        Application? existingApplication = null;
        int branchId;

        if (request.ApplicationId > 0)
        {
            // Gắn vào hồ sơ có sẵn: hợp đồng thuộc chi nhánh của hồ sơ
            existingApplication = await _context.Applications
                .FirstOrDefaultAsync(a => a.ApplicationId == request.ApplicationId)
                ?? throw new ArgumentException("Hồ sơ không tồn tại.");

            branchId = request.BranchId > 0 ? request.BranchId : existingApplication.BranchId;
            if (branchId != existingApplication.BranchId)
            {
                throw new ArgumentException("Chi nhánh của hợp đồng phải trùng chi nhánh của hồ sơ.");
            }
        }
        else
        {
            // Giao diện mới: chọn khách hàng => hệ thống tự mở hồ sơ (Application) cho khách hàng đó
            if (request.CustomerId is not > 0)
            {
                throw new ArgumentException("Vui lòng chọn khách hàng hoặc hồ sơ.");
            }

            if (!await _context.Customers.AnyAsync(c => c.CustomerId == request.CustomerId))
            {
                throw new ArgumentException("Khách hàng không tồn tại.");
            }

            branchId = request.BranchId > 0 ? request.BranchId : actor.BranchId ?? 0;
            if (!await _context.Branches.AnyAsync(b => b.BranchId == branchId && b.Status == "ACTIVE"))
            {
                throw new ArgumentException("Chi nhánh không tồn tại.");
            }
        }

        actor.EnsureCanWriteBranch(branchId);

        // Cấp mã TRƯỚC khi ghi: lúc này request chưa giữ kết nối/transaction nào, nên bộ đếm (kết nối riêng,
        // tự commit) không bao giờ phải chờ kết nối thứ hai trong lúc đang giữ kết nối thứ nhất
        // (trước đây ~200 lệnh tạo cùng lúc làm cạn pool 100 kết nối => cả hệ thống treo 15 giây).
        var applicationCode = existingApplication == null ? await _context.NextApplicationCodeAsync() : null;
        var contractCode = manualCode ?? await _context.NextContractCodeAsync();

        var now = DateTime.Now;
        var application = existingApplication ?? new Application
        {
            ApplicationCode = applicationCode!,
            CustomerId = request.CustomerId!.Value,
            ApplicationType = contractType.TypeName.Replace("Hợp đồng ", "Hồ sơ "),
            AssignedTo = userId,
            BranchId = branchId,
            Status = "DRAFT",
            Priority = "NORMAL",
            CreatedAt = now
        };

        var contract = new Contract
        {
            ContractCode = contractCode,
            Application = application,
            ContractTypeId = contractType.ContractTypeId,
            BranchId = branchId,
            CreatedBy = userId,
            SignedDate = request.SignedDate,
            EffectiveDate = request.EffectiveDate ?? request.StartDate,
            ExpiryDate = request.ExpiryDate ?? request.EndDate,
            ContractValue = request.ContractValue,
            TermMonths = request.TermMonths,
            InterestRate = request.InterestRate,
            Purpose = Clean(request.Purpose),
            Collateral = Clean(request.Collateral),
            // Hợp đồng mới luôn ở trạng thái nháp, chuyển trạng thái qua ChangeStatusAsync
            Status = ContractStatuses.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Ghi lại lịch sử trạng thái
        contract.StatusHistories.Add(new ContractStatusHistory
        {
            OldStatus = null,
            NewStatus = contract.Status,
            ChangedBy = userId,
            ChangedAt = now,
            Reason = "Khởi tạo hợp đồng"
        });

        var customerId = existingApplication?.CustomerId ?? request.CustomerId;
        var customerName = await _context.Customers
            .Where(c => c.CustomerId == customerId)
            .Select(c => c.FullName)
            .FirstOrDefaultAsync();
        _audit.Add(userId, AuditActions.Create, "CONTRACTS", contractCode,
            $"Tạo hồ sơ nháp – {contractType.TypeName}, khách hàng {customerName}");

        // Hồ sơ + hợp đồng + lịch sử + nhật ký lưu trong MỘT SaveChanges (EF tự bọc transaction) => không có hồ sơ mồ côi
        _context.Contracts.Add(contract);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(contract.ContractId)
            ?? throw new Exception("Không thể lấy hợp đồng vừa tạo.");
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<ContractResponse?> UpdateAsync(
        int id,
        ContractUpdateRequest request,
        int userId)
    {
        // Khóa hợp đồng như trình duyệt / lập tờ trình: đổi TSBĐ chạy cùng lúc với lập tờ trình TSBĐ thì
        // một trong hai phải chờ => tờ trình lập cho tài sản cũ luôn bị hủy hiệu lực
        await using var transaction = await _context.Database.BeginTransactionAsync();
        if ((await _context.LockByIdAsync(id)).Count == 0)
        {
            return null;
        }

        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.ContractId == id);

        if (contract == null)
        {
            return null;
        }

        var actor = await _context.GetActorAsync(userId);
        actor.EnsureCanWriteBranch(contract.BranchId);

        // Form sửa gửi kèm phiên bản lúc mở => người khác đã sửa/gửi duyệt sau đó thì không ghi đè
        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            byte[] version;
            try
            {
                version = Convert.FromBase64String(request.RowVersion);
            }
            catch (FormatException)
            {
                throw new ArgumentException("RowVersion không hợp lệ.");
            }

            if (contract.RowVersion != null && !version.AsSpan().SequenceEqual(contract.RowVersion))
            {
                throw new DbUpdateConcurrencyException(
                    "Hồ sơ đã được người khác cập nhật sau khi bạn mở form sửa.");
            }

            _context.Entry(contract).Property(c => c.RowVersion).OriginalValue = version;
        }

        // Mã để trống => giữ mã cũ
        var contractCode = string.IsNullOrWhiteSpace(request.ContractCode)
            ? contract.ContractCode
            : request.ContractCode.Trim();

        // Gộp: trường có gửi => lấy giá trị gửi (kể cả null = xóa), không gửi => giữ giá trị đang lưu.
        // Kiểm tra hợp lệ trên giá trị SAU KHI gộp (vd chỉ gửi ngày hết hạn vẫn bị so với ngày hiệu lực đang lưu).
        var signedDate = request.Has(nameof(request.SignedDate)) ? request.SignedDate : contract.SignedDate;
        var effectiveDate = request.Has(nameof(request.EffectiveDate)) ? request.EffectiveDate : contract.EffectiveDate;
        var expiryDate = request.Has(nameof(request.ExpiryDate)) ? request.ExpiryDate : contract.ExpiryDate;
        var contractValue = request.Has(nameof(request.ContractValue)) ? request.ContractValue : contract.ContractValue;
        var termMonths = request.Has(nameof(request.TermMonths)) ? request.TermMonths : contract.TermMonths;
        var interestRate = request.Has(nameof(request.InterestRate)) ? request.InterestRate : contract.InterestRate;
        var purpose = request.Has(nameof(request.Purpose)) ? Clean(request.Purpose) : contract.Purpose;
        var collateral = request.Has(nameof(request.Collateral)) ? Clean(request.Collateral) : contract.Collateral;

        ValidateCommon(contractCode, contractValue, termMonths, interestRate);
        if (effectiveDate.HasValue && expiryDate.HasValue && effectiveDate > expiryDate)
        {
            throw new ArgumentException("Ngày hết hạn không được trước ngày hiệu lực.");
        }

        ValidateText(purpose, collateral);

        // Chi nhánh gắn theo hồ sơ nên không đổi khi sửa
        if (request.BranchId > 0 && request.BranchId != contract.BranchId)
        {
            throw new ArgumentException("Không được đổi chi nhánh của hợp đồng (theo chi nhánh của hồ sơ).");
        }

        if (request.ContractTypeId > 0)
        {
            await EnsureContractTypeExistsAsync(request.ContractTypeId);
        }

        // Kiểm tra mã hợp đồng bị trùng
        var duplicateCode = await _context.Contracts
            .AnyAsync(c => c.ContractCode == contractCode && c.ContractId != id);

        if (duplicateCode)
        {
            throw new BusinessRuleException("Mã hợp đồng đã tồn tại.");
        }

        // Chỉ sửa được thông tin khi hợp đồng còn ở nháp hoặc bị từ chối.
        // Trạng thái không đổi ở đây mà đi qua ChangeStatusAsync.
        if (contract.Status != ContractStatuses.Draft &&
            contract.Status != ContractStatuses.Rejected)
        {
            throw new BusinessRuleException(
                "Chỉ được sửa hợp đồng ở trạng thái DRAFT hoặc REJECTED.");
        }

        // N09: tờ trình nợ chứng từ được lập/duyệt cho TSBĐ và loại hợp đồng cũ => đổi thì hết hiệu lực, phải lập lại
        var voidTypes = new List<string>();
        if (!string.Equals(collateral ?? "", contract.Collateral ?? "", StringComparison.OrdinalIgnoreCase))
        {
            voidTypes.Add("COLLATERAL");
        }
        if (request.ContractTypeId > 0 && request.ContractTypeId != contract.ContractTypeId)
        {
            voidTypes.Add("INCOME_PROOF");
        }
        if (voidTypes.Count > 0)
        {
            var stale = await _context.DocumentDeferrals
                .Where(d => d.ContractId == id && voidTypes.Contains(d.DocumentType) && DeferralStatuses.Active.Contains(d.Status))
                .ToListAsync();
            foreach (var d in stale)
            {
                d.Status = DeferralStatuses.Cancelled;
                d.DecisionNote = ((d.DecisionNote ?? "") + " [Hết hiệu lực: hồ sơ đổi tài sản bảo đảm / loại hợp đồng]").Trim();
            }
        }

        contract.ContractCode = contractCode;
        if (request.ContractTypeId > 0) contract.ContractTypeId = request.ContractTypeId;
        contract.SignedDate = signedDate;
        contract.EffectiveDate = effectiveDate;
        contract.ExpiryDate = expiryDate;
        contract.ContractValue = contractValue;
        contract.TermMonths = termMonths;
        contract.InterestRate = interestRate;
        contract.Purpose = purpose;
        contract.Collateral = collateral;
        contract.UpdatedAt = DateTime.Now;
        _audit.Add(userId, AuditActions.Update, "CONTRACTS", contractCode, "Cập nhật thông tin hồ sơ");

        // rowversion lệch (có người vừa sửa/duyệt) => DbUpdateConcurrencyException => 409
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await GetByIdAsync(id);
    }

    // =========================
    // CHANGE STATUS
    // =========================
    // Hai người duyệt/từ chối cùng lúc: cả hai đọc cùng rowversion, người lưu sau bị
    // DbUpdateConcurrencyException (409) và toàn bộ lịch sử/phê duyệt của họ bị hủy theo.
    public async Task<ContractResponse?> ChangeStatusAsync(
        int id,
        ContractStatusChangeRequest request,
        int userId)
    {
        // Khóa hàng hợp đồng tới khi commit: kiểm tra tài liệu (ERR_DOC_01/02) và đổi trạng thái là MỘT bước –
        // xóa / đánh dấu "không hợp lệ" tài liệu cùng lúc phải chờ (T02, T03)
        await using var transaction = await _context.Database.BeginTransactionAsync();
        if ((await _context.LockByIdAsync(id)).Count == 0)
        {
            return null;
        }

        var contract = await _context.Contracts
            .Include(c => c.Application)
            .FirstOrDefaultAsync(c => c.ContractId == id);

        if (contract == null)
        {
            return null;
        }

        var actor = await _context.GetActorAsync(userId);

        var newStatus = request.Status.Trim().ToUpperInvariant();

        if (!ContractStatuses.IsValid(newStatus))
        {
            throw new BusinessRuleException(
                $"Trạng thái '{request.Status}' không hợp lệ.");
        }

        if (!ContractStatuses.CanTransition(contract.Status, newStatus))
        {
            throw new BusinessRuleException(
                $"Không thể chuyển từ {contract.Status} sang {newStatus}.");
        }

        if (ContractStatuses.RequiresApprover(newStatus) && !actor.IsApprover())
        {
            throw new UnauthorizedAccessException(
                "Chỉ Admin hoặc Manager được phê duyệt/kết thúc hợp đồng.");
        }

        actor.EnsureCanWriteBranch(contract.BranchId);

        if (ContractStatuses.RequiresReason(newStatus) &&
            string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException(
                "Vui lòng nhập lý do khi từ chối, thanh lý hoặc hủy hợp đồng.");
        }

        // N09: tài liệu bắt buộc / nợ chứng từ khi trình; Maker ≠ Checker khi phê duyệt, từ chối
        switch (newStatus)
        {
            case ContractStatuses.PendingApproval:
                await _compliance.EnsureCanSubmitAsync(contract);
                break;
            case ContractStatuses.Active:
            case ContractStatuses.Rejected:
                await _compliance.EnsureCanDecideAsync(contract, userId, approving: newStatus == ContractStatuses.Active);
                break;
            // "Hết hạn" chỉ khi đã tới ngày hết hạn; kết thúc sớm phải dùng Thanh lý (bắt buộc lý do)
            case ContractStatuses.Expired when contract.ExpiryDate > DateTime.Today:
                throw new BusinessRuleException(
                    $"Hợp đồng chưa đến ngày hết hạn ({contract.ExpiryDate:dd/MM/yyyy}). Muốn kết thúc sớm hãy dùng Thanh lý (ghi lý do).");
        }

        var now = DateTime.Now;

        _context.ContractStatusHistories.Add(new ContractStatusHistory
        {
            ContractId = contract.ContractId,
            OldStatus = contract.Status,
            NewStatus = newStatus,
            ChangedBy = userId,
            ChangedAt = now,
            Reason = request.Reason
        });

        // Lưu quyết định phê duyệt vào hồ sơ gốc
        if (newStatus == ContractStatuses.Active ||
            newStatus == ContractStatuses.Rejected)
        {
            _context.Approvals.Add(new Approval
            {
                ApplicationId = contract.ApplicationId,
                ApproverId = userId,
                Status = newStatus == ContractStatuses.Active ? "APPROVED" : "REJECTED",
                Comment = request.Reason,
                ApprovedAt = now
            });
        }

        // Phê duyệt: ký hôm nay; chưa có ngày hiệu lực/hết hạn thì tính từ hôm nay theo kỳ hạn
        if (newStatus == ContractStatuses.Active)
        {
            contract.SignedDate ??= DateTime.Today;
            contract.EffectiveDate ??= DateTime.Today;
            if (contract.ExpiryDate == null && contract.TermMonths > 0)
            {
                contract.ExpiryDate = contract.EffectiveDate.Value.AddMonths(contract.TermMonths.Value);
            }
        }

        // Đồng bộ trạng thái hồ sơ (Application) theo hợp đồng
        if (contract.Application != null)
        {
            switch (newStatus)
            {
                case ContractStatuses.PendingApproval:
                    contract.Application.Status = "SUBMITTED";
                    contract.Application.SubmittedAt = now;
                    break;
                case ContractStatuses.Active:
                    contract.Application.Status = "APPROVED";
                    contract.Application.CompletedAt = now;
                    break;
                case ContractStatuses.Rejected:
                case ContractStatuses.Cancelled:
                    contract.Application.Status = "REJECTED";
                    break;
                case ContractStatuses.Draft:
                    contract.Application.Status = "DRAFT";
                    break;
            }
        }

        var (action, text) = newStatus switch
        {
            ContractStatuses.PendingApproval => (AuditActions.Submit, "Gửi phê duyệt"),
            ContractStatuses.Active => (AuditActions.Approve, "Phê duyệt hồ sơ"),
            ContractStatuses.Rejected => (AuditActions.Reject, "Từ chối"),
            _ => (AuditActions.Update, $"Chuyển trạng thái → {newStatus}")
        };
        _audit.Add(userId, action, "CONTRACTS", contract.ContractCode,
            string.IsNullOrWhiteSpace(request.Reason) ? text : $"{text}: {request.Reason}");

        contract.Status = newStatus;
        contract.UpdatedAt = now;

        // UPDATE hợp đồng (kiểm rowversion) + lịch sử + phê duyệt + nhật ký trong cùng transaction đang giữ khóa
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await GetByIdAsync(id);
    }

    // =========================
    // STATUS HISTORY
    // =========================
    public async Task<List<ContractStatusHistoryResponse>> GetHistoryAsync(int id)
    {
        return await _context.ContractStatusHistories
            .AsNoTracking()
            .Where(h => h.ContractId == id)
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.HistoryId)
            .Select(h => new ContractStatusHistoryResponse
            {
                HistoryId = h.HistoryId,
                ContractId = h.ContractId,
                OldStatus = h.OldStatus,
                NewStatus = h.NewStatus,
                ChangedBy = h.ChangedBy,
                ChangedByName = h.Changer != null ? h.Changer.FullName : null,
                ChangedAt = h.ChangedAt,
                Reason = h.Reason
            })
            .ToListAsync();
    }

    public async Task<PagedResult<ContractDecisionItem>> GetDecisionsAsync(bool mine, int userId, int page, int pageSize)
    {
        (page, pageSize) = PagedResult<ContractDecisionItem>.Normalize(page, pageSize);

        var query = _context.ContractStatusHistories
            .AsNoTracking()
            .Where(h => h.NewStatus == ContractStatuses.Active || h.NewStatus == ContractStatuses.Rejected);
        if (mine)
        {
            query = query.Where(h => h.ChangedBy == userId);
        }

        // Đếm cả bảng lịch sử: cache tới khi có thao tác ghi
        var total = await _cache.GetOrCreateAsync($"decisions-count:{(mine ? userId : 0)}", () => query.CountAsync());
        var items = await query
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.HistoryId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new ContractDecisionItem
            {
                HistoryId = h.HistoryId,
                ContractId = h.ContractId,
                ContractCode = h.Contract!.ContractCode,
                CustomerName = h.Contract.Application!.Customer!.FullName,
                ContractValue = h.Contract.ContractValue,
                NewStatus = h.NewStatus,
                Reason = h.Reason,
                ChangedBy = h.ChangedBy,
                ChangedByName = h.Changer!.FullName,
                ChangedAt = h.ChangedAt
            })
            .ToListAsync();

        return new PagedResult<ContractDecisionItem>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        };
    }

    // =========================
    // DELETE
    // =========================
    // Admin xóa được mọi hợp đồng; người khác chỉ xóa bản NHÁP thuộc chi nhánh mình.
    // Hồ sơ đi kèm (tự tạo cùng hợp đồng) bị xóa luôn nếu không còn hợp đồng nào khác.
    public async Task<string?> DeleteAsync(int id, int userId, string storageRoot)
    {
        var filesToDelete = new List<string>();
        string code;

        try
        {
            // Khóa hàng hợp đồng trước (cùng thứ tự với trình duyệt / xóa tài liệu) rồi mới kiểm tra trạng thái
            await using var transaction = await _context.Database.BeginTransactionAsync();
            if ((await _context.LockByIdAsync(id)).Count == 0)
            {
                return null;
            }

            var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.ContractId == id);
            if (contract == null)
            {
                return null;
            }

            var actor = await _context.GetActorAsync(userId);
            if (!actor.IsAdmin())
            {
                if (contract.Status != ContractStatuses.Draft)
                {
                    throw new BusinessRuleException("Chỉ được xóa hợp đồng ở trạng thái Nháp.");
                }

                actor.EnsureCanWriteBranch(contract.BranchId);
            }

            code = contract.ContractCode;
            var applicationId = contract.ApplicationId;

            _context.Contracts.Remove(contract);
            _audit.Add(userId, AuditActions.Delete, "CONTRACTS", code, "Xóa hồ sơ");
            await _context.SaveChangesAsync();

            var otherContracts = await _context.Contracts.AnyAsync(c => c.ApplicationId == applicationId);
            if (!otherContracts)
            {
                // Khóa phạm vi tài liệu của hồ sơ (UPDLOCK, HOLDLOCK) tới khi commit: upload đang chạy song song
                // không chen thêm tài liệu vào giữa lúc lấy danh sách file và lúc xóa hồ sơ (nó sẽ nhận 404 và tự
                // xóa file của mình) => không còn file rác trên đĩa.
                var paths = await _context.Database
                    .SqlQueryRaw<string>(
                        "SELECT [FilePath] AS [Value] FROM [DOCUMENTS] WITH (UPDLOCK, HOLDLOCK) WHERE [ApplicationID] = {0}",
                        applicationId)
                    .ToListAsync();

                var application = await _context.Applications
                    .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

                if (application != null)
                {
                    filesToDelete.AddRange(paths.Select(p =>
                        Path.Combine(storageRoot, p.Replace("/", Path.DirectorySeparatorChar.ToString()))));
                    _context.Applications.Remove(application); // tài liệu xóa theo (cascade)
                    await _context.SaveChangesAsync();
                }
            }

            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Hai lệnh xóa cùng lúc: lệnh sau thấy bản ghi đã mất => 404 thay vì 500
            _context.ChangeTracker.Clear();
            if (!await _context.Contracts.AnyAsync(c => c.ContractId == id))
            {
                return null;
            }

            throw; // vẫn còn nhưng đã đổi (vd vừa được gửi duyệt) => 409
        }

        // Xóa file sau khi DB đã commit (lỗi DB thì file vẫn còn)
        foreach (var path in filesToDelete)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        return code;
    }

    // =========================
    // SEARCH & PAGINATION (API cũ, giữ cho tương thích)
    // =========================
    public async Task<ContractSearchResponse> SearchAsync(ContractSearchRequest request)
    {
        var (page, pageSize) = PagedResult<ContractResponse>.Normalize(request.Page, request.PageSize, 10);

        var query = _context.Contracts.AsNoTracking();

        // 1. Keyword search (ContractCode, TypeName, CustomerName)
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(c =>
                c.ContractCode.Contains(keyword) ||
                c.ContractType!.TypeName.Contains(keyword) ||
                c.Application!.Customer!.FullName.Contains(keyword));
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
            .Select(ToResponse)
            .ToListAsync();

        return new ContractSearchResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Mã tối đa 30 ký tự (cột ContractCode), giá trị không âm, kỳ hạn 1–360 tháng, lãi suất 0–30%,
    // mục đích ≤ 500 và tài sản bảo đảm ≤ 255 ký tự (độ dài cột)
    // Độ dài theo cột DB: báo 400 rõ ràng thay vì để SQL cắt chuỗi lỗi
    private static void ValidateText(string? purpose, string? collateral)
    {
        if (purpose?.Length > 500)
        {
            throw new ArgumentException("Mục đích tối đa 500 ký tự.");
        }

        if (collateral?.Length > 255)
        {
            throw new ArgumentException("Tài sản bảo đảm tối đa 255 ký tự.");
        }
    }

    private static void ValidateCommon(string contractCode, decimal? contractValue, int? termMonths, decimal? interestRate)
    {
        if (string.IsNullOrWhiteSpace(contractCode))
        {
            throw new ArgumentException("Mã hợp đồng không được để trống.");
        }

        if (contractCode.Length > 30)
        {
            throw new ArgumentException("Mã hợp đồng tối đa 30 ký tự.");
        }

        if (contractValue is < 0 or > 9_999_999_999_999_999m)
        {
            throw new ArgumentException("Giá trị hợp đồng không hợp lệ.");
        }

        if (termMonths is < 1 or > 360)
        {
            throw new ArgumentException("Kỳ hạn phải từ 1 đến 360 tháng.");
        }

        if (interestRate is < 0 or > 30)
        {
            throw new ArgumentException("Lãi suất phải từ 0 đến 30 %/năm.");
        }
    }

    private async Task EnsureContractTypeExistsAsync(int contractTypeId)
    {
        if (contractTypeId <= 0 ||
            !await _context.ContractTypes.AnyAsync(t => t.ContractTypeId == contractTypeId))
        {
            throw new ArgumentException("Loại hợp đồng không tồn tại.");
        }
    }
}
