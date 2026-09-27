using System.Text;
using Teta.Ippcms.Domain.Common;

namespace Teta.Ippcms.Domain.Suppliers;

public enum SupplierStatus
{
    PendingVerification,
    Active,
    Suspended,
    Restricted,
    Inactive
}

/// <summary>
/// Supplier / implementing-partner master record (SRS §7, FR-SCM-018). Registration and CSD numbers
/// are unique; a normalised legal name supports fuzzy duplicate detection.
/// </summary>
public class Supplier : AuditableEntity
{
    public string SupplierNumber { get; set; } = default!;
    public string LegalName { get; set; } = default!;
    public string NormalizedName { get; set; } = default!;
    public string? TradingName { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? CsdNumber { get; set; }
    public string? TaxNumber { get; set; }
    public string? VatNumber { get; set; }
    public int? BbbeeLevel { get; set; }
    public SupplierStatus Status { get; set; } = SupplierStatus.PendingVerification;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Province { get; set; }
    public bool IsImplementingPartner { get; set; }
    public bool CsdVerified { get; set; }
    public DateOnly? TaxClearanceExpiry { get; set; }
    public string? ErpVendorCode { get; set; }

    /// <summary>Upper-case, alphanumeric-only name without common company suffixes, for duplicate matching.</summary>
    public static string Normalize(string name)
    {
        var upper = name.ToUpperInvariant();
        foreach (var suffix in new[] { "(PTY) LTD", "PTY LTD", "(PTY)LTD", " LTD", " CC", " INC", " NPC", " SOC", " (RF)" })
        {
            upper = upper.Replace(suffix, " ");
        }
        var sb = new StringBuilder(upper.Length);
        foreach (var c in upper)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        }
        return sb.ToString();
    }
}

/// <summary>Links a platform user to a supplier for restricted supplier-portal access (SRS §13).</summary>
public class SupplierUser : Entity
{
    public Guid SupplierId { get; set; }
    public Guid UserId { get; set; }
    public DateTime LinkedAtUtc { get; set; }
}
