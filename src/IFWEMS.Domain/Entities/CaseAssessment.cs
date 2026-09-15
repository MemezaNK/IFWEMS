namespace IFWEMS.Domain.Entities;

/// <summary>
/// Structured, configurable assessment questionnaire response captured against a case. FR-022.
/// </summary>
public class CaseAssessment : Common.BaseEntity
{
    public Guid CaseId { get; set; }
    public Case Case { get; set; } = default!;
    public Guid AssessorUserId { get; set; }
    public User AssessorUser { get; set; } = default!;
    /// <summary>JSON-encoded questionnaire answers (question code -> answer).</summary>
    public string AnswersJson { get; set; } = "{}";
    public string Conclusion { get; set; } = default!;
    public string? Reasons { get; set; }
}
