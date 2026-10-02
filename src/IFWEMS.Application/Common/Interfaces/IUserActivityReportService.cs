namespace IFWEMS.Application.Common.Interfaces;

/// <summary>Per-user activity and workload summary row used by the User Activity Report.</summary>
public sealed record UserActivitySummaryDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string? OrgUnitName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    int StatusChangesMade,
    int InvestigationsCompleted,
    int AssessmentsCompleted,
    int AuditActions,
    int TasksCompletedTotal,
    int OpenInvestigationsAssigned,
    int OpenCorrectiveActionsAssigned,
    int TasksPendingTotal,
    DateTime? LastActivityAtUtc);

/// <summary>A single completed or pending work item shown in a user's detailed activity breakdown.</summary>
public sealed record UserActivityItemDto(
    string Type,
    string Reference,
    string? Detail,
    string Status,
    DateTime? OccurredAtUtc);

/// <summary>Drill-down for a single user: their summary plus the underlying completed/pending items.</summary>
public sealed record UserActivityDetailDto(
    UserActivitySummaryDto Summary,
    IReadOnlyList<UserActivityItemDto> Completed,
    IReadOnlyList<UserActivityItemDto> Pending);

/// <summary>
/// Full report payload: either an organisation-wide summary (one row per user, <see cref="Detail"/> null)
/// or a single user's detail (<see cref="Users"/> contains just that user, <see cref="Detail"/> populated).
/// </summary>
public sealed record UserActivityReportDto(
    DateTime GeneratedAtUtc,
    DateTime? FromUtc,
    DateTime? ToUtc,
    IReadOnlyList<UserActivitySummaryDto> Users,
    UserActivityDetailDto? Detail);

/// <summary>
/// Builds the User Activity Report (FR-042-style exportable register): what each user has done
/// (status changes, completed investigations/assessments, audit actions) and what they still need
/// to do (open investigations and corrective actions assigned to them), with a client-ready PDF export
/// containing charts and tables.
/// </summary>
public interface IUserActivityReportService
{
    /// <summary>
    /// Returns activity stats for all active users (<paramref name="userId"/> null) or a single user's
    /// detailed breakdown (<paramref name="userId"/> set). <paramref name="fromUtc"/>/<paramref name="toUtc"/>
    /// bound "done" activity (status changes, completions, audit actions); "to do" items are always the
    /// current open workload regardless of the date range.
    /// </summary>
    Task<UserActivityReportDto> GetAsync(Guid? userId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);

    /// <summary>Renders the same data as <see cref="GetAsync"/> into a client-ready PDF with charts and tables.</summary>
    Task<byte[]> ExportPdfAsync(Guid? userId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);
}
