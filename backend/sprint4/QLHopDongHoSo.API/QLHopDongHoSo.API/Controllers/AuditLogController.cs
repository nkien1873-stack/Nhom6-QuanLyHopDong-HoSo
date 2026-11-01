using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.DTOs;
using QLHopDongHoSo.API.Services;

namespace QLHopDongHoSo.API.Controllers;

public class AuditLogQuery
{
    // Tìm trong đối tượng, chi tiết, IP
    [MinLength(2, ErrorMessage = "Từ khóa tìm kiếm cần ít nhất 2 ký tự.")]
    [MaxLength(100, ErrorMessage = "Từ khóa tìm kiếm tối đa 100 ký tự.")]
    public string? Q { get; set; }

    [MaxLength(50)]
    public string? Action { get; set; }

    public int? UserId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;

    // Tối đa 5000 dòng/trang (dùng khi xuất Excel)
    public int PageSize { get; set; } = 20;
}

public class AuditLogItem
{
    public long LogId { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Nhật ký hệ thống: Admin và Kiểm soát viên (Auditor) được xem
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Auditor)]
public class AuditLogController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AuditLogController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/AuditLog?q=&action=LOGIN&userId=&from=2026-10-01&to=2026-10-31&page=1&pageSize=20
    // Lọc + phân trang phía server; index (CreatedAt DESC, LogID DESC) nên không phải quét cả bảng
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] AuditLogQuery request)
    {
        var (page, pageSize) = PagedResult<AuditLogItem>.Normalize(request.Page, request.PageSize, 20, 5000);

        var query = _context.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(l => l.Action == request.Action);
        }

        if (request.UserId is > 0)
        {
            query = query.Where(l => l.UserId == request.UserId);
        }

        // Tìm theo từ khóa phải quét từng dòng (LIKE '%...%') => giới hạn mặc định 30 ngày gần nhất
        // (dùng được index CreatedAt). Trước đây quét 1 triệu dòng mất 12 s, 10 người cùng tìm => 9 lỗi 500.
        DateTime? searchFrom = null;
        var keyword = request.Q?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            if (keyword.Length < 2)
            {
                return BadRequest(new { message = "Từ khóa tìm kiếm cần ít nhất 2 ký tự." });
            }

            if (!request.From.HasValue)
            {
                searchFrom = DateTime.Today.AddDays(-DefaultSearchDays);
            }
        }

        var fromDate = request.From?.Date ?? searchFrom;
        if (fromDate.HasValue)
        {
            var from = fromDate.Value;
            query = query.Where(l => l.CreatedAt >= from);
        }

        if (request.To.HasValue)
        {
            var to = request.To.Value.Date.AddDays(1);
            query = query.Where(l => l.CreatedAt < to);
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            // So sánh nhị phân trên chữ in hoa: không phân biệt hoa/thường, nhanh hơn ~3 lần collation Unicode
            var k = SearchText.Key(keyword);
            query = query.Where(l =>
                EF.Functions.Collate(l.EntityId.ToUpper(), SearchText.Binary).Contains(k) ||
                (l.Description != null && EF.Functions.Collate(l.Description.ToUpper(), SearchText.Binary).Contains(k)) ||
                (l.IPAddress != null && l.IPAddress.Contains(k)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.LogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AuditLogItem
            {
                LogId = l.LogId,
                UserId = l.UserId,
                Username = l.User != null ? l.User.Username : null,
                FullName = l.User != null ? l.User.FullName : null,
                Action = l.Action,
                EntityType = l.EntityType,
                Target = l.EntityId,
                Detail = l.Description,
                Ip = l.IPAddress,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();

        return Ok(new AuditLogPagedResult
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            SearchFrom = searchFrom
        });
    }

    private const int DefaultSearchDays = 30;
}

public class AuditLogPagedResult : PagedResult<AuditLogItem>
{
    // Có giá trị khi tìm theo từ khóa mà không chọn "Từ ngày": máy chủ tự giới hạn từ ngày này (30 ngày gần nhất)
    public DateTime? SearchFrom { get; set; }
}
