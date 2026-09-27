using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Documents;
using Teta.Ippcms.Domain.Common;
using Teta.Ippcms.Domain.Finance;
using Teta.Ippcms.Infrastructure.Persistence;

namespace Teta.Ippcms.Infrastructure.Services;

/// <summary>
/// Allocates business numbers in a dedicated DbContext/transaction so the increment is committed
/// independently of the caller's unit of work (numbers are unique and immutable; a rolled-back
/// business transaction may leave a gap). Concurrency is handled with the sequence version token.
/// </summary>
public sealed class NumberGenerator : INumberGenerator
{
    private readonly DbContextOptions<TetaDbContext> _options;
    private readonly IClock _clock;

    public NumberGenerator(DbContextOptions<TetaDbContext> options, IClock clock)
    {
        _options = options;
        _clock = clock;
    }

    public async Task<string> NextAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var year = _clock.UtcNow.Year;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await using var db = new TetaDbContext(_options);
            var sequence = await db.NumberSequences.SingleOrDefaultAsync(s => s.Prefix == prefix && s.Year == year, cancellationToken);
            if (sequence is null)
            {
                sequence = new NumberSequence { Prefix = prefix, Year = year, NextValue = 1 };
                db.NumberSequences.Add(sequence);
            }

            var value = sequence.NextValue;
            sequence.NextValue = value + 1;
            sequence.Version++;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return $"{prefix}-{year}-{value:D4}";
            }
            catch (DbUpdateException)
            {
                // Lost the race for this value (version conflict or duplicate insert) — retry.
                await Task.Delay(Random.Shared.Next(5, 40), cancellationToken);
            }
        }
        throw new ConflictException($"Could not allocate a {prefix} number; please retry.");
    }
}

/// <summary>
/// File-system document repository (a network share or local disk in production). Content is
/// addressed by an opaque URI; files are written once and never overwritten (new versions get new
/// files). Configure <c>Documents:RootPath</c>; production should point at encrypted storage (SEC-007).
/// </summary>
public sealed class FileSystemDocumentStorage : IDocumentStorage
{
    private readonly string _root;

    public FileSystemDocumentStorage(IConfiguration configuration)
    {
        _root = configuration["Documents:RootPath"] is { Length: > 0 } configured
            ? configured
            : Path.Combine(AppContext.BaseDirectory, "App_Data", "documents");
    }

    public async Task<StoredFile> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var id = Guid.NewGuid().ToString("N");
        var folder = Path.Combine(_root, id[..2]);
        Directory.CreateDirectory(folder);
        var extension = Path.GetExtension(fileName);
        var safeExtension = extension.Length <= 10 && extension.All(c => char.IsLetterOrDigit(c) || c == '.') ? extension : string.Empty;
        var path = Path.Combine(folder, id + safeExtension);

        using var sha = SHA256.Create();
        long size;
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await using (var hashing = new CryptoStream(file, sha, CryptoStreamMode.Write))
        {
            await content.CopyToAsync(hashing, cancellationToken);
            await hashing.FlushFinalBlockAsync(cancellationToken);
            size = file.Length;
        }
        return new StoredFile($"fs://{id[..2]}/{id}{safeExtension}", Convert.ToHexString(sha.Hash!), size);
    }

    public Task<Stream> OpenAsync(string repositoryUri, CancellationToken cancellationToken = default)
    {
        if (!repositoryUri.StartsWith("fs://", StringComparison.Ordinal)) throw new NotFoundException("Document", repositoryUri);
        var relative = repositoryUri["fs://".Length..].Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        if (!full.StartsWith(Path.GetFullPath(_root), StringComparison.Ordinal) || !File.Exists(full))
            throw new NotFoundException("Document", repositoryUri);
        return Task.FromResult<Stream>(new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read));
    }
}

/// <summary>
/// Upload malware scanning (SRS §36). Default scanner: rejects the EICAR test signature and, when <c>Documents:ScannerCommand</c> is
/// configured (e.g. Microsoft Defender's MpCmdRun.exe), runs the external scanner on a temporary copy.
/// </summary>
public sealed class DefaultMalwareScanner : IUploadScanner
{
    private const string Eicar = @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
    private readonly string? _command;
    private readonly ILogger<DefaultMalwareScanner> _logger;

    public DefaultMalwareScanner(IConfiguration configuration, ILogger<DefaultMalwareScanner> logger)
    {
        _command = configuration["Documents:ScannerCommand"];
        _logger = logger;
    }

    public async Task<(bool Clean, string? Reason)> ScanAsync(byte[] content, string fileName, CancellationToken cancellationToken)
    {
        if (Encoding.ASCII.GetString(content).Contains(Eicar, StringComparison.Ordinal))
            return (false, "Malware signature detected (EICAR).");

        if (string.IsNullOrWhiteSpace(_command)) return (true, null);

        var temp = Path.Combine(Path.GetTempPath(), "teta-scan-" + Guid.NewGuid().ToString("N") + Path.GetExtension(fileName));
        await File.WriteAllBytesAsync(temp, content, cancellationToken);
        try
        {
            var parts = _command.Replace("{file}", temp);
            var split = parts.IndexOf(' ');
            var psi = new System.Diagnostics.ProcessStartInfo(split < 0 ? parts : parts[..split], split < 0 ? string.Empty : parts[(split + 1)..])
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi)!;
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 ? (true, null) : (false, "The file was rejected by the malware scanner.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogError(ex, "Malware scanner could not run; upload rejected (fail closed)");
            return (false, "Malware scanning is unavailable; please try again later.");
        }
        finally
        {
            try { File.Delete(temp); } catch (IOException) { }
        }
    }
}

/// <summary>
/// Outbound ERP gateway (FR-FIN-002). Queues certified invoices as outbound interface messages; an
/// ERP adapter (or file export) picks them up. Payment status comes back through the inbound API.
/// </summary>
public sealed class QueuedErpGateway : IErpGateway
{
    private readonly TetaDbContext _db;

    public QueuedErpGateway(TetaDbContext db) => _db = db;

    public Task<string> SubmitCertifiedInvoiceAsync(Guid invoiceId, string payloadJson, CancellationToken cancellationToken = default)
    {
        var reference = "OUT-" + invoiceId.ToString("N")[..12].ToUpperInvariant();
        _db.ErpInterfaceMessages.Add(new ErpInterfaceMessage
        {
            Direction = InterfaceDirection.Outbound,
            MessageType = "InvoiceCertified",
            ExternalReference = reference,
            PayloadJson = payloadJson,
            Status = InterfaceStatus.Pending
        });
        return Task.FromResult(reference);
    }
}
