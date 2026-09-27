using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Domain.Admin;
using Teta.Ippcms.Domain.Assurance;
using Teta.Ippcms.Domain.Suppliers;

namespace Teta.Ippcms.Application.Admin;

public sealed record ImportError(int Row, string Field, string Message);
public sealed record ImportResultDto(Guid JobId, string ImportType, string FileName, bool ValidateOnly, int TotalRows, int SucceededRows, int FailedRows,
    string Status, IReadOnlyList<ImportError> Errors);
public sealed record ImportJobDto(Guid Id, string ImportType, string FileName, bool ValidateOnly, int TotalRows, int SucceededRows, int FailedRows, string Status,
    DateTime CreatedAtUtc, string? CreatedBy);
public sealed record ImportTemplateDto(string ImportType, IReadOnlyList<string> Columns, IReadOnlyList<string> Required, string SampleCsv);

public interface IImportService
{
    IReadOnlyList<ImportTemplateDto> Templates();
    Task<ImportResultDto> ImportAsync(string importType, string fileName, Stream csv, bool validateOnly, CancellationToken ct);
    Task<IReadOnlyList<ImportJobDto>> ListJobsAsync(CancellationToken ct);
    Task<ImportResultDto> GetJobAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Validated bulk import of approved master/legacy data (FR-ADM-009). Every run — including
/// validate-only runs — produces an error report. Imports are all-or-nothing: a file with any
/// error is rejected so master data is never partially loaded.
/// </summary>
public sealed class ImportService : IImportService
{
    public const int MaxRows = 5000;

    private static readonly IReadOnlyList<ImportTemplateDto> TemplateList = new[]
    {
        new ImportTemplateDto("Suppliers",
            new[] { "LegalName", "TradingName", "RegistrationNumber", "CsdNumber", "TaxNumber", "VatNumber", "BbbeeLevel", "Email", "Phone", "Address", "Province", "IsImplementingPartner", "ErpVendorCode" },
            new[] { "LegalName" },
            "LegalName,TradingName,RegistrationNumber,CsdNumber,TaxNumber,VatNumber,BbbeeLevel,Email,Phone,Address,Province,IsImplementingPartner,ErpVendorCode\n" +
            "Example Training (Pty) Ltd,Example Training,2015/123456/07,MAAA0012345,9123456789,4123456789,1,info@example.co.za,0110000000,1 Main Rd,Gauteng,true,V1001\n"),
        new ImportTemplateDto("ReferenceData", new[] { "Category", "Code", "Name", "Description", "SortOrder", "IsActive" }, new[] { "Category", "Code", "Name" },
            "Category,Code,Name,Description,SortOrder,IsActive\nCostCategory,TRAINING,Training delivery,,10,true\n"),
        new ImportTemplateDto("PublicHolidays", new[] { "Date", "Name" }, new[] { "Date", "Name" }, "Date,Name\n2027-01-01,New Year's Day\n"),
        new ImportTemplateDto("ComplianceObligations", new[] { "Code", "Title", "Source", "Description", "Frequency", "OwnerName" },
            new[] { "Code", "Title", "Source", "OwnerName" },
            "Code,Title,Source,Description,Frequency,OwnerName\nPFMA-38,Payment within 30 days,PFMA s38(1)(f),,Monthly,CFO\n")
    };

    private readonly ITetaDbContext _db;
    private readonly IClock _clock;
    private readonly INumberGenerator _numbers;
    private readonly IAuditWriter _audit;

    public ImportService(ITetaDbContext db, IClock clock, INumberGenerator numbers, IAuditWriter audit)
    {
        _db = db;
        _clock = clock;
        _numbers = numbers;
        _audit = audit;
    }

    public IReadOnlyList<ImportTemplateDto> Templates() => TemplateList;

    public async Task<ImportResultDto> ImportAsync(string importType, string fileName, Stream csv, bool validateOnly, CancellationToken ct)
    {
        var template = TemplateList.FirstOrDefault(t => string.Equals(t.ImportType, importType, StringComparison.OrdinalIgnoreCase))
                       ?? throw new ValidationException("importType", $"Unknown import type. Use one of: {string.Join(", ", TemplateList.Select(t => t.ImportType))}.");
        using var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(ct);
        var table = Csv.Parse(text);
        var errors = new List<ImportError>();
        if (table.Count == 0) throw new ValidationException("file", "The file is empty.");

        var header = table[0].Select(h => h.Trim()).ToList();
        var index = header.Select((h, i) => (h, i)).ToDictionary(x => x.h, x => x.i, StringComparer.OrdinalIgnoreCase);
        foreach (var required in template.Required.Where(r => !index.ContainsKey(r)))
            errors.Add(new ImportError(1, required, "Required column is missing."));
        foreach (var unknown in header.Where(h => !template.Columns.Contains(h, StringComparer.OrdinalIgnoreCase)))
            errors.Add(new ImportError(1, unknown, "Unknown column."));

        var rows = table.Skip(1).Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
        if (rows.Count > MaxRows) errors.Add(new ImportError(0, "file", $"A file may contain at most {MaxRows} rows."));

        var parsed = new List<(int Row, Func<string, string?> Get)>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            parsed.Add((i + 2, col => index.TryGetValue(col, out var at) && at < row.Length && !string.IsNullOrWhiteSpace(row[at]) ? row[at].Trim() : null));
        }

        var succeeded = 0;
        if (errors.Count == 0)
        {
            succeeded = template.ImportType switch
            {
                "Suppliers" => await SuppliersAsync(parsed, errors, validateOnly, ct),
                "ReferenceData" => await ReferenceDataAsync(parsed, errors, validateOnly, ct),
                "PublicHolidays" => await HolidaysAsync(parsed, errors, validateOnly, ct),
                _ => await ObligationsAsync(parsed, errors, validateOnly, ct)
            };
        }

        var failedRows = errors.Select(e => e.Row).Where(r => r > 1).Distinct().Count();
        var rejected = errors.Count > 0;
        if (rejected && !validateOnly)
        {
            // All-or-nothing: discard pending changes of this import.
            succeeded = 0;
        }
        var status = validateOnly ? (rejected ? "ValidationFailed" : "Validated") : rejected ? "Rejected" : "Completed";
        var job = new ImportJob
        {
            ImportType = template.ImportType,
            FileName = Path.GetFileName(fileName),
            TotalRows = rows.Count,
            SucceededRows = rejected ? 0 : succeeded,
            FailedRows = failedRows,
            Status = status,
            ValidateOnly = validateOnly,
            ErrorReportJson = JsonSerializer.Serialize(errors.Take(2000)),
            CompletedAtUtc = _clock.UtcNow
        };

        if (rejected || validateOnly) DiscardPending();
        _db.ImportJobs.Add(job);
        _audit.Write("Admin", nameof(ImportJob), job.Id.ToString(), validateOnly ? "ImportValidate" : "Import",
            new { job.ImportType, job.FileName, job.TotalRows, job.SucceededRows, job.FailedRows, job.Status });
        await _db.SaveChangesAsync(ct);
        return new ImportResultDto(job.Id, job.ImportType, job.FileName, validateOnly, job.TotalRows, job.SucceededRows, job.FailedRows, job.Status, errors);
    }

    private readonly List<object> _pending = new();

    private void Stage<T>(DbSet<T> set, T entity, bool validateOnly) where T : class
    {
        if (validateOnly) return;
        set.Add(entity);
        _pending.Add(entity);
    }

    private void DiscardPending()
    {
        foreach (var entity in _pending)
        {
            switch (entity)
            {
                case Supplier s: _db.Suppliers.Remove(s); break;
                case ReferenceDataItem r: _db.ReferenceData.Remove(r); break;
                case PublicHoliday h: _db.PublicHolidays.Remove(h); break;
                case ComplianceObligation o: _db.ComplianceObligations.Remove(o); break;
            }
        }
        _pending.Clear();
    }

    private async Task<int> SuppliersAsync(List<(int Row, Func<string, string?> Get)> rows, List<ImportError> errors, bool validateOnly, CancellationToken ct)
    {
        var existing = await _db.Suppliers.AsNoTracking().Select(s => new { s.NormalizedName, s.RegistrationNumber, s.CsdNumber }).ToListAsync(ct);
        var names = existing.Select(e => e.NormalizedName).ToHashSet();
        var registrations = existing.Where(e => e.RegistrationNumber != null).Select(e => e.RegistrationNumber!.ToUpperInvariant()).ToHashSet();
        var csds = existing.Where(e => e.CsdNumber != null).Select(e => e.CsdNumber!.ToUpperInvariant()).ToHashSet();
        var count = 0;
        foreach (var (row, get) in rows)
        {
            var before = errors.Count;
            var name = get("LegalName");
            if (name is null) errors.Add(new ImportError(row, "LegalName", "Required."));
            else if (name.Length > 200) errors.Add(new ImportError(row, "LegalName", "Maximum 200 characters."));
            var normalized = name is null ? null : Supplier.Normalize(name);
            if (normalized is not null && !names.Add(normalized)) errors.Add(new ImportError(row, "LegalName", "Duplicate supplier name (existing or earlier in file)."));
            var reg = get("RegistrationNumber");
            if (reg is not null && !registrations.Add(reg.ToUpperInvariant())) errors.Add(new ImportError(row, "RegistrationNumber", "Duplicate registration number."));
            var csd = get("CsdNumber");
            if (csd is not null && !csds.Add(csd.ToUpperInvariant())) errors.Add(new ImportError(row, "CsdNumber", "Duplicate CSD number."));
            int? level = null;
            if (get("BbbeeLevel") is { } lv)
            {
                if (int.TryParse(lv, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) && l is >= 1 and <= 9) level = l;
                else errors.Add(new ImportError(row, "BbbeeLevel", "Must be a number from 1 to 9 (9 = non-compliant)."));
            }
            var email = get("Email");
            if (email is not null && (!email.Contains('@') || email.Length > 200)) errors.Add(new ImportError(row, "Email", "Invalid e-mail address."));
            var isPartner = ParseBool(get("IsImplementingPartner"), row, "IsImplementingPartner", errors) ?? false;
            if (errors.Count > before) continue;

            Stage(_db.Suppliers, new Supplier
            {
                SupplierNumber = validateOnly ? "-" : await _numbers.NextAsync(NumberPrefixes.Supplier, ct),
                LegalName = name!,
                NormalizedName = normalized!,
                TradingName = get("TradingName"),
                RegistrationNumber = reg,
                CsdNumber = csd,
                TaxNumber = get("TaxNumber"),
                VatNumber = get("VatNumber"),
                BbbeeLevel = level,
                Email = email,
                Phone = get("Phone"),
                Address = get("Address"),
                Province = get("Province"),
                IsImplementingPartner = isPartner,
                ErpVendorCode = get("ErpVendorCode"),
                Status = SupplierStatus.PendingVerification
            }, validateOnly);
            count++;
        }
        return count;
    }

    private async Task<int> ReferenceDataAsync(List<(int Row, Func<string, string?> Get)> rows, List<ImportError> errors, bool validateOnly, CancellationToken ct)
    {
        var keys = (await _db.ReferenceData.AsNoTracking().Select(r => new { r.Category, r.Code }).ToListAsync(ct))
            .Select(r => (r.Category.ToUpperInvariant(), r.Code.ToUpperInvariant())).ToHashSet();
        var count = 0;
        foreach (var (row, get) in rows)
        {
            var before = errors.Count;
            var category = get("Category");
            var code = get("Code");
            var name = get("Name");
            if (category is null) errors.Add(new ImportError(row, "Category", "Required."));
            if (code is null) errors.Add(new ImportError(row, "Code", "Required."));
            else if (code.Length > 50) errors.Add(new ImportError(row, "Code", "Maximum 50 characters."));
            if (name is null) errors.Add(new ImportError(row, "Name", "Required."));
            if (category is not null && code is not null && !keys.Add((category.ToUpperInvariant(), code.ToUpperInvariant())))
                errors.Add(new ImportError(row, "Code", "Duplicate code in this category."));
            var sort = 0;
            if (get("SortOrder") is { } so && !int.TryParse(so, NumberStyles.Integer, CultureInfo.InvariantCulture, out sort))
                errors.Add(new ImportError(row, "SortOrder", "Must be a whole number."));
            var active = ParseBool(get("IsActive"), row, "IsActive", errors) ?? true;
            if (errors.Count > before) continue;
            Stage(_db.ReferenceData, new ReferenceDataItem
            {
                Category = category!, Code = code!, Name = name!, Description = get("Description"), SortOrder = sort, IsActive = active
            }, validateOnly);
            count++;
        }
        return count;
    }

    private async Task<int> HolidaysAsync(List<(int Row, Func<string, string?> Get)> rows, List<ImportError> errors, bool validateOnly, CancellationToken ct)
    {
        var dates = (await _db.PublicHolidays.AsNoTracking().Select(h => h.Date).ToListAsync(ct)).ToHashSet();
        var count = 0;
        foreach (var (row, get) in rows)
        {
            var before = errors.Count;
            var name = get("Name");
            if (name is null) errors.Add(new ImportError(row, "Name", "Required."));
            if (!DateOnly.TryParseExact(get("Date") ?? string.Empty, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                errors.Add(new ImportError(row, "Date", "Use the format yyyy-MM-dd."));
            else if (!dates.Add(date)) errors.Add(new ImportError(row, "Date", "A holiday already exists on this date."));
            if (errors.Count > before) continue;
            Stage(_db.PublicHolidays, new PublicHoliday { Date = date, Name = name! }, validateOnly);
            count++;
        }
        return count;
    }

    private async Task<int> ObligationsAsync(List<(int Row, Func<string, string?> Get)> rows, List<ImportError> errors, bool validateOnly, CancellationToken ct)
    {
        var codes = (await _db.ComplianceObligations.AsNoTracking().Select(o => o.Code).ToListAsync(ct)).Select(c => c.ToUpperInvariant()).ToHashSet();
        var frequencies = new[] { "Monthly", "Quarterly", "Biannually", "Annually", "Once-off" };
        var count = 0;
        foreach (var (row, get) in rows)
        {
            var before = errors.Count;
            var code = get("Code");
            if (code is null) errors.Add(new ImportError(row, "Code", "Required."));
            else if (!codes.Add(code.ToUpperInvariant())) errors.Add(new ImportError(row, "Code", "Duplicate obligation code."));
            foreach (var field in new[] { "Title", "Source", "OwnerName" })
                if (get(field) is null) errors.Add(new ImportError(row, field, "Required."));
            var frequency = get("Frequency") ?? "Quarterly";
            if (!frequencies.Contains(frequency, StringComparer.OrdinalIgnoreCase))
                errors.Add(new ImportError(row, "Frequency", $"Use one of: {string.Join(", ", frequencies)}."));
            if (errors.Count > before) continue;
            Stage(_db.ComplianceObligations, new ComplianceObligation
            {
                Code = code!, Title = get("Title")!, Source = get("Source")!, Description = get("Description"),
                Frequency = frequencies.First(f => string.Equals(f, frequency, StringComparison.OrdinalIgnoreCase)), OwnerName = get("OwnerName")!
            }, validateOnly);
            count++;
        }
        return count;
    }

    private static bool? ParseBool(string? value, int row, string field, List<ImportError> errors)
    {
        if (value is null) return null;
        switch (value.Trim().ToLowerInvariant())
        {
            case "true" or "yes" or "y" or "1": return true;
            case "false" or "no" or "n" or "0": return false;
            default:
                errors.Add(new ImportError(row, field, "Use true/false."));
                return null;
        }
    }

    public async Task<IReadOnlyList<ImportJobDto>> ListJobsAsync(CancellationToken ct) =>
        (await _db.ImportJobs.AsNoTracking().OrderByDescending(j => j.CreatedAtUtc).Take(100).ToListAsync(ct))
        .Select(j => new ImportJobDto(j.Id, j.ImportType, j.FileName, j.ValidateOnly, j.TotalRows, j.SucceededRows, j.FailedRows, j.Status, j.CreatedAtUtc, j.CreatedBy))
        .ToList();

    public async Task<ImportResultDto> GetJobAsync(Guid id, CancellationToken ct)
    {
        var j = await _db.ImportJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Import job", id);
        var errors = string.IsNullOrEmpty(j.ErrorReportJson) ? new List<ImportError>() : JsonSerializer.Deserialize<List<ImportError>>(j.ErrorReportJson) ?? new();
        return new ImportResultDto(j.Id, j.ImportType, j.FileName, j.ValidateOnly, j.TotalRows, j.SucceededRows, j.FailedRows, j.Status, errors);
    }
}

/// <summary>Minimal RFC 4180 CSV parser (quoted fields, escaped quotes, CRLF/LF).</summary>
public static class Csv
{
    public static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }
            switch (c)
            {
                case '"' when field.Length == 0: inQuotes = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row.ToArray()); row.Clear();
                    break;
                default: field.Append(c); break;
            }
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }
        return rows;
    }
}
