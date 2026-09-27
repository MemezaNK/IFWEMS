using Microsoft.AspNetCore.Mvc;
using Teta.Ippcms.Api.Security;
using Teta.Ippcms.Application.Documents;
using Teta.Ippcms.Domain.Documents;
using Teta.Ippcms.Domain.Security;

namespace Teta.Ippcms.Api.Controllers;

/// <summary>Versioned document and evidence repository with malware scanning and audited downloads (SRS §14, §36).</summary>
[Route("api/v1/documents")]
[HasPermission(Permissions.DocumentsRead, Permissions.PortalAccess, Permissions.ProcurementEvaluate)]
public sealed class DocumentsController : TetaControllerBase
{
    private readonly IDocumentService _documents;

    public DocumentsController(IDocumentService documents) => _documents = documents;

    public sealed class UploadForm
    {
        public IFormFile File { get; set; } = default!;
        public string ParentType { get; set; } = default!;
        public Guid ParentId { get; set; }
        public string DocumentType { get; set; } = default!;
        public string? Title { get; set; }
        public Classification Classification { get; set; } = Classification.Internal;
        public DateOnly? ExpiryDate { get; set; }
        public string? RetentionClass { get; set; }
        public Guid? ReplacesDocumentId { get; set; }
        public string? SignatureStatus { get; set; }
        public string? EvidenceType { get; set; }
        public string? Source { get; set; }

        public UploadRequest ToRequest() => new(ParentType, ParentId, DocumentType, Title, Classification, ExpiryDate, RetentionClass, ReplacesDocumentId, SignatureStatus);
    }

    [HttpGet]
    public Task<IReadOnlyList<DocumentDto>> List([FromQuery] string parentType, [FromQuery] Guid parentId, [FromQuery] bool includeHistory = false,
        CancellationToken ct = default) => _documents.ListAsync(parentType, parentId, includeHistory, ct);

    [HttpPost, RequestSizeLimit(60 * 1024 * 1024), HasPermission(Permissions.DocumentsUpload, Permissions.PortalAccess)]
    public async Task<DocumentDto> Upload([FromForm] UploadForm form, CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        return await _documents.UploadAsync(form.ToRequest(), stream, form.File.FileName, form.File.ContentType, ct);
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await _documents.DownloadAsync(id, ct);
        return File(file.Stream, file.ContentType, file.FileName);
    }

    [HttpGet("expiring"), HasPermission(Permissions.DocumentsRead)]
    public Task<IReadOnlyList<DocumentDto>> Expiring([FromQuery] int withinDays = 60, CancellationToken ct = default) => _documents.ExpiringAsync(withinDays, ct);

    [HttpGet("evidence")]
    public Task<IReadOnlyList<EvidenceDto>> Evidence([FromQuery] string parentType, [FromQuery] Guid parentId, CancellationToken ct) =>
        _documents.ListEvidenceAsync(parentType, parentId, ct);

    [HttpPost("evidence"), RequestSizeLimit(60 * 1024 * 1024), HasPermission(Permissions.DocumentsUpload, Permissions.PortalAccess, Permissions.PerformanceCapture)]
    public async Task<EvidenceDto> AddEvidence([FromForm] UploadForm form, CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        return await _documents.AddEvidenceAsync(form.ToRequest(), form.EvidenceType ?? form.DocumentType, form.Source, stream, form.File.FileName,
            form.File.ContentType, ct);
    }

    [HttpPost("evidence/{id:guid}/verify"), HasPermission(Permissions.EvidenceVerify, Permissions.PerformanceVerify)]
    public Task<EvidenceDto> Verify(Guid id, VerifyEvidenceRequest request, CancellationToken ct) => _documents.VerifyEvidenceAsync(id, request, ct);
}
