using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Api.Controllers;

public record AuditLogDto(
    long Id,
    Guid? UserId,
    string? Username,
    string Action,
    string EntityName,
    string? EntityId,
    string? BeforeValuesJson,
    string? AfterValuesJson,
    DateTime TimestampUtc,
    string? IpAddress);

public record AuditLogPageDto(IReadOnlyList<AuditLogDto> Items, int TotalCount, int Page, int PageSize);

/// <summary>
/// Read-only access to the immutable audit trail (append-only; no application-level
/// delete/update exists anywhere in the API). Exposes the material-action history captured
/// by <see cref="IFWEMS.Infrastructure.Persistence.Interceptors.AppendOnlyAuditInterceptor"/>
/// for auditors, compliance officers and administrators. FR-051, Section 20 (Immutable Audit Trail).
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize(Roles = "ReadOnlyAuditor,ComplianceOfficer,SystemAdministrator")]
public class AuditController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;

    public AuditController(IfwemsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Query the audit trail with optional filters. Every parameter is optional; results are
    /// ordered newest-first and paginated so the full (potentially very large) log can be
    /// browsed without loading it all into memory or the browser at once.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AuditLogPageDto>> GetAll(
        [FromQuery] string? entityName,
        [FromQuery] string? username,
        [FromQuery] string? action,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 || pageSize > 200 ? 50 : pageSize;

        var query = _dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(a => a.EntityName == entityName);
        }
        if (!string.IsNullOrWhiteSpace(username))
        {
            query = query.Where(a => a.Username != null && a.Username.Contains(username));
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }
        if (fromUtc.HasValue)
        {
            query = query.Where(a => a.TimestampUtc >= fromUtc.Value);
        }
        if (toUtc.HasValue)
        {
            query = query.Where(a => a.TimestampUtc <= toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id, a.UserId, a.Username, a.Action, a.EntityName, a.EntityId,
                a.BeforeValuesJson, a.AfterValuesJson, a.TimestampUtc, a.IpAddress))
            .ToListAsync(cancellationToken);

        return Ok(new AuditLogPageDto(items, totalCount, page, pageSize));
    }

    /// <summary>Distinct entity names present in the audit log, to power a filter dropdown.</summary>
    [HttpGet("entity-names")]
    public async Task<ActionResult<IEnumerable<string>>> GetEntityNames(CancellationToken cancellationToken)
    {
        var names = await _dbContext.AuditLogs.AsNoTracking()
            .Select(a => a.EntityName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        return Ok(names);
    }
}
