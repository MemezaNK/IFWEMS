namespace Teta.Ippcms.Application.Abstractions;

/// <summary>Binary document repository (file share / DMS / object storage). Metadata stays in the database (SRS §36).</summary>
public interface IDocumentStorage
{
    Task<StoredFile> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
    Task<Stream> OpenAsync(string repositoryUri, CancellationToken cancellationToken = default);
}

public sealed record StoredFile(string RepositoryUri, string Sha256, long SizeBytes);

/// <summary>Generates immutable human-readable business numbers, e.g. PRJ-2026-0007 (SRS §25).</summary>
public interface INumberGenerator
{
    Task<string> NextAsync(string prefix, CancellationToken cancellationToken = default);
}

/// <summary>Outbound interface to the finance/ERP platform (FR-FIN-002). Default implementation queues messages.</summary>
public interface IErpGateway
{
    Task<string> SubmitCertifiedInvoiceAsync(Guid invoiceId, string payloadJson, CancellationToken cancellationToken = default);
}

/// <summary>Business number prefixes.</summary>
public static class NumberPrefixes
{
    public const string ProjectDraft = "CON";
    public const string Project = "PRJ";
    public const string Requisition = "REQ";
    public const string Procurement = "PRC";
    public const string Award = "AWD";
    public const string Contract = "CNT";
    public const string Variation = "VAR";
    public const string Deliverable = "DLV";
    public const string Invoice = "INV";
    public const string ChangeRequest = "CR";
    public const string Issue = "ISS";
    public const string Risk = "RSK";
    public const string Visit = "MV";
    public const string Finding = "FND";
    public const string CorrectiveAction = "CA";
    public const string AuditFinding = "AF";
    public const string Beneficiary = "BEN";
    public const string Supplier = "SUP";
    public const string ProcurementException = "EXC";
    public const string BoardPack = "BRP";
}
