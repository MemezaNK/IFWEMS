using System.Globalization;
using static Teta.Ippcms.Application.Reporting.ReportExport;

namespace Teta.Ippcms.Application.Reporting;

/// <summary>Builds the client-ready PDF for the User Activity Report: cover/KPIs, bar/pie charts and paginated tables.</summary>
internal static class UserActivityReportPdf
{
    public static byte[] Build(UserActivityReportDto report)
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
            pages[i].TextRightAligned(PageWidth - Margin, PageHeight - 16, $"Page {i + 1} of {pages.Count}", 8, "F2", 0.5, 0.5, 0.52);
        }

        return BuildPdfFromCanvases(pages);
    }

    private static PdfCanvas BuildCoverPage(UserActivityReportDto report)
    {
        var c = new PdfCanvas();
        c.Fill(0.09, 0.16, 0.33);
        c.FillRect(0, 0, PageWidth, 64);
        var title = report.Detail is not null ? $"User Activity Report - {report.Detail.Summary.DisplayName}" : "TETA IPPCMS - User Activity Report";
        c.Text(Margin, 30, title, 18, "F3", 1, 1, 1);
        var period = report.FromUtc is { } f && report.ToUtc is { } t
            ? $"Period {f:yyyy-MM-dd} to {t:yyyy-MM-dd}"
            : "Period: last 3 months";
        c.Text(Margin, 48, $"{period}  |  Generated {report.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC", 10, "F2", 0.85, 0.88, 0.95);

        c.Text(Margin, 90, "Summary", 13, "F3", 0.12, 0.12, 0.15);
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

        const int cols = 4;
        const double gap = 14, cardH = 64, startY = 108;
        var cardW = (PageWidth - 2 * Margin - (cols - 1) * gap) / cols;
        for (var i = 0; i < kpis.Length; i++)
        {
            var k = kpis[i];
            var x = Margin + i * (cardW + gap);
            c.Fill(0.97, 0.97, 0.98);
            c.FillRect(x, startY, cardW, cardH);
            c.Fill(k.Color.R, k.Color.G, k.Color.B);
            c.FillRect(x, startY, 5, cardH);
            c.Text(x + 14, startY + 20, k.Label, 9, "F2", 0.35, 0.35, 0.4);
            c.Text(x + 14, startY + 46, k.Value, 18, "F3", 0.12, 0.12, 0.15);
        }

        c.Text(Margin, 210, "What this report shows", 11, "F3", 0.12, 0.12, 0.15);
        c.Text(Margin, 228, "Completed: workflow decisions made, completed corrective actions/risk treatments and resolved issues owned by the user.", 9, "F2", 0.3, 0.3, 0.34);
        c.Text(Margin, 242, "Outstanding (to do): pending workflow approvals for the user's active roles, plus open corrective actions, risk treatments and issues they own.", 9, "F2", 0.3, 0.3, 0.34);

        return c;
    }

    private static PdfCanvas BuildChartsPage(UserActivityReportDto report)
    {
        var c = new PdfCanvas();
        c.Text(Margin, 30, "Activity Overview", 14, "F3", 0.12, 0.12, 0.15);

        var half = (PageWidth - 2 * Margin - 20) / 2;
        var topCompleted = report.Users.OrderByDescending(u => u.TasksCompletedTotal).Take(8)
            .Select(u => (Label: ShortName(u.DisplayName), Value: (double)u.TasksCompletedTotal, Color: (0.16, 0.65, 0.27)))
            .ToList();
        DrawBarChart(c, Margin, 55, half, 200, "Tasks Completed per User (top 8)", topCompleted);

        var topPending = report.Users.OrderByDescending(u => u.TasksPendingTotal).Take(8)
            .Select(u => (Label: ShortName(u.DisplayName), Value: (double)u.TasksPendingTotal, Color: (0.86, 0.62, 0.11)))
            .ToList();
        DrawBarChart(c, Margin + half + 20, 55, half, 200, "Outstanding Work per User (top 8)", topPending);

        var breakdown = new List<(string Label, double Value, (double R, double G, double B) Color)>
        {
            ("Decisions made", report.Users.Sum(u => u.DecisionsMade), (0.16, 0.42, 0.80)),
            ("Corrective actions", report.Users.Sum(u => u.CorrectiveActionsCompleted), (0.55, 0.40, 0.85)),
            ("Risk treatments", report.Users.Sum(u => u.RiskTreatmentsCompleted), (0.16, 0.65, 0.27)),
            ("Issues resolved", report.Users.Sum(u => u.IssuesResolved), (0.80, 0.45, 0.20))
        };
        DrawPieChart(c, Margin + 100, 400, 90, "Completed Work Breakdown", breakdown, Margin + 230, 340);

        return c;
    }

    private static List<PdfCanvas> BuildUsersTablePages(IReadOnlyList<UserActivitySummaryDto> users)
    {
        var columnWidths = new double[] { 150, 230, 90, 90, 120 };
        var headers = new[] { "User", "Roles", "Completed", "Pending", "Last Activity" };
        var rows = users.Select(u => new[]
        {
            u.DisplayName,
            u.Roles.Count > 0 ? string.Join(", ", u.Roles) : "-",
            u.TasksCompletedTotal.ToString(CultureInfo.InvariantCulture),
            u.TasksPendingTotal.ToString(CultureInfo.InvariantCulture),
            u.LastActivityAtUtc?.ToString("yyyy-MM-dd") ?? "-"
        }).ToList();

        return PaginateTable("Users - Activity Summary", columnWidths, headers, rows);
    }

    private static List<PdfCanvas> BuildItemsTablePages(string heading, IReadOnlyList<UserActivityItemDto> items)
    {
        var columnWidths = new double[] { 130, 100, 320, 90, 90 };
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
        var usableHeight = PageHeight - Margin - 55;
        var rowsPerPage = Math.Max(5, (int)(usableHeight / rowHeight) - 1);

        var pages = new List<PdfCanvas>();
        if (rows.Count == 0)
        {
            var c = new PdfCanvas();
            c.Text(Margin, 30, heading, 14, "F3", 0.12, 0.12, 0.15);
            c.Text(Margin, 50, "(no records)", 9, "F2", 0.5, 0.5, 0.5);
            pages.Add(c);
            return pages;
        }

        for (var i = 0; i < rows.Count; i += rowsPerPage)
        {
            var c = new PdfCanvas();
            var pageRows = rows.Skip(i).Take(rowsPerPage).ToList();
            c.Text(Margin, 30, heading, 14, "F3", 0.12, 0.12, 0.15);
            DrawTable(c, Margin, 46, columnWidths, headers, pageRows, rowHeight);
            pages.Add(c);
        }
        return pages;
    }

    private static PdfCanvas BuildDetailKpiPage(UserActivityDetailDto detail)
    {
        var c = new PdfCanvas();
        var s = detail.Summary;
        c.Text(Margin, 30, $"{s.DisplayName} - Detail", 14, "F3", 0.12, 0.12, 0.15);
        c.Text(Margin, 48, s.Roles.Count > 0 ? string.Join(", ", s.Roles) : "No role assigned", 9, "F2", 0.4, 0.4, 0.42);

        var half = (PageWidth - 2 * Margin - 20) / 2;
        var completedBreakdown = new List<(string Label, double Value, (double R, double G, double B) Color)>
        {
            ("Decisions made", s.DecisionsMade, (0.16, 0.42, 0.80)),
            ("Corrective actions done", s.CorrectiveActionsCompleted, (0.55, 0.40, 0.85)),
            ("Risk treatments done", s.RiskTreatmentsCompleted, (0.16, 0.65, 0.27)),
            ("Issues resolved", s.IssuesResolved, (0.80, 0.45, 0.20))
        };
        DrawBarChart(c, Margin, 75, half, 240, "Completed Work", completedBreakdown);

        var pendingBreakdown = new List<(string Label, double Value, (double R, double G, double B) Color)>
        {
            ("Pending approvals", s.PendingDecisions, (0.86, 0.62, 0.11)),
            ("Open corrective actions", s.OpenCorrectiveActions, (0.70, 0.20, 0.20)),
            ("Open risk treatments", s.OpenRiskTreatments, (0.70, 0.40, 0.20)),
            ("Open issues", s.OpenIssues, (0.55, 0.25, 0.25))
        };
        DrawBarChart(c, Margin + half + 20, 75, half, 240, "Outstanding Work (To Do)", pendingBreakdown);

        return c;
    }

    private static void DrawBarChart(PdfCanvas c, double x, double yTop, double w, double h, string title,
        IReadOnlyList<(string Label, double Value, (double R, double G, double B) Color)> data)
    {
        c.Text(x, yTop, title, 10, "F3", 0.12, 0.12, 0.15);
        var chartTop = yTop + 16;
        var chartH = h - 16;
        var max = Math.Max(1, data.Count == 0 ? 1 : data.Max(d => d.Value));
        c.Stroke(0.75, 0.75, 0.78);
        c.LineWidth(0.5);
        c.Line(x, chartTop + chartH, x + w, chartTop + chartH);

        if (data.Count == 0)
        {
            c.Text(x, chartTop + chartH / 2, "(no data)", 8, "F2", 0.5, 0.5, 0.5);
            return;
        }

        var gap = 10.0;
        var barW = (w - gap * (data.Count - 1)) / data.Count;
        for (var i = 0; i < data.Count; i++)
        {
            var d = data[i];
            var barH = max <= 0 ? 0 : d.Value / max * (chartH - 20);
            var barX = x + i * (barW + gap);
            var barYTop = chartTop + chartH - barH;
            c.Fill(d.Color.R, d.Color.G, d.Color.B);
            c.FillRect(barX, barYTop, barW, barH);
            c.TextCentered(barX + barW / 2, barYTop - 4, d.Value.ToString("0", CultureInfo.InvariantCulture), 7.5, "F2", 0.2, 0.2, 0.22);
            c.TextCentered(barX + barW / 2, chartTop + chartH + 10, d.Label, 7, "F2", 0.3, 0.3, 0.34);
        }
    }

    private static void DrawPieChart(PdfCanvas c, double cx, double cyTop, double radius, string title,
        IReadOnlyList<(string Label, double Value, (double R, double G, double B) Color)> data, double legendX, double legendYTop)
    {
        c.Text(cx - radius, cyTop - radius - 16, title, 10, "F3", 0.12, 0.12, 0.15);
        var total = data.Sum(d => d.Value);
        if (total <= 0)
        {
            c.Text(cx - 30, cyTop, "(no data)", 8, "F2", 0.5, 0.5, 0.5);
            return;
        }

        var startAngle = -90.0;
        var legendY = legendYTop;
        foreach (var d in data)
        {
            var sweep = d.Value / total * 360.0;
            if (sweep > 0)
            {
                var points = PieSlicePoints(cx, cyTop, radius, startAngle, startAngle + sweep);
                c.Fill(d.Color.R, d.Color.G, d.Color.B);
                c.FillPolygon(points);
            }
            startAngle += sweep;

            c.Fill(d.Color.R, d.Color.G, d.Color.B);
            c.FillRect(legendX, legendY, 10, 10);
            var pct = total <= 0 ? 0 : d.Value / total * 100.0;
            c.Text(legendX + 16, legendY + 9, $"{d.Label} ({d.Value:0} / {pct:0}%)", 8, "F2", 0.2, 0.2, 0.22);
            legendY += 16;
        }
    }

    private static List<(double X, double YTop)> PieSlicePoints(double cx, double cyTop, double radius, double startAngleDeg, double endAngleDeg)
    {
        var points = new List<(double X, double YTop)> { (cx, cyTop) };
        var steps = Math.Max(2, (int)((endAngleDeg - startAngleDeg) / 6) + 1);
        for (var s = 0; s <= steps; s++)
        {
            var angle = (startAngleDeg + (endAngleDeg - startAngleDeg) * s / steps) * Math.PI / 180.0;
            points.Add((cx + radius * Math.Cos(angle), cyTop + radius * Math.Sin(angle)));
        }
        return points;
    }

    private static void DrawTable(PdfCanvas c, double x, double yTop, IReadOnlyList<double> columnWidths, IReadOnlyList<string> headers,
        IReadOnlyList<string[]> rows, double rowHeight)
    {
        var totalWidth = columnWidths.Sum();

        c.Fill(0.12, 0.16, 0.33);
        c.FillRect(x, yTop, totalWidth, rowHeight);
        var colX = x;
        for (var i = 0; i < headers.Count; i++)
        {
            c.Text(colX + 4, yTop + rowHeight - 5, headers[i], 8.5, "F3", 1, 1, 1);
            colX += columnWidths[i];
        }

        var rowY = yTop + rowHeight;
        for (var r = 0; r < rows.Count; r++)
        {
            c.Fill(r % 2 == 0 ? 1 : 0.96, r % 2 == 0 ? 1 : 0.96, r % 2 == 0 ? 1 : 0.97);
            c.FillRect(x, rowY, totalWidth, rowHeight);
            colX = x;
            var row = rows[r];
            for (var i = 0; i < row.Length && i < columnWidths.Count; i++)
            {
                var text = row[i].Length > 48 ? row[i][..47] + "~" : row[i];
                c.Text(colX + 4, rowY + rowHeight - 5, text, 8, "F2", 0.2, 0.2, 0.22);
                colX += columnWidths[i];
            }
            rowY += rowHeight;
        }

        c.Stroke(0.8, 0.8, 0.82);
        c.LineWidth(0.5);
        c.StrokeRect(x, yTop, totalWidth, rowHeight * (rows.Count + 1));
    }

    private static string ShortName(string name) => name.Length > 14 ? name[..13] + "~" : name;
}
