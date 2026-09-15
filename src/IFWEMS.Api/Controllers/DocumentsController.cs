using System.Security.Cryptography;
using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IFWEMS.Api.Controllers;

public record DocumentDto(
    Guid Id, string FileName, string DocumentType, string ConfidentialityClassification,
    string IntegrityHash, int Version, string UploadedBy, Guid? CaseId, DateTime CreatedAtUtc);

/// <summary>
/// Evidentiary document upload and metadata storage with an integrity hash captured at upload. FR-032.
/// </summary>
[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IfwemsDbContext _dbContext;
    private readonly string _storageRoot;

    public DocumentsController(IfwemsDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _storageRoot = configuration["Storage:DocumentsPath"] ?? Path.Combine(AppContext.BaseDirectory, "document-storage");
        Directory.CreateDirectory(_storageRoot);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentDto>>> GetAll([FromQuery] Guid? caseId, CancellationToken cancellationToken)
    {
        var query = _dbContext.Documents.AsNoTracking().AsQueryable();
        if (caseId.HasValue)
        {
            query = query.Where(d => d.CaseId == caseId);
        }

        var documents = await query
            .Select(d => new DocumentDto(d.Id, d.FileName, d.DocumentType, d.ConfidentialityClassification, d.IntegrityHash, d.Version, d.UploadedBy, d.CaseId, d.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(documents);
    }

    /// <summary>Upload a document, capturing a SHA-256 integrity hash at time of upload. FR-032.</summary>
    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<DocumentDto>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string documentType,
        [FromForm] string confidentialityClassification,
        [FromForm] Guid? caseId,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { message = "The uploaded file is empty." });
        }

        if (caseId.HasValue && !await _dbContext.Cases.AnyAsync(c => c.Id == caseId, cancellationToken))
        {
            return BadRequest(new { message = "The referenced case does not exist." });
        }

        var storedFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        var storagePath = Path.Combine(_storageRoot, storedFileName);

        string integrityHash;
        await using (var sourceStream = file.OpenReadStream())
        await using (var destinationStream = System.IO.File.Create(storagePath))
        {
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            destinationStream.Position = 0;
            var hashBytes = await SHA256.HashDataAsync(destinationStream, cancellationToken);
            integrityHash = Convert.ToHexString(hashBytes);
        }

        var entity = new Document
        {
            FileName = file.FileName,
            DocumentType = documentType,
            ConfidentialityClassification = confidentialityClassification,
            StoragePath = storagePath,
            IntegrityHash = integrityHash,
            Version = 1,
            UploadedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "unknown",
            CaseId = caseId
        };

        _dbContext.Documents.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new DocumentDto(entity.Id, entity.FileName, entity.DocumentType, entity.ConfidentialityClassification, entity.IntegrityHash, entity.Version, entity.UploadedBy, entity.CaseId, entity.CreatedAtUtc);
        return CreatedAtAction(nameof(GetAll), new { caseId = entity.CaseId }, dto);
    }

    /// <summary>Download a document and re-verify its stored integrity hash. FR-032.</summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null || !System.IO.File.Exists(document.StoragePath)) return NotFound();

        var bytes = await System.IO.File.ReadAllBytesAsync(document.StoragePath, cancellationToken);
        var currentHash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!currentHash.Equals(document.IntegrityHash, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "Document integrity check failed: stored file hash no longer matches the recorded upload hash." });
        }

        return File(bytes, "application/octet-stream", document.FileName);
    }
}
