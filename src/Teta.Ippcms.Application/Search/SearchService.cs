using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Documents;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Application.Search;

public sealed record SearchResultDto(string Type, Guid Id, string Reference, string Title, string? Subtitle, string Link);
public sealed record SearchResponse(string Query, IReadOnlyList<SearchResultDto> Results, IReadOnlyDictionary<string, int> CountsByType);

public interface ISearchService
{
    Task<SearchResponse> SearchAsync(string query, string? type, int limit, CancellationToken ct);
}

/// <summary>
/// Global authorised search across projects, procurements, contracts, suppliers, risks and documents
/// (FR-ADM-010, SRS §13). Every result set is filtered by permission and row-level project scope;
/// bid documents (BR-003) and restricted-classification documents are never returned.
/// </summary>
public sealed class SearchService : ISearchService
{
    public static readonly IReadOnlyList<string> Types = new[] { "Project", "Procurement", "Contract", "Supplier", "Risk", "Invoice", "Document" };

    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAccessScope _scope;

    public SearchService(ITetaDbContext db, ICurrentUser user, IAccessScope scope)
    {
        _db = db;
        _user = user;
        _scope = scope;
    }

    public async Task<SearchResponse> SearchAsync(string query, string? type, int limit, CancellationToken ct)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length < 2) throw new ValidationException("q", "Enter at least 2 characters.");
        if (q.Length > 100) q = q[..100];
        limit = Math.Clamp(limit, 1, 100);
        var scope = await _scope.GetAsync(ct);
        bool Want(string t) => type is null || string.Equals(type, t, StringComparison.OrdinalIgnoreCase);
        var results = new List<SearchResultDto>();

        if (Want("Project") && _user.HasPermission(Permissions.PortfolioRead))
        {
            var rows = await _db.Projects.AsNoTracking().InScope(scope, p => p.Id)
                .Where(p => p.Name.Contains(q) || p.DraftReference.Contains(q) || (p.ProjectNumber != null && p.ProjectNumber.Contains(q))
                            || (p.Description != null && p.Description.Contains(q)))
                .OrderBy(p => p.Name).Take(limit).ToListAsync(ct);
            results.AddRange(rows.Select(p => new SearchResultDto("Project", p.Id, p.Reference, p.Name, $"{p.Status} · {p.Stage} · {p.Health}", $"/projects/{p.Id}")));
        }
        if (Want("Procurement") && _user.HasPermission(Permissions.ProcurementRead))
        {
            var rows = await _db.Procurements.AsNoTracking().InScope(scope, p => p.ProjectId)
                .Where(p => p.Number.Contains(q) || p.Title.Contains(q)).OrderByDescending(p => p.CreatedAtUtc).Take(limit).ToListAsync(ct);
            results.AddRange(rows.Select(p => new SearchResultDto("Procurement", p.Id, p.Number, p.Title, $"{p.Method} · {p.Status}", $"/procurement/{p.Id}")));
        }
        if (Want("Contract") && _user.HasPermission(Permissions.ContractRead))
        {
            var rows = await _db.Contracts.AsNoTracking().InScope(scope, c => c.ProjectId)
                .Where(c => c.ContractNumber.Contains(q) || c.Title.Contains(q) || (c.PoReference != null && c.PoReference.Contains(q)))
                .OrderByDescending(c => c.StartDate).Take(limit).ToListAsync(ct);
            results.AddRange(rows.Select(c => new SearchResultDto("Contract", c.Id, c.ContractNumber, c.Title, $"{c.Status} · ends {c.CurrentEndDate:yyyy-MM-dd}",
                $"/contracts/{c.Id}")));
        }
        if (Want("Supplier") && _user.HasPermission(Permissions.SupplierRead))
        {
            var upper = q.ToUpperInvariant();
            var rows = await _db.Suppliers.AsNoTracking()
                .Where(s => s.LegalName.Contains(q) || s.NormalizedName.Contains(upper) || s.SupplierNumber.Contains(q)
                            || (s.TradingName != null && s.TradingName.Contains(q)) || (s.RegistrationNumber != null && s.RegistrationNumber.Contains(q))
                            || (s.CsdNumber != null && s.CsdNumber.Contains(q)))
                .OrderBy(s => s.LegalName).Take(limit).ToListAsync(ct);
            results.AddRange(rows.Select(s => new SearchResultDto("Supplier", s.Id, s.SupplierNumber, s.LegalName, s.Status.ToString(), $"/suppliers/{s.Id}")));
        }
        if (Want("Risk") && _user.HasPermission(Permissions.RiskRead))
        {
            var rows = await _db.Risks.AsNoTracking().InScopeNullable(scope, r => r.ProjectId)
                .Where(r => r.Number.Contains(q) || r.Title.Contains(q)).OrderByDescending(r => r.ResidualScore).Take(limit).ToListAsync(ct);
            results.AddRange(rows.Select(r => new SearchResultDto("Risk", r.Id, r.Number, r.Title, $"{r.ResidualRating} · {r.Status}", $"/assurance/risks/{r.Id}")));
        }
        if (Want("Invoice") && _user.HasPermission(Permissions.FinanceRead))
        {
            var rows = await _db.Invoices.AsNoTracking().InScope(scope, i => i.ProjectId)
                .Where(i => i.Number.Contains(q) || i.SupplierInvoiceNumber.Contains(q)).OrderByDescending(i => i.InvoiceDate).Take(limit).ToListAsync(ct);
            results.AddRange(rows.Select(i => new SearchResultDto("Invoice", i.Id, i.Number, i.SupplierInvoiceNumber, $"{i.Amount:N2} · {i.Status}",
                $"/finance/invoices/{i.Id}")));
        }
        if (Want("Document") && _user.HasPermission(Permissions.DocumentsRead))
        {
            var rows = await _db.Documents.AsNoTracking().InScopeNullable(scope, d => d.ProjectId)
                .Where(d => d.IsLatest && d.ParentType != ParentTypes.Bid && d.Classification != Classification.Restricted)
                .Where(d => d.Title.Contains(q) || d.FileName.Contains(q) || d.DocumentType.Contains(q))
                .OrderByDescending(d => d.CreatedAtUtc).Take(limit).ToListAsync(ct);
            // Documents not attached to a project are only visible organisation-wide.
            if (!scope.All) rows = rows.Where(d => d.ProjectId is not null).ToList();
            results.AddRange(rows.Select(d => new SearchResultDto("Document", d.Id, $"{d.DocumentType} v{d.DocumentVersion}", d.Title,
                $"{d.ParentType} · {d.FileName}", $"/documents/{d.Id}")));
        }

        var counts = results.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count());
        var ordered = results
            .OrderByDescending(r => string.Equals(r.Reference, q, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(r => r.Title.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            .ThenBy(r => r.Type).Take(type is null ? limit * 2 : limit).ToList();
        return new SearchResponse(q, ordered, counts);
    }
}
