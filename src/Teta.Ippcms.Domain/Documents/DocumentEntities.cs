using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Documents;

public enum Classification
{
    Public,
    Internal,
    Confidential,
    Restricted
}

/// <summary>
/// Document metadata (SRS §7 "Document", §36). Binary content lives in the document repository; the
/// database holds metadata, classification, repository URI, version and SHA-256 hash.
/// </summary>
public class DocumentRecord : AuditableEntity
{
    public string ParentType { get; set; } = default!;
    public Guid ParentId { get; set; }
    public Guid? ProjectId { get; set; }
    public string DocumentType { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public int DocumentVersion { get; set; } = 1;
    public Guid? PreviousVersionId { get; set; }
    public bool IsLatest { get; set; } = true;
    public Classification Classification { get; set; } = Classification.Internal;
    public string RepositoryUri { get; set; } = default!;
    public string Sha256 { get; set; } = default!;
    public DateOnly? ExpiryDate { get; set; }
    public string SignatureStatus { get; set; } = "NotRequired";
    public string? RetentionClass { get; set; }
}

public enum EvidenceStatus
{
    Pending,
    Verified,
    Rejected
}

/// <summary>Evidence metadata and verification (SRS §14, FR-ME-004, FR-STR-006).</summary>
public class Evidence : AuditableEntity
{
    public string ParentType { get; set; } = default!;
    public Guid ParentId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid DocumentId { get; set; }
    public string EvidenceType { get; set; } = default!;
    public string? Source { get; set; }
    public EvidenceStatus VerificationStatus { get; set; } = EvidenceStatus.Pending;
    public DateTime? VerifiedAtUtc { get; set; }
    public string? VerifiedBy { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public string? VerificationComment { get; set; }
}
