using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Documents;
using Teta.Ippcms.Domain.Scm;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Application.Documents;

public sealed record DocumentDto(Guid Id, string ParentType, Guid ParentId, Guid? ProjectId, string DocumentType, string Title, string FileName,
    string ContentType, long SizeBytes, int DocumentVersion, Guid? PreviousVersionId, bool IsLatest, string Classification, string Sha256,
    DateOnly? ExpiryDate, string SignatureStatus, string? RetentionClass, string? UploadedBy, DateTime UploadedAtUtc);

public sealed record UploadRequest(string ParentType, Guid ParentId, string DocumentType, string? Title, Classification Classification,
    DateOnly? ExpiryDate, string? RetentionClass, Guid? ReplacesDocumentId, string? SignatureStatus);

public sealed record EvidenceDto(Guid Id, string ParentType, Guid ParentId, Guid? ProjectId, Guid DocumentId, string FileName, string EvidenceType,
    string? Source, string VerificationStatus, DateTime? VerifiedAtUtc, string? VerifiedBy, string? VerificationComment, string? UploadedBy,
    DateTime UploadedAtUtc, string Sha256, int DocumentVersion, string Classification);

public sealed record VerifyEvidenceRequest(bool Verified, string? Comment);

public sealed record FileContent(Stream Stream, string FileName, string ContentType);

/// <summary>Scanner abstraction used by the document service (implemented in infrastructure).</summary>
public interface IUploadScanner
{
    Task<(bool Clean, string? Reason)> ScanAsync(byte[] content, string fileName, CancellationToken cancellationToken);
}

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> ListAsync(string parentType, Guid parentId, bool includeHistory, CancellationToken ct);
    Task<DocumentDto> UploadAsync(UploadRequest request, Stream content, string fileName, string contentType, CancellationToken ct);
    Task<FileContent> DownloadAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<EvidenceDto>> ListEvidenceAsync(string parentType, Guid parentId, CancellationToken ct);
    Task<EvidenceDto> AddEvidenceAsync(UploadRequest request, string evidenceType, string? source, Stream content, string fileName, string contentType, CancellationToken ct);
    Task<EvidenceDto> VerifyEvidenceAsync(Guid id, VerifyEvidenceRequest request, CancellationToken ct);
    Task<IReadOnlyList<DocumentDto>> ExpiringAsync(int withinDays, CancellationToken ct);
}

/// <summary>
/// Versioned document and evidence repository (SRS §14, §36): metadata, classification, SHA-256 hash,
/// verification status; malware scan and type/size limits on upload; downloads audited; bid documents
/// restricted by BR-003.
/// </summary>
public sealed class DocumentService : IDocumentService
{
    private readonly ITetaDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAccessScope _scope;
    private readonly IDocumentStorage _storage;
    private readonly IUploadScanner _scanner;
    private readonly ISettings _settings;
    private readonly ISodService _sod;
    private readonly ITransactionLedger _ledger;
    private readonly IAuditWriter _audit;

    public DocumentService(ITetaDbContext db, ICurrentUser user, IClock clock, IAccessScope scope, IDocumentStorage storage, IUploadScanner scanner,
        ISettings settings, ISodService sod, ITransactionLedger ledger, IAuditWriter audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _scope = scope;
        _storage = storage;
        _scanner = scanner;
        _settings = settings;
        _sod = sod;
        _ledger = ledger;
        _audit = audit;
    }

    public async Task<IReadOnlyList<DocumentDto>> ListAsync(string parentType, Guid parentId, bool includeHistory, CancellationToken ct)
    {
        await EnsureParentAccessAsync(parentType, parentId, write: false, ct);
        var q = _db.Documents.AsNoTracking().Where(d => d.ParentType == parentType && d.ParentId == parentId);
        if (!includeHistory) q = q.Where(d => d.IsLatest);
        return (await q.OrderBy(d => d.Title).ThenByDescending(d => d.DocumentVersion).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<DocumentDto> UploadAsync(UploadRequest r, Stream content, string fileName, string contentType, CancellationToken ct)
    {
        var doc = await StoreAsync(r, content, fileName, contentType, ct);
        await _db.SaveChangesAsync(ct);
        return ToDto(doc);
    }

    public async Task<FileContent> DownloadAsync(Guid id, CancellationToken ct)
    {
        var doc = await _db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Document", id);
        await EnsureParentAccessAsync(doc.ParentType, doc.ParentId, write: false, ct);
        if (doc.Classification == Classification.Restricted && !_user.HasPermission(Permissions.AuditRead) && doc.CreatedByUserId != _user.UserId
            && !await IsProjectLeadAsync(doc.ProjectId, ct))
        {
            throw new ForbiddenException("This document is classified Restricted.", "SEC-004");
        }
        _audit.Write("Documents", nameof(DocumentRecord), id.ToString(), "Downloaded", new { doc.FileName, doc.DocumentVersion, doc.Classification });
        await _db.SaveChangesAsync(ct);
        return new FileContent(await _storage.OpenAsync(doc.RepositoryUri, ct), doc.FileName, doc.ContentType);
    }

    public async Task<IReadOnlyList<EvidenceDto>> ListEvidenceAsync(string parentType, Guid parentId, CancellationToken ct)
    {
        await EnsureParentAccessAsync(parentType, parentId, write: false, ct);
        return await (from e in _db.Evidence.AsNoTracking()
                      join d in _db.Documents.AsNoTracking() on e.DocumentId equals d.Id
                      where e.ParentType == parentType && e.ParentId == parentId
                      orderby e.CreatedAtUtc descending
                      select new EvidenceDto(e.Id, e.ParentType, e.ParentId, e.ProjectId, e.DocumentId, d.FileName, e.EvidenceType, e.Source,
                          e.VerificationStatus.ToString(), e.VerifiedAtUtc, e.VerifiedBy, e.VerificationComment, e.CreatedBy, e.CreatedAtUtc, d.Sha256,
                          d.DocumentVersion, d.Classification.ToString())).ToListAsync(ct);
    }

    public async Task<EvidenceDto> AddEvidenceAsync(UploadRequest r, string evidenceType, string? source, Stream content, string fileName, string contentType, CancellationToken ct)
    {
        new Validator().Required("evidenceType", evidenceType, 100).ThrowIfInvalid();
        var doc = await StoreAsync(r with { DocumentType = "Evidence" }, content, fileName, contentType, ct);
        var evidence = new Evidence
        {
            ParentType = r.ParentType, ParentId = r.ParentId, ProjectId = doc.ProjectId, DocumentId = doc.Id, EvidenceType = evidenceType.Trim(),
            Source = source ?? (_user.IsInRole(Roles.Supplier) ? "Supplier portal" : "Internal")
        };
        _db.Evidence.Add(evidence);
        _ledger.Record(nameof(Evidence), evidence.Id, "UploadEvidence", doc.ProjectId);
        await _db.SaveChangesAsync(ct);
        return (await ListEvidenceAsync(r.ParentType, r.ParentId, ct)).Single(x => x.Id == evidence.Id);
    }

    public async Task<EvidenceDto> VerifyEvidenceAsync(Guid id, VerifyEvidenceRequest r, CancellationToken ct)
    {
        var e = await _db.Evidence.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Evidence", id);
        await EnsureParentAccessAsync(e.ParentType, e.ParentId, write: false, ct);
        if (!r.Verified && string.IsNullOrWhiteSpace(r.Comment)) throw new ValidationException("comment", "A reason is required when rejecting evidence.");
        var userId = _user.UserId ?? throw new ForbiddenException();
        await _sod.EnsureAllowedAsync(nameof(Evidence), id, "VerifyEvidence", userId, ct);
        e.VerificationStatus = r.Verified ? EvidenceStatus.Verified : EvidenceStatus.Rejected;
        e.VerifiedAtUtc = _clock.UtcNow;
        e.VerifiedBy = _user.DisplayName ?? _user.Username;
        e.VerifiedByUserId = userId;
        e.VerificationComment = r.Comment;
        _ledger.Record(nameof(Evidence), id, "VerifyEvidence", e.ProjectId);
        await _db.SaveChangesAsync(ct);
        return (await ListEvidenceAsync(e.ParentType, e.ParentId, ct)).Single(x => x.Id == id);
    }

    public async Task<IReadOnlyList<DocumentDto>> ExpiringAsync(int withinDays, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var horizon = _clock.Today.AddDays(withinDays);
        return (await _db.Documents.AsNoTracking().InScopeNullable(scope, d => d.ProjectId)
            .Where(d => d.IsLatest && d.ExpiryDate != null && d.ExpiryDate <= horizon).OrderBy(d => d.ExpiryDate).ToListAsync(ct)).Select(ToDto).ToList();
    }

    // ----- internals -----
    private async Task<DocumentRecord> StoreAsync(UploadRequest r, Stream content, string fileName, string contentType, CancellationToken ct)
    {
        new Validator().OneOf("parentType", r.ParentType, ParentTypes.All).RequiredId("parentId", r.ParentId).Required("documentType", r.DocumentType, 100)
            .Required("fileName", fileName, 255).ThrowIfInvalid();
        var projectId = await EnsureParentAccessAsync(r.ParentType, r.ParentId, write: true, ct);

        var maxMb = await _settings.GetIntAsync(SettingKeys.DocumentMaxUploadMb, ct);
        var allowed = await _settings.GetListAsync(SettingKeys.DocumentAllowedExtensions, ct);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new ValidationException("file", $"File type {extension} is not permitted. Allowed: {string.Join(", ", allowed)}.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0) throw new ValidationException("file", "The file is empty.");
        if (maxMb > 0 && buffer.Length > maxMb * 1024L * 1024L) throw new ValidationException("file", $"The file exceeds the {maxMb} MB limit.");
        var bytes = buffer.ToArray();
        var (clean, reason) = await _scanner.ScanAsync(bytes, fileName, ct);
        if (!clean)
        {
            _audit.Write("Documents", r.ParentType, r.ParentId.ToString(), "UploadBlocked", new { fileName }, reason);
            await _db.SaveChangesAsync(ct);
            throw new DomainException(reason ?? "The file failed the malware scan.", "SEC-013");
        }

        buffer.Position = 0;
        var stored = await _storage.SaveAsync(buffer, fileName, ct);

        DocumentRecord? previous = null;
        if (r.ReplacesDocumentId is { } replaces)
        {
            previous = await _db.Documents.SingleOrDefaultAsync(d => d.Id == replaces && d.ParentType == r.ParentType && d.ParentId == r.ParentId, ct)
                       ?? throw new ValidationException("replacesDocumentId", "The document being replaced does not belong to this record.");
            if (!previous.IsLatest) throw new ConflictException("Only the latest version can be superseded.");
            previous.IsLatest = false;
        }

        var doc = new DocumentRecord
        {
            ParentType = r.ParentType, ParentId = r.ParentId, ProjectId = projectId, DocumentType = r.DocumentType,
            Title = string.IsNullOrWhiteSpace(r.Title) ? Path.GetFileNameWithoutExtension(fileName) : r.Title!, FileName = Path.GetFileName(fileName),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType, SizeBytes = stored.SizeBytes,
            DocumentVersion = (previous?.DocumentVersion ?? 0) + 1, PreviousVersionId = previous?.Id, Classification = r.Classification,
            RepositoryUri = stored.RepositoryUri, Sha256 = stored.Sha256, ExpiryDate = r.ExpiryDate, RetentionClass = r.RetentionClass,
            SignatureStatus = string.IsNullOrWhiteSpace(r.SignatureStatus) ? "NotRequired" : r.SignatureStatus!
        };
        _db.Documents.Add(doc);
        return doc;
    }

    /// <summary>Resolves the owning project for a parent record and enforces data scope; bids also require BR-003 declarations.</summary>
    private async Task<Guid?> EnsureParentAccessAsync(string parentType, Guid parentId, bool write, CancellationToken ct)
    {
        Guid? projectId = parentType switch
        {
            ParentTypes.Project => parentId,
            ParentTypes.Contract => await _db.Contracts.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.Deliverable => await _db.Deliverables.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.Milestone => await _db.WbsElements.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.Procurement => await _db.Procurements.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.Publication => await (from pub in _db.Publications join p in _db.Procurements on pub.ProcurementId equals p.Id
                                              where pub.Id == parentId select (Guid?)p.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.Bid => await (from b in _db.Bids join p in _db.Procurements on b.ProcurementId equals p.Id
                                      where b.Id == parentId select (Guid?)p.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.Invoice => await _db.Invoices.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.PerformanceResult => await _db.PerformanceResults.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.MonitoringVisit => await _db.MonitoringVisits.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.Finding => await _db.Findings.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.CorrectiveAction => await _db.CorrectiveActions.Where(x => x.Id == parentId).Select(x => x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.AuditFinding => await _db.AuditFindings.Where(x => x.Id == parentId).Select(x => x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.Beneficiary => await _db.Beneficiaries.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.ContractBreach => await (from b in _db.ContractBreaches join c in _db.Contracts on b.ContractId equals c.Id
                                                 where b.Id == parentId select (Guid?)c.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.ChangeRequest => await _db.ChangeRequests.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.Risk => await _db.Risks.Where(x => x.Id == parentId).Select(x => x.ProjectId).SingleOrDefaultAsync(ct),
            ParentTypes.Issue => await _db.Issues.Where(x => x.Id == parentId).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
            _ => null
        };

        // Supplier-portal users: only their own contracts, deliverables and invoices (SRS §13).
        if (_user.IsInRole(Roles.Supplier) && !_user.HasPermission(Permissions.ContractRead))
        {
            var supplierId = await _db.SupplierUsers.Where(s => s.UserId == _user.UserId).Select(s => (Guid?)s.SupplierId).FirstOrDefaultAsync(ct);
            var owns = parentType switch
            {
                ParentTypes.Contract => await _db.Contracts.AnyAsync(c => c.Id == parentId && c.SupplierId == supplierId, ct),
                ParentTypes.Deliverable => await (from d in _db.Deliverables join c in _db.Contracts on d.ContractId equals c.Id
                                                  where d.Id == parentId && c.SupplierId == supplierId select d).AnyAsync(ct),
                ParentTypes.Invoice => await _db.Invoices.AnyAsync(i => i.Id == parentId && i.SupplierId == supplierId, ct),
                _ => false
            };
            if (!owns) throw new NotFoundException(parentType, parentId);
            return projectId;
        }

        if (parentType == ParentTypes.Bid)
        {
            var procurementId = await _db.Bids.Where(b => b.Id == parentId).Select(b => b.ProcurementId).SingleOrDefaultAsync(ct);
            var declared = await _db.Declarations.AnyAsync(d => d.ProcurementId == procurementId && d.UserId == _user.UserId && d.ConfidentialityAccepted && !d.HasConflict, ct);
            if (!declared) throw new ForbiddenException("Complete the declaration of interest before accessing bid documents.", "BR-003");
        }

        if (projectId is { } pid)
        {
            var scope = await _scope.GetAsync(ct);
            var committee = parentType is ParentTypes.Procurement or ParentTypes.Bid or ParentTypes.Publication
                            && await IsCommitteeMemberForParentAsync(parentType, parentId, ct);
            if (!scope.Includes(pid) && !committee) throw new NotFoundException(parentType, parentId);
        }
        else if (parentType is not (ParentTypes.Supplier or ParentTypes.Programme))
        {
            throw new NotFoundException(parentType, parentId);
        }
        if (write && !_user.HasPermission(Permissions.DocumentsUpload) && !_user.HasPermission(Permissions.PortalAccess))
            throw new ForbiddenException("You are not permitted to upload documents.");
        return projectId;
    }

    private async Task<bool> IsCommitteeMemberForParentAsync(string parentType, Guid parentId, CancellationToken ct)
    {
        var procurementId = parentType switch
        {
            ParentTypes.Procurement => parentId,
            ParentTypes.Bid => await _db.Bids.Where(b => b.Id == parentId).Select(b => b.ProcurementId).SingleOrDefaultAsync(ct),
            _ => await _db.Publications.Where(p => p.Id == parentId).Select(p => p.ProcurementId).SingleOrDefaultAsync(ct)
        };
        var today = _clock.Today;
        return await (from m in _db.CommitteeMembers join c in _db.Committees on m.CommitteeId equals c.Id
                      where c.ProcurementId == procurementId && m.UserId == _user.UserId && m.AccessFrom <= today && m.AccessTo >= today
                      select m).AnyAsync(ct);
    }

    private async Task<bool> IsProjectLeadAsync(Guid? projectId, CancellationToken ct) =>
        projectId is { } pid && await _db.Projects.AnyAsync(p => p.Id == pid && (p.ManagerUserId == _user.UserId || p.SponsorUserId == _user.UserId), ct);

    private static DocumentDto ToDto(DocumentRecord d) =>
        new(d.Id, d.ParentType, d.ParentId, d.ProjectId, d.DocumentType, d.Title, d.FileName, d.ContentType, d.SizeBytes, d.DocumentVersion,
            d.PreviousVersionId, d.IsLatest, d.Classification.ToString(), d.Sha256, d.ExpiryDate, d.SignatureStatus, d.RetentionClass, d.CreatedBy,
            d.CreatedAtUtc);
}
