using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.DTOs;
using QLHopDongHoSo.API.Models;
using QLHopDongHoSo.API.Services;

namespace QLHopDongHoSo.API.Controllers;

public class CustomerSaveRequest
{
    // DN = doanh nghiệp (CORPORATE), CN = cá nhân (INDIVIDUAL)
    public string Kind { get; set; } = "CN";

    public string Name { get; set; } = string.Empty;

    // CCCD (12 số) hoặc mã số thuế (10/12 số)
    public string IdNo { get; set; } = string.Empty;

    public string? Representative { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Address { get; set; }

    // Phiên bản lúc mở form sửa (base64) – khác bản trong DB => 409
    public string? RowVersion { get; set; }
}

public class CustomerListQuery
{
    [MinLength(2, ErrorMessage = "Từ khóa tìm kiếm cần ít nhất 2 ký tự.")]
    [MaxLength(100, ErrorMessage = "Từ khóa tìm kiếm tối đa 100 ký tự.")]
    public string? Q { get; set; }

    // ALL | DN | CN
    [MaxLength(10)]
    public string? Kind { get; set; }

    // Chi nhánh của hồ sơ gần nhất
    public int? BranchId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public class CustomerItem
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string Kind { get; set; } = "CN";
    public string Name { get; set; } = string.Empty;
    public string? IdNo { get; set; }
    public string? Representative { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? BranchName { get; set; }
    public int ContractCount { get; set; }
    public decimal ActiveValue { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public byte[]? RowVersionBytes { get; set; }

    public string? RowVersion => RowVersionBytes == null ? null : Convert.ToBase64String(RowVersionBytes);
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomerController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogger _audit;
    private readonly IMemoryCache _cache;

    public CustomerController(ApplicationDbContext context, IAuditLogger audit, IMemoryCache cache)
    {
        _context = context;
        _audit = audit;
        _cache = cache;
    }

    // Kèm chi nhánh của hồ sơ gần nhất (khách hàng không gắn cứng chi nhánh), số hồ sơ và giá trị đang hiệu lực
    private static readonly Expression<Func<Customer, CustomerItem>> ToItem = c => new CustomerItem
    {
        CustomerId = c.CustomerId,
        CustomerCode = c.CustomerCode,
        Kind = c.CustomerType == "CORPORATE" ? "DN" : "CN",
        Name = c.FullName,
        IdNo = c.IdentityNumber,
        Representative = c.Representative,
        Phone = c.Phone,
        Email = c.Email,
        Address = c.Address,
        Status = c.Status,
        CreatedAt = c.CreatedAt,
        RowVersionBytes = c.RowVersion,
        BranchName = c.Applications
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.Branch!.BranchName)
            .FirstOrDefault()
    };

    // Số hồ sơ + giá trị hiệu lực của các khách hàng trong trang: một câu GROUP BY cho cả trang
    private async Task FillStatsAsync(List<CustomerItem> items)
    {
        var ids = items.Select(i => i.CustomerId).ToList();
        var stats = await _cache.GetOrCreateAsync($"customer-stats:{string.Join(',', ids)}", async () =>
            (await _context.Contracts.AsNoTracking()
                .Where(x => ids.Contains(x.Application!.CustomerId))
                .GroupBy(x => x.Application!.CustomerId)
                .Select(g => new
                {
                    g.Key,
                    Count = g.Count(),
                    Active = g.Sum(x => x.Status == ContractStatuses.Active ? x.ContractValue ?? 0 : 0)
                })
                .ToListAsync())
            .ToDictionary(x => x.Key, x => (x.Count, x.Active)));

        foreach (var item in items)
        {
            var (count, active) = stats.GetValueOrDefault(item.CustomerId);
            item.ContractCount = count;
            item.ActiveValue = active;
        }
    }

    // GET: api/Customer?q=&kind=DN&branchId=&page=1&pageSize=20 – lọc + phân trang phía server
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] CustomerListQuery query)
    {
        var (page, pageSize) = PagedResult<CustomerItem>.Normalize(query.Page, query.PageSize);

        var filtered = _context.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var k = query.Q.Trim();
            filtered = filtered.Where(c =>
                EF.Functions.Collate(c.FullName, "Latin1_General_CI_AI").Contains(k) ||
                (c.IdentityNumber != null && c.IdentityNumber.Contains(k)) ||
                (c.Phone != null && c.Phone.Contains(k)) ||
                (c.Email != null && c.Email.Contains(k)));
        }

        if (query.BranchId is > 0)
        {
            filtered = filtered.Where(c => c.Applications
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => (int?)a.BranchId)
                .FirstOrDefault() == query.BranchId);
        }

        var counts = await _cache.GetOrCreateAsync($"customer-counts:{query.Q?.Trim()}:{query.BranchId}", async () =>
        {
            var byType = await filtered
                .GroupBy(c => c.CustomerType)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync();
            return new Dictionary<string, int>
            {
                ["ALL"] = byType.Sum(x => x.Count),
                ["DN"] = byType.Where(x => x.Key == "CORPORATE").Sum(x => x.Count),
                ["CN"] = byType.Where(x => x.Key != "CORPORATE").Sum(x => x.Count)
            };
        });

        var kind = (query.Kind ?? "ALL").ToUpperInvariant();
        if (kind == "DN") filtered = filtered.Where(c => c.CustomerType == "CORPORATE");
        else if (kind == "CN") filtered = filtered.Where(c => c.CustomerType != "CORPORATE");
        else kind = "ALL";

        var total = counts[kind];
        var items = await filtered
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.CustomerId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToItem)
            .ToListAsync();
        await FillStatsAsync(items);

        return Ok(new PagedResult<CustomerItem>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            Counts = counts
        });
    }

    // GET: api/Customer/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == id)
            .Select(ToItem)
            .FirstOrDefaultAsync();

        if (customer == null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        await FillStatsAsync([customer]);
        return Ok(customer);
    }

    // POST: api/Customer
    [HttpPost]
    [Authorize(Roles = AppRoles.Editors)]
    public async Task<IActionResult> Create([FromBody] CustomerSaveRequest request)
    {
        var error = await ValidateAsync(request, null);
        if (error != null)
        {
            return BadRequest(new { message = error });
        }

        // Cấp mã trước khi ghi (bộ đếm nguyên tử, kết nối riêng tự commit): không giữ 2 kết nối cùng lúc
        var code = await _context.NextCustomerCodeAsync();

        var customer = new Customer
        {
            CustomerCode = code,
            CreatedAt = DateTime.Now,
            Status = "ACTIVE"
        };
        Apply(customer, request);

        _context.Customers.Add(customer);
        _audit.Add(GetUserId(), AuditActions.Create, "CUSTOMERS", customer.FullName, "Thêm khách hàng");
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Hai người cùng thêm một CCCD/MST: index duy nhất chặn người sau
            return BadRequest(new { message = "Đã tồn tại khách hàng với số CCCD/MST này." });
        }

        return StatusCode(201, new { customer.CustomerId, customer.CustomerCode });
    }

    // PUT: api/Customer/1
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Editors)]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerSaveRequest request)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound(new { message = "Không tìm thấy khách hàng." });
        }

        var error = await ValidateAsync(request, id);
        if (error != null)
        {
            return BadRequest(new { message = error });
        }

        // Form sửa gửi kèm phiên bản lúc mở => người khác vừa sửa thì báo 409 thay vì ghi đè im lặng
        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            byte[] version;
            try
            {
                version = Convert.FromBase64String(request.RowVersion);
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "RowVersion không hợp lệ." });
            }

            if (customer.RowVersion != null && !version.AsSpan().SequenceEqual(customer.RowVersion))
            {
                return Conflict(new { message = "Khách hàng vừa được người khác cập nhật. Vui lòng tải lại rồi sửa lại." });
            }

            _context.Entry(customer).Property(c => c.RowVersion).OriginalValue = version;
        }

        Apply(customer, request);
        _audit.Add(GetUserId(), AuditActions.Update, "CUSTOMERS", customer.FullName, "Cập nhật khách hàng");
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return BadRequest(new { message = "Đã tồn tại khách hàng với số CCCD/MST này." });
        }
        return Ok(new { customer.CustomerId, message = "Đã cập nhật khách hàng." });
    }

    private static void Apply(Customer customer, CustomerSaveRequest r)
    {
        var corporate = r.Kind == "DN";
        customer.CustomerType = corporate ? "CORPORATE" : "INDIVIDUAL";
        customer.FullName = r.Name.Trim();
        customer.IdentityNumber = Digits(r.IdNo);
        customer.Representative = corporate && !string.IsNullOrWhiteSpace(r.Representative) ? r.Representative.Trim() : null;
        customer.Phone = r.Phone.Trim();
        customer.Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim();
        customer.Address = string.IsNullOrWhiteSpace(r.Address) ? null : r.Address.Trim();
    }

    private async Task<string?> ValidateAsync(CustomerSaveRequest r, int? id)
    {
        if (r.Kind != "DN" && r.Kind != "CN") return "Loại khách hàng chỉ nhận DN hoặc CN.";
        if (string.IsNullOrWhiteSpace(r.Name)) return "Tên khách hàng không được để trống.";
        if (r.Name.Trim().Length > 150) return "Tên khách hàng tối đa 150 ký tự.";

        var idNo = Digits(r.IdNo);
        if (!Regex.IsMatch(idNo, @"^\d{10}(\d{2})?$"))
            return r.Kind == "DN" ? "Mã số thuế gồm 10 hoặc 12 chữ số." : "CCCD gồm 12 chữ số.";
        if (await _context.Customers.AnyAsync(c => c.IdentityNumber == idNo && c.CustomerId != id))
            return "Đã tồn tại khách hàng với số CCCD/MST này.";

        if (!Regex.IsMatch(Digits(r.Phone), @"^0\d{9,10}$")) return "Số điện thoại không hợp lệ.";
        if (!string.IsNullOrWhiteSpace(r.Email) && !Regex.IsMatch(r.Email.Trim(), @"^\S+@\S+\.\S+$")) return "Email không hợp lệ.";
        return null;
    }

    private static string Digits(string? s) => new((s ?? string.Empty).Where(char.IsDigit).ToArray());

    private int? GetUserId() =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
