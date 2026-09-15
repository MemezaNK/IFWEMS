namespace IFWEMS.Domain.Entities;

/// <summary>
/// Evidentiary document metadata. FR-032.
/// </summary>
public class Document : Common.BaseEntity
{
    public string FileName { get; set; } = default!;
    public string DocumentType { get; set; } = default!;
    public string ConfidentialityClassification { get; set; } = "PUBLIC";
    public string StoragePath { get; set; } = default!;
    public string IntegrityHash { get; set; } = default!;
    public int Version { get; set; } = 1;
    public string UploadedBy { get; set; } = default!;
    public Guid? CaseId { get; set; }
    public Case? Case { get; set; }
}
