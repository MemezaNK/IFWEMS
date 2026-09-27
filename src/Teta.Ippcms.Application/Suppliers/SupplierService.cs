using Microsoft.EntityFrameworkCore;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Engines;
using Teta.Ippcms.Domain.Contracts;
using Teta.Ippcms.Domain.Scm;
using Teta.Ippcms.Domain.Suppliers;

namespace Teta.Ippcms.Application.Suppliers;

public sealed record SupplierDto(Guid Id, string SupplierNumber, string LegalName, string? TradingName, string? RegistrationNumber, string? CsdNumber,
    string? TaxNumber, string? VatNumber, int? BbbeeLevel, string Status, string? Email, string? Phone, string? Address, string? Province,
    bool IsImplementingPartner, bool CsdVerified, DateOnly? TaxClearanceExpiry, string? ErpVendorCode, long Version);

public sealed record SaveSupplierRequest(string LegalName, string? TradingName, string? RegistrationNumber, string? CsdNumber, string? TaxNumber,
    string? VatNumber, int? BbbeeLevel, string? Email, string? Phone, string? Address, string? Province, bool IsImplementingPartner,
    bool CsdVerified, DateOnly? TaxClearanceExpiry, string? ErpVendorCode, SupplierStatus? Status, bool ConfirmNotDuplicate = false);

public sealed record DuplicateCandidate(Guid Id, string SupplierNumber, string LegalName, string? RegistrationNumber, string? CsdNumber, string Reason, double Similarity);

public sealed record SupplierHistoryDto(SupplierDto Supplier, IReadOnlyList<SupplierContractRow> Contracts, IReadOnlyList<SupplierReviewRow> Reviews,
    IReadOnlyList<SupplierBidRow> Bids, decimal? AverageScore, int OpenBreaches);
public sealed record SupplierContractRow(Guid Id, string ContractNumber, string Title, decimal RevisedValue, DateOnly StartDate, DateOnly CurrentEndDate, string Status);
public sealed record SupplierReviewRow(Guid ContractId, string Period, DateOnly ReviewDate, decimal OverallScore, string Rating);
public sealed record SupplierBidRow(Guid ProcurementId, string ProcurementNumber, string Title, decimal BidAmount, string Status, DateTime ReceivedAtUtc);

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> ListAsync(string? search, string? status, bool? implementingPartner, int page, int pageSize, CancellationToken ct);
    Task<SupplierDto> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DuplicateCandidate>> FindDuplicatesAsync(SaveSupplierRequest request, Guid? excludeId, CancellationToken ct);
    Task<SupplierDto> SaveAsync(Guid? id, SaveSupplierRequest request, CancellationToken ct);
    Task<SupplierHistoryDto> HistoryAsync(Guid id, CancellationToken ct);
    Task LinkPortalUserAsync(Guid supplierId, Guid userId, CancellationToken ct);
}

/// <summary>Supplier master with controlled duplicate detection (FR-SCM-018) and history (FR-CON-014).</summary>
public sealed class SupplierService : ISupplierService
{
    private readonly ITetaDbContext _db;
    private readonly INumberGenerator _numbers;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public SupplierService(ITetaDbContext db, INumberGenerator numbers, IClock clock, IAuditWriter audit)
    {
        _db = db;
        _numbers = numbers;
        _clock = clock;
        _audit = audit;
    }

    public async Task<PagedResult<SupplierDto>> ListAsync(string? search, string? status, bool? implementingPartner, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var normalized = Supplier.Normalize(s);
            q = q.Where(x => x.LegalName.Contains(s) || (x.TradingName != null && x.TradingName.Contains(s)) || x.NormalizedName.Contains(normalized)
                             || x.SupplierNumber.Contains(s) || x.RegistrationNumber == s || x.CsdNumber == s);
        }
        if (Enum.TryParse<SupplierStatus>(status, true, out var st)) q = q.Where(x => x.Status == st);
        if (implementingPartner is { } ip) q = q.Where(x => x.IsImplementingPartner == ip);
        var total = await q.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var rows = await q.OrderBy(x => x.LegalName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<SupplierDto>(rows.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<SupplierDto> GetAsync(Guid id, CancellationToken ct) =>
        ToDto(await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Supplier", id));

    public async Task<IReadOnlyList<DuplicateCandidate>> FindDuplicatesAsync(SaveSupplierRequest r, Guid? excludeId, CancellationToken ct)
    {
        var normalized = Supplier.Normalize(r.LegalName ?? string.Empty);
        var candidates = new List<DuplicateCandidate>();
        var all = await _db.Suppliers.AsNoTracking()
            .Where(s => excludeId == null || s.Id != excludeId)
            .Select(s => new { s.Id, s.SupplierNumber, s.LegalName, s.NormalizedName, s.RegistrationNumber, s.CsdNumber, s.TaxNumber })
            .ToListAsync(ct);
        foreach (var s in all)
        {
            if (!string.IsNullOrWhiteSpace(r.RegistrationNumber) && string.Equals(s.RegistrationNumber, r.RegistrationNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                candidates.Add(new DuplicateCandidate(s.Id, s.SupplierNumber, s.LegalName, s.RegistrationNumber, s.CsdNumber, "Same registration number", 1));
            else if (!string.IsNullOrWhiteSpace(r.CsdNumber) && string.Equals(s.CsdNumber, r.CsdNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                candidates.Add(new DuplicateCandidate(s.Id, s.SupplierNumber, s.LegalName, s.RegistrationNumber, s.CsdNumber, "Same CSD number", 1));
            else if (!string.IsNullOrWhiteSpace(r.TaxNumber) && string.Equals(s.TaxNumber, r.TaxNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                candidates.Add(new DuplicateCandidate(s.Id, s.SupplierNumber, s.LegalName, s.RegistrationNumber, s.CsdNumber, "Same tax number", 1));
            else if (normalized.Length > 0)
            {
                var similarity = DuplicateDetection.Similarity(normalized, s.NormalizedName);
                if (similarity >= DuplicateDetection.SupplierNameThreshold)
                    candidates.Add(new DuplicateCandidate(s.Id, s.SupplierNumber, s.LegalName, s.RegistrationNumber, s.CsdNumber, "Similar legal name", Math.Round(similarity, 3)));
            }
        }
        return candidates.OrderByDescending(c => c.Similarity).ToList();
    }

    public async Task<SupplierDto> SaveAsync(Guid? id, SaveSupplierRequest r, CancellationToken ct)
    {
        new Validator().Required("legalName", r.LegalName, 300).Optional("registrationNumber", r.RegistrationNumber, 50)
            .Optional("csdNumber", r.CsdNumber, 50).Optional("email", r.Email, 256).Range("bbbeeLevel", r.BbbeeLevel, 1, 8)
            .Must(r.Email is null || r.Email.Contains('@'), "email", "Enter a valid e-mail address.").ThrowIfInvalid();

        var duplicates = await FindDuplicatesAsync(r, id, ct);
        var hard = duplicates.Where(d => d.Similarity >= 1 && d.Reason != "Similar legal name").ToList();
        if (hard.Count > 0)
            throw new ConflictException($"Duplicate supplier: {hard[0].Reason} as {hard[0].SupplierNumber} {hard[0].LegalName}.", "FR-SCM-018");
        if (duplicates.Count > 0 && !r.ConfirmNotDuplicate)
            throw new ConflictException(
                $"Potential duplicate of {string.Join(", ", duplicates.Take(3).Select(d => $"{d.SupplierNumber} {d.LegalName}"))}. " +
                "Review and confirm it is not a duplicate to continue.", "FR-SCM-018-POTENTIAL");

        Supplier supplier;
        if (id is null)
        {
            supplier = new Supplier { SupplierNumber = await _numbers.NextAsync(NumberPrefixes.Supplier, ct) };
            _db.Suppliers.Add(supplier);
        }
        else
        {
            supplier = await _db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Supplier", id);
        }
        supplier.LegalName = r.LegalName.Trim();
        supplier.NormalizedName = Supplier.Normalize(r.LegalName);
        supplier.TradingName = r.TradingName;
        supplier.RegistrationNumber = Blank(r.RegistrationNumber);
        supplier.CsdNumber = Blank(r.CsdNumber);
        supplier.TaxNumber = Blank(r.TaxNumber);
        supplier.VatNumber = Blank(r.VatNumber);
        supplier.BbbeeLevel = r.BbbeeLevel;
        supplier.Email = r.Email;
        supplier.Phone = r.Phone;
        supplier.Address = r.Address;
        supplier.Province = r.Province;
        supplier.IsImplementingPartner = r.IsImplementingPartner;
        supplier.CsdVerified = r.CsdVerified;
        supplier.TaxClearanceExpiry = r.TaxClearanceExpiry;
        supplier.ErpVendorCode = r.ErpVendorCode;
        if (r.Status is { } status) supplier.Status = status;
        else if (id is null) supplier.Status = r.CsdVerified ? SupplierStatus.Active : SupplierStatus.PendingVerification;

        if (duplicates.Count > 0)
        {
            _audit.Write("Suppliers", nameof(Supplier), supplier.Id.ToString(), "DuplicateOverride",
                new { Candidates = duplicates.Select(d => d.SupplierNumber) }, "User confirmed the record is not a duplicate");
        }
        await _db.SaveChangesAsync(ct);
        return ToDto(supplier);
    }

    public async Task<SupplierHistoryDto> HistoryAsync(Guid id, CancellationToken ct)
    {
        var supplier = await GetAsync(id, ct);
        var contracts = await _db.Contracts.AsNoTracking().Where(c => c.SupplierId == id).OrderByDescending(c => c.StartDate).ToListAsync(ct);
        var reviews = await _db.ContractPerformanceReviews.AsNoTracking().Where(r => r.SupplierId == id).OrderByDescending(r => r.ReviewDate)
            .Select(r => new SupplierReviewRow(r.ContractId, r.Period, r.ReviewDate, r.OverallScore, r.Rating)).ToListAsync(ct);
        var bids = await (from b in _db.Bids.AsNoTracking()
                          join p in _db.Procurements.AsNoTracking() on b.ProcurementId equals p.Id
                          where b.SupplierId == id
                          orderby b.ReceivedAtUtc descending
                          select new { b, p }).ToListAsync(ct);
        var contractIds = contracts.Select(c => c.Id).ToList();
        var openBreaches = await _db.ContractBreaches.CountAsync(b => contractIds.Contains(b.ContractId)
            && b.Status != BreachStatus.Remedied && b.Status != BreachStatus.Closed, ct);
        return new SupplierHistoryDto(supplier,
            contracts.Select(c => new SupplierContractRow(c.Id, c.ContractNumber, c.Title, c.RevisedValue, c.StartDate, c.CurrentEndDate, c.Status.ToString())).ToList(),
            reviews,
            bids.Select(x => new SupplierBidRow(x.p.Id, x.p.Number, x.p.Title, x.b.BidAmount, x.b.Status.ToString(), x.b.ReceivedAtUtc)).ToList(),
            reviews.Count == 0 ? null : Math.Round(reviews.Average(r => r.OverallScore), 2), openBreaches);
    }

    public async Task LinkPortalUserAsync(Guid supplierId, Guid userId, CancellationToken ct)
    {
        _ = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == supplierId, ct) ?? throw new NotFoundException("Supplier", supplierId);
        if (!await _db.Users.AnyAsync(u => u.Id == userId, ct)) throw new NotFoundException("User", userId);
        if (await _db.SupplierUsers.AnyAsync(x => x.UserId == userId && x.SupplierId != supplierId, ct))
            throw new ConflictException("This user is already linked to another supplier.");
        if (!await _db.SupplierUsers.AnyAsync(x => x.UserId == userId && x.SupplierId == supplierId, ct))
        {
            _db.SupplierUsers.Add(new SupplierUser { SupplierId = supplierId, UserId = userId, LinkedAtUtc = _clock.UtcNow });
            await _db.SaveChangesAsync(ct);
        }
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    internal static SupplierDto ToDto(Supplier s) =>
        new(s.Id, s.SupplierNumber, s.LegalName, s.TradingName, s.RegistrationNumber, s.CsdNumber, s.TaxNumber, s.VatNumber, s.BbbeeLevel,
            s.Status.ToString(), s.Email, s.Phone, s.Address, s.Province, s.IsImplementingPartner, s.CsdVerified, s.TaxClearanceExpiry,
            s.ErpVendorCode, s.Version);
}
