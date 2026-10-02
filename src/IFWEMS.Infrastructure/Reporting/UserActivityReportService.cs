using System.Globalization;
using IFWEMS.Application.Common.Interfaces;
using IFWEMS.Application.Common.Reporting;
using IFWEMS.Domain.Entities;
using IFWEMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IFWEMS.Infrastructure.Reporting;

/// <summary>
/// Builds the User Activity Report: per-user stats on completed work (case status changes,
/// completed investigations/assessments, audit actions) and outstanding workload (open
/// investigations and unverified corrective actions assigned to them), plus a client-ready PDF
/// export with vector bar/pie charts and tables (FR-042).
/// </summary>
public sealed class UserActivityReportService : IUserActivityReportService
{
    private readonly IfwemsDbContext _db;

    public UserActivityReportService(IfwemsDbContext db)
    {
        _db = db;
    }

    public async Task<UserActivityReportDto> GetAsync(Guid? userId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var generatedAtUtc = DateTime.UtcNow;
        var from = fromUtc ?? generatedAtUtc.AddMonths(-3);
        var to = toUtc ?? generatedAtUtc;

        var usersQuery = _db.Users.AsNoTracking()
            .Include(u => u.OrgUnit)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .AsQueryable();
        usersQuery = userId is { } id ? usersQuery.Where(u => u.Id == id) : usersQuery.Where(u => u.IsActive);

        var users = await usersQuery.OrderBy(u => u.DisplayName).ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            return new UserActivityReportDto(generatedAtUtc, fromUtc, toUtc, Array.Empty<UserActivitySummaryDto>(), null);
        }

        var usernames = users.Select(u => u.Username).ToList();
        var userIds = users.Select(u => u.Id).ToList();

        var statusChanges = await _db.CaseStatusHistories.AsNoTracking()
            .Include(h => h.Case)
            .Where(h => h.ChangedBy != null && usernames.Contains(h.ChangedBy) && h.ChangedAtUtc >= from && h.ChangedAtUtc <= to)
            .ToListAsync(cancellationToken);

        var completedInvestigations = await _db.Investigations.AsNoTracking()
            .Include(i => i.Case)
            .Where(i => userIds.Contains(i.InvestigatorUserId)
                        && (i.Status == InvestigationStatus.Approved || i.Status == InvestigationStatus.Rejected)
                        && i.ApprovedAtUtc != null && i.ApprovedAtUtc >= from && i.ApprovedAtUtc <= to)
            .ToListAsync(cancellationToken);

        var openInvestigations = await _db.Investigations.AsNoTracking()
            .Include(i => i.Case)
            .Where(i => userIds.Contains(i.InvestigatorUserId) && i.Status == InvestigationStatus.InProgress)
            .ToListAsync(cancellationToken);

        var assessments = await _db.CaseAssessments.AsNoTracking()
            .Include(a => a.Case)
            .Where(a => userIds.Contains(a.AssessorUserId) && a.CreatedAtUtc >= from && a.CreatedAtUtc <= to)
            .ToListAsync(cancellationToken);

        var openCorrectiveActions = await _db.CorrectiveActions.AsNoTracking()
            .Include(ca => ca.Case)
            .Where(ca => ca.CreatedBy != null && usernames.Contains(ca.CreatedBy) && ca.Status != CorrectiveActionStatus.Verified)
            .ToListAsync(cancellationToken);

        var auditCounts = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.UserId != null && userIds.Contains(a.UserId.Value) && a.TimestampUtc >= from && a.TimestampUtc <= to)
            .GroupBy(a => a.UserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var lastActivity = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.UserId != null && userIds.Contains(a.UserId.Value))
            .GroupBy(a => a.UserId!.Value)
            .Select(g => new { UserId = g.Key, Last = g.Max(a => a.TimestampUtc) })
            .ToListAsync(cancellationToken);

        var summaries = users.Select(u =>
        {
            var statusCount = statusChanges.Count(h => h.ChangedBy == u.Username);
            var invCompleted = completedInvestigations.Count(i => i.InvestigatorUserId == u.Id);
            var assessCompleted = assessments.Count(a => a.AssessorUserId == u.Id);
            var auditCount = auditCounts.FirstOrDefault(a => a.UserId == u.Id)?.Count ?? 0;
            var openInv = openInvestigations.Count(i => i.InvestigatorUserId == u.Id);
            var openCorrective = openCorrectiveActions.Count(ca => ca.CreatedBy == u.Username);
            var last = lastActivity.FirstOrDefault(a => a.UserId == u.Id)?.Last;

            return new UserActivitySummaryDto(
                u.Id, u.Username, u.DisplayName, u.OrgUnit?.Name,
                u.UserRoles.Select(ur => ur.Role.Name).Distinct().OrderBy(n => n).ToList(),
                u.IsActive, statusCount, invCompleted, assessCompleted, auditCount,
                statusCount + invCompleted + assessCompleted, openInv, openCorrective, openInv + openCorrective, last);
        }).OrderByDescending(s => s.TasksCompletedTotal).ToList();

        UserActivityDetailDto? detail = null;
        if (userId is { } singleId)
        {
            var user = users.Single(u => u.Id == singleId);
            var completedItems = new List<UserActivityItemDto>();
            completedItems.AddRange(statusChanges.Where(h => h.ChangedBy == user.Username).OrderByDescending(h => h.ChangedAtUtc)
                .Select(h => new UserActivityItemDto("Case status change", h.Case.CaseNumber, $"{h.FromStatus} -> {h.ToStatus}", h.ToStatus.ToString(), h.ChangedAtUtc)));
            completedItems.AddRange(completedInvestigations.Where(i => i.InvestigatorUserId == user.Id).OrderByDescending(i => i.ApprovedAtUtc)
                .Select(i => new UserActivityItemDto("Investigation", i.Case.CaseNumber, i.Recommendation, i.Status.ToString(), i.ApprovedAtUtc)));
            completedItems.AddRange(assessments.Where(a => a.AssessorUserId == user.Id).OrderByDescending(a => a.CreatedAtUtc)
                .Select(a => new UserActivityItemDto("Case assessment", a.Case.CaseNumber, a.Conclusion, "Completed", a.CreatedAtUtc)));

            var pendingItems = new List<UserActivityItemDto>();
            pendingItems.AddRange(openInvestigations.Where(i => i.InvestigatorUserId == user.Id)
                .Select(i => new UserActivityItemDto("Investigation", i.Case.CaseNumber, i.Findings, "In progress", null)));
            pendingItems.AddRange(openCorrectiveActions.Where(ca => ca.CreatedBy == user.Username)
                .Select(ca => new UserActivityItemDto("Corrective action", ca.Case.CaseNumber, ca.Description, ca.Status.ToString(), null)));

            var summary = summaries.Single(s => s.UserId == user.Id);
            detail = new UserActivityDetailDto(summary, completedItems.OrderByDescending(i => i.OccurredAtUtc).ToList(), pendingItems);
            summaries = new List<UserActivitySummaryDto> { summary };
        }

        return new UserActivityReportDto(generatedAtUtc, fromUtc, toUtc, summaries, detail);
    }

    public async Task<byte[]> ExportPdfAsync(Guid? userId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var report = await GetAsync(userId, fromUtc, toUtc, cancellationToken);
        return BuildPdf(report);
    }

    private static byte[] BuildPdf(UserActivityReportDto report)
    {
        var pages = new List<PdfCanvas> { BuildCoverPage(report), BuildChartsPage(report) };
        pages.AddRange(BuildUsersTablePages(report.Users));

        if (report.Detail is { } detail)
        {
            pages.Add(BuildDetailKpiPage(detail));
            pages.AddRange(BuildItemsTablePages("Work Completed", detail.Completed));
            pages.AddRange(BuildItemsTablePages("Outstanding Work (To Do)", detail.Pending));
        }

        for (var i = 0; i < pages.Count; i++)
        {
            pages[i].TextRightAligned(PdfCanvas.PageWidth - PdfCanvas.Margin, PdfCanvas.PageHeight - 18, $"Page {i + 1} of {pages.Count}", 8, "F2", 0.5, 0.5, 0.52);
        }

        return PdfReportBuilder.Build(pages);
    }

    private static PdfCanvas BuildCoverPage(UserActivityReportDto report)
    {
        var c = new PdfCanvas();
        c.Fill(0.09, 0.16, 0.33);
        c.FillRect(0, 0, PdfCanvas.PageWidth, 80);
        var title = report.Detail is not null ? $"User Activity Report - {report.Detail.Summary.DisplayName}" : "IFWEMS - User Activity Report";
        c.Text(PdfCanvas.Margin, 36, title, 18, "F3", 1, 1, 1);
        var period = report.FromUtc is { } f && report.ToUtc is { } t
            ? $"Period {f:yyyy-MM-dd} to {t:yyyy-MM-dd}"
            : "Period: last 3 months";
        c.Text(PdfCanvas.Margin, 58, $"{period}  |  Generated {report.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC", 10, "F2", 0.85, 0.88, 0.95);

        c.Text(PdfCanvas.Margin, 110, "Summary", 13, "F3", 0.12, 0.12, 0.15);
        var totalCompleted = report.Users.Sum(u => u.TasksCompletedTotal);
        var totalPending = report.Users.Sum(u => u.TasksPendingTotal);
        var usersWithPending = report.Users.Count(u => u.TasksPendingTotal > 0);
        var kpis = new (string Label, string Value, (double R, double G, double B) Color)[]
        {
            ("Users in report", report.Users.Count.ToString(CultureInfo.InvariantCulture), (0.16, 0.42, 0.80)),
            ("Tasks completed", totalCompleted.ToString(CultureInfo.InvariantCulture), (0.16, 0.65, 0.27)),
            ("Tasks outstanding", totalPending.ToString(CultureInfo.InvariantCulture), (0.86, 0.62, 0.11)),
            ("Users with open work", usersWithPending.ToString(CultureInfo.InvariantCulture), (0.70, 0.20, 0.20))
        };

        const int cols = 2;
        const double gap = 14, cardH = 70, rowGap = 14, startY = 130;
        var cardW = (PdfCanvas.PageWidth - 2 * PdfCanvas.Margin - (cols - 1) * gap) / cols;
        for (var i = 0; i < kpis.Length; i++)
        {
            var k = kpis[i];
            var x = PdfCanvas.Margin + i % cols * (cardW + gap);
            var yTop = startY + i / cols * (cardH + rowGap);
            c.Fill(0.97, 0.97, 0.98);
            c.FillRect(x, yTop, cardW, cardH);
            c.Fill(k.Color.R, k.Color.G, k.Color.B);
            c.FillRect(x, yTop, 5, cardH);
            c.Text(x + 14, yTop + 22, k.Label, 9.5, "F2", 0.35, 0.35, 0.4);
            c.Text(x + 14, yTop + 50, k.Value, 20, "F3", 0.12, 0.12, 0.15);
        }

        c.Text(PdfCanvas.Margin, 310, "What this report shows", 11, "F3", 0.12, 0.12, 0.15);
        c.Text(PdfCanvas.Margin, 328, "Completed: case status changes, completed investigations, completed assessments and audit actions.", 9, "F2", 0.3, 0.3, 0.34);
        c.Text(PdfCanvas.Margin, 342, "Outstanding (to do): investigations in progress and corrective actions not yet verified, assigned to the user.", 9, "F2", 0.3, 0.3, 0.34);

        return c;
    }

    private static PdfCanvas BuildChartsPage(UserActivityReportDto report)
    {
        var c = new PdfCanvas();
        c.Text(PdfCanvas.Margin, 40, "Activity Overview", 14, "F3", 0.12, 0.12, 0.15);

        var topCompleted = report.Users.OrderByDescending(u => u.TasksCompletedTotal).Take(8)
            .Select(u => (Label: ShortName(u.DisplayName), Value: (double)u.TasksCompletedTotal, Color: (0.16, 0.65, 0.27)))
            .ToList();
        PdfReportBuilder.DrawBarChart(c, PdfCanvas.Margin, 70, PdfCanvas.PageWidth - 2 * PdfCanvas.Margin, 200, "Tasks Completed per User (top 8)", topCompleted);

        var topPending = report.Users.OrderByDescending(u => u.TasksPendingTotal).Take(8)
            .Select(u => (Label: ShortName(u.DisplayName), Value: (double)u.TasksPendingTotal, Color: (0.86, 0.62, 0.11)))
            .ToList();
        PdfReportBuilder.DrawBarChart(c, PdfCanvas.Margin, 300, PdfCanvas.PageWidth - 2 * PdfCanvas.Margin, 200, "Outstanding Work per User (top 8)", topPending);

        var breakdown = new List<(string Label, double Value, (double R, double G, double B) Color)>
        {
            ("Status changes", report.Users.Sum(u => u.StatusChangesMade), (0.16, 0.42, 0.80)),
            ("Investigations", report.Users.Sum(u => u.InvestigationsCompleted), (0.55, 0.40, 0.85)),
            ("Assessments", report.Users.Sum(u => u.AssessmentsCompleted), (0.16, 0.65, 0.27))
        };
        PdfReportBuilder.DrawPieChart(c, PdfCanvas.Margin + 100, 630, 90, "Completed Work Breakdown", breakdown, PdfCanvas.Margin + 230, 570);

        return c;
    }

    private static List<PdfCanvas> BuildUsersTablePages(IReadOnlyList<UserActivitySummaryDto> users)
    {
        var columnWidths = new double[] { 110, 90, 90, 70, 70, 90 };
        var headers = new[] { "User", "Org Unit", "Roles", "Completed", "Pending", "Last Activity" };
        var rows = users.Select(u => new[]
        {
            u.DisplayName,
            u.OrgUnitName ?? "-",
            u.Roles.Count > 0 ? string.Join(", ", u.Roles) : "-",
            u.TasksCompletedTotal.ToString(CultureInfo.InvariantCulture),
            u.TasksPendingTotal.ToString(CultureInfo.InvariantCulture),
            u.LastActivityAtUtc?.ToString("yyyy-MM-dd") ?? "-"
        }).ToList();

        return PaginateTable("Users - Activity Summary", columnWidths, headers, rows);
    }

    private static List<PdfCanvas> BuildItemsTablePages(string heading, IReadOnlyList<UserActivityItemDto> items)
    {
        var columnWidths = new double[] { 90, 80, 190, 80, 80 };
        var headers = new[] { "Type", "Reference", "Detail", "Status", "Date" };
        var rows = items.Select(i => new[]
        {
            i.Type, i.Reference, i.Detail ?? "-", i.Status, i.OccurredAtUtc?.ToString("yyyy-MM-dd") ?? "-"
        }).ToList();

        return PaginateTable(heading, columnWidths, headers, rows);
    }

    private static List<PdfCanvas> PaginateTable(string heading, double[] columnWidths, string[] headers, IReadOnlyList<string[]> rows)
    {
        const double rowHeight = 16;
        var usableHeight = PdfCanvas.PageHeight - PdfCanvas.Margin - 70;
        var rowsPerPage = Math.Max(5, (int)(usableHeight / rowHeight) - 1);

        var pages = new List<PdfCanvas>();
        if (rows.Count == 0)
        {
            var c = new PdfCanvas();
            c.Text(PdfCanvas.Margin, 40, heading, 14, "F3", 0.12, 0.12, 0.15);
            c.Text(PdfCanvas.Margin, 60, "(no records)", 9, "F2", 0.5, 0.5, 0.5);
            pages.Add(c);
            return pages;
        }

        for (var i = 0; i < rows.Count; i += rowsPerPage)
        {
            var c = new PdfCanvas();
            var pageRows = rows.Skip(i).Take(rowsPerPage).ToList();
            c.Text(PdfCanvas.Margin, 40, heading, 14, "F3", 0.12, 0.12, 0.15);
            PdfReportBuilder.DrawTable(c, PdfCanvas.Margin, 56, columnWidths, headers, pageRows, rowHeight);
            pages.Add(c);
        }
        return pages;
    }

    private static PdfCanvas BuildDetailKpiPage(UserActivityDetailDto detail)
    {
        var c = new PdfCanvas();
        var s = detail.Summary;
        c.Text(PdfCanvas.Margin, 40, $"{s.DisplayName} - Detail", 14, "F3", 0.12, 0.12, 0.15);
        c.Text(PdfCanvas.Margin, 58, $"{s.OrgUnitName ?? "-"}  |  {(s.Roles.Count > 0 ? string.Join(", ", s.Roles) : "No role assigned")}", 9, "F2", 0.4, 0.4, 0.42);

        var breakdown = new List<(string Label, double Value, (double R, double G, double B) Color)>
        {
            ("Status changes", s.StatusChangesMade, (0.16, 0.42, 0.80)),
            ("Investigations done", s.InvestigationsCompleted, (0.55, 0.40, 0.85)),
            ("Assessments done", s.AssessmentsCompleted, (0.16, 0.65, 0.27)),
            ("Audit actions", s.AuditActions, (0.45, 0.45, 0.48))
        };
        PdfReportBuilder.DrawBarChart(c, PdfCanvas.Margin, 90, PdfCanvas.PageWidth - 2 * PdfCanvas.Margin, 220, "Completed Work", breakdown);

        var workload = new List<(string Label, double Value, (double R, double G, double B) Color)>
        {
            ("Investigations in progress", s.OpenInvestigationsAssigned, (0.86, 0.62, 0.11)),
            ("Corrective actions open", s.OpenCorrectiveActionsAssigned, (0.70, 0.20, 0.20))
        };
        PdfReportBuilder.DrawBarChart(c, PdfCanvas.Margin, 340, PdfCanvas.PageWidth - 2 * PdfCanvas.Margin, 220, "Outstanding Work (To Do)", workload);

        return c;
    }

    private static string ShortName(string name) => name.Length > 14 ? name[..13] + "~" : name;
}
