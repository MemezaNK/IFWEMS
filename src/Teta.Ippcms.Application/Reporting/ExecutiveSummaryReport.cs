using System.Globalization;
using Platform.Core;
using Teta.Ippcms.Application.Abstractions;
using Teta.Ippcms.Application.Common;

namespace Teta.Ippcms.Application.Reporting;

public interface IExecutiveSummaryReportService
{
    Task<ExportedFile> ExportAsync(string? financialYear, string format, CancellationToken ct);
}

/// <summary>
/// Board/client-ready Executive Summary: the same data as the Executive Dashboard (KPIs, project
/// health, programme roll-up, APP indicator performance and top exceptions - FR-REP-002), exported
/// as a single PDF with real vector bar/pie charts (no third-party library) or as a multi-sheet
/// Excel workbook for further analysis.
/// </summary>
public sealed class ExecutiveSummaryReportService : IExecutiveSummaryReportService
{
    private readonly IReportingService _reports;
    private readonly ITetaDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public ExecutiveSummaryReportService(IReportingService reports, ITetaDbContext db, IClock clock, IAuditWriter audit)
    {
        _reports = reports;
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task<ExportedFile> ExportAsync(string? financialYear, string format, CancellationToken ct)
    {
        var dashboard = await _reports.ExecutiveDashboardAsync(financialYear, ct);
        var generatedAtUtc = _clock.UtcNow;
        var stamp = generatedAtUtc.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var fileBase = $"Executive-Summary-{dashboard.FinancialYear.Replace('/', '-')}-{stamp}";

        ExportedFile file = format.ToLowerInvariant() switch
        {
            "pdf" => new ExportedFile(BuildPdf(dashboard, generatedAtUtc), "application/pdf", $"{fileBase}.pdf"),
            "xlsx" => new ExportedFile(ReportExport.XlsxMultiSheet(BuildSheets(dashboard, generatedAtUtc)),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileBase}.xlsx"),
            _ => throw new ValidationException("format", "Supported export formats are 'pdf' and 'xlsx'.")
        };

        _audit.Write("Reporting", "ExecutiveSummary", dashboard.FinancialYear, "Export", new { format = format.ToLowerInvariant(), dashboard.FinancialYear });
        await _db.SaveChangesAsync(ct);
        return file;
    }

    // ---------------- Excel (tabular, one sheet per section) ----------------
    private static List<ReportTable> BuildSheets(ExecutiveDashboardDto d, DateTime generatedAtUtc)
    {
        ReportTable Table(string code, string title, string[] columns, IEnumerable<object?[]> rows) =>
            new(code, title, generatedAtUtc, columns, rows.ToList(), new Dictionary<string, string?> { ["Financial Year"] = d.FinancialYear });

        return new List<ReportTable>
        {
            Table("KPIS", "Key Performance Indicators", new[] { "Code", "Label", "Value", "Unit", "Status" },
                d.Kpis.Select(k => new object?[] { k.Code, k.Label, k.Value, k.Unit, k.Status })),
            Table("HEALTH", "Project Health", new[] { "Health", "Count" },
                d.Health.Select(h => new object?[] { h.Health, h.Count })),
            Table("PROGRAMMES", "Programme Roll-up", new[] { "Programme", "Portfolio", "Projects", "Red", "Amber", "Green", "Budget", "Actual" },
                d.Programmes.Select(p => new object?[] { p.Name, p.PortfolioName, p.Projects, p.Red, p.Amber, p.Green, p.Budget, p.Actual })),
            Table("APP_PERF", "APP Indicator Performance", new[] { "Code", "Name", "Target", "Verified Actual", "Achievement %", "Status" },
                d.AppPerformance.Select(p => new object?[] { p.Code, p.Name, p.Target, p.VerifiedActual, p.AchievementPercent, p.Status })),
            Table("EXCEPTIONS", "Top Exceptions", new[] { "Category", "Reference", "Description", "Severity", "Due Date", "Days Overdue" },
                d.TopExceptions.Select(e => new object?[] { e.Category, e.Reference, e.Description, e.Severity, e.DueDate, e.DaysOverdue }))
        };
    }

    // ---------------- PDF (cover + KPI cards, bar/pie charts, tables) ----------------
    private static byte[] BuildPdf(ExecutiveDashboardDto d, DateTime generatedAtUtc)
    {
        var pages = new List<ReportExport.PdfCanvas> { BuildCoverPage(d, generatedAtUtc), BuildHealthFinancePage(d), BuildPerformancePage(d) };
        pages.AddRange(BuildMonoTablePages("Programme Roll-up", $"Financial Year {d.FinancialYear}",
            new[] { "Programme", "Portfolio", "Projects", "Red", "Amber", "Green", "Budget (ZAR)", "Actual (ZAR)" },
            d.Programmes.Select(p => new object?[] { p.Name, p.PortfolioName, p.Projects, p.Red, p.Amber, p.Green, p.Budget, p.Actual }).ToList()));
        pages.AddRange(BuildMonoTablePages("Top Exceptions", $"Financial Year {d.FinancialYear} - {d.TopExceptions.Count} shown, ordered by severity",
            new[] { "Category", "Reference", "Description", "Severity", "Due Date", "Days Overdue" },
            d.TopExceptions.Select(e => new object?[] { e.Category, e.Reference, e.Description, e.Severity, e.DueDate, e.DaysOverdue }).ToList()));

        for (var i = 0; i < pages.Count; i++)
        {
            pages[i].Text(ReportExport.PageWidth - ReportExport.Margin - 90, ReportExport.PageHeight - 18, $"Page {i + 1} of {pages.Count}", 8, "F2", 0.5, 0.5,
                0.52);
        }

        return ReportExport.BuildPdfFromCanvases(pages);
    }

    private static ReportExport.PdfCanvas BuildCoverPage(ExecutiveDashboardDto d, DateTime generatedAtUtc)
    {
        var c = new ReportExport.PdfCanvas();
        c.Fill(0.09, 0.16, 0.33);
        c.FillRect(0, 0, ReportExport.PageWidth, 70);
        c.Text(ReportExport.Margin, 32, "TETA - Executive Summary Report", 20, "F3", 1, 1, 1);
        c.Text(ReportExport.Margin, 54, $"Financial Year {d.FinancialYear}  |  Generated {generatedAtUtc:yyyy-MM-dd HH:mm} UTC", 10, "F2", 0.85, 0.88, 0.95);
        c.Text(ReportExport.Margin, 98, "Key Performance Indicators", 12, "F3", 0.12, 0.12, 0.15);

        const int cols = 4;
        const double gap = 12, cardH = 78, rowGap = 12, startY = 110;
        var cardW = (ReportExport.PageWidth - 2 * ReportExport.Margin - (cols - 1) * gap) / cols;

        for (var i = 0; i < d.Kpis.Count; i++)
        {
            var k = d.Kpis[i];
            var x = ReportExport.Margin + i % cols * (cardW + gap);
            var yTop = startY + i / cols * (cardH + rowGap);
            var (r, g, b) = RagColor(k.Status);

            c.Fill(0.97, 0.97, 0.98);
            c.FillRect(x, yTop, cardW, cardH);
            c.Fill(r, g, b);
            c.FillRect(x, yTop, 5, cardH);

            var label = k.Label.Length > 42 ? k.Label[..41] + "~" : k.Label;
            c.Text(x + 14, yTop + 20, label, 8.5, "F2", 0.35, 0.35, 0.4);
            c.Text(x + 14, yTop + 48, FormatKpiValue(k), 17, "F3", 0.12, 0.12, 0.15);
            if (k.Status != "Info") c.TextRightAligned(x + cardW - 10, yTop + 16, k.Status, 7, "F2", r, g, b);
        }

        return c;
    }

    private static ReportExport.PdfCanvas BuildHealthFinancePage(ExecutiveDashboardDto d)
    {
        var c = new ReportExport.PdfCanvas();
        c.Text(ReportExport.Margin, 40, "Project Health & Financial Position", 14, "F3", 0.12, 0.12, 0.15);
        c.Text(ReportExport.Margin, 58, $"Financial Year {d.FinancialYear}", 9, "F2", 0.4, 0.4, 0.42);

        var healthData = d.Health.Select(h => (
            Label: h.Health == "NotAssessed" ? "Not Assessed" : h.Health,
            Value: (double)h.Count,
            Color: h.Health is "Red" or "Amber" or "Green" ? RagColor(h.Health) : (0.55, 0.55, 0.55))).ToList();
        DrawBarChart(c, ReportExport.Margin, 90, 360, 250, "Active Projects by Health", healthData,
            v => v.ToString("0", CultureInfo.InvariantCulture));

        decimal KpiValue(string code) => d.Kpis.FirstOrDefault(k => k.Code == code)?.Value ?? 0;
        var financeData = new List<(string Label, double Value, (double R, double G, double B) Color)>
        {
            ("Budget", (double)KpiValue("BUDGET"), (0.16, 0.42, 0.80)),
            ("Committed", (double)KpiValue("COMMITTED"), (0.55, 0.40, 0.85)),
            ("Actual", (double)KpiValue("SPEND"), (0.16, 0.65, 0.27))
        };
        DrawBarChart(c, ReportExport.Margin + 400, 90, 362, 250, "Budget vs Committed vs Actual (ZAR)", financeData,
            v => "R " + v.ToString("N0", CultureInfo.InvariantCulture));

        return c;
    }

    private static ReportExport.PdfCanvas BuildPerformancePage(ExecutiveDashboardDto d)
    {
        var c = new ReportExport.PdfCanvas();
        c.Text(ReportExport.Margin, 40, "APP Performance & Top Exceptions", 14, "F3", 0.12, 0.12, 0.15);
        c.Text(ReportExport.Margin, 58, $"Financial Year {d.FinancialYear}", 9, "F2", 0.4, 0.4, 0.42);

        var statusGroups = d.AppPerformance.GroupBy(p => p.Status)
            .Select(g => (Label: g.Key, Value: (double)g.Count(), Color: RagColor(g.Key)))
            .OrderByDescending(x => x.Value)
            .ToList();
        DrawPieChart(c, ReportExport.Margin + 120, 230, 100, "APP Indicator Status", statusGroups, ReportExport.Margin + 260, 130);

        var severityColors = new Dictionary<string, (double R, double G, double B)>
        {
            ["Critical"] = (0.82, 0.18, 0.18), ["High"] = (0.93, 0.45, 0.13), ["Medium"] = (0.93, 0.62, 0.07), ["Low"] = (0.35, 0.60, 0.85)
        };
        var severityData = new[] { "Critical", "High", "Medium", "Low" }
            .Select(s => (Label: s, Value: (double)d.TopExceptions.Count(e => e.Severity == s), Color: severityColors[s]))
            .ToList();
        DrawBarChart(c, ReportExport.Margin + 420, 90, 340, 250, "Top Exceptions by Severity", severityData,
            v => v.ToString("0", CultureInfo.InvariantCulture));

        return c;
    }

    // ---------------- Chart drawing (generic bar/pie helpers over PdfCanvas) ----------------
    private static void DrawBarChart(ReportExport.PdfCanvas c, double x, double yTop, double w, double h, string title,
        IReadOnlyList<(string Label, double Value, (double R, double G, double B) Color)> data, Func<double, string> formatValue)
    {
        c.Text(x, yTop, title, 11, "F3", 0.12, 0.12, 0.15);
        var chartTop = yTop + 20;
        var chartHeight = h - 50;
        var chartBottom = chartTop + chartHeight;

        c.Stroke(0.75, 0.75, 0.78);
        c.LineWidth(1);
        c.Line(x, chartBottom, x + w, chartBottom);

        if (data.Count == 0)
        {
            c.Text(x + 10, chartTop + chartHeight / 2, "No data available", 9, "F2", 0.5, 0.5, 0.5);
            return;
        }

        var maxVal = Math.Max(1, data.Max(dd => dd.Value));
        var gap = 20.0;
        var barWidth = (w - gap * (data.Count + 1)) / data.Count;
        for (var i = 0; i < data.Count; i++)
        {
            var dd = data[i];
            var barH = dd.Value <= 0 ? 0 : dd.Value / maxVal * chartHeight;
            var bx = x + gap + i * (barWidth + gap);
            var topY = chartBottom - barH;
            if (barH > 0)
            {
                c.Fill(dd.Color.R, dd.Color.G, dd.Color.B);
                c.FillRect(bx, topY, barWidth, barH);
            }
            c.TextCentered(bx + barWidth / 2, topY - 6, formatValue(dd.Value), 8, "F3", 0.15, 0.15, 0.18);
            c.TextCentered(bx + barWidth / 2, chartBottom + 14, dd.Label, 8, "F2", 0.35, 0.35, 0.38);
        }
    }

    private static void DrawPieChart(ReportExport.PdfCanvas c, double cx, double cyTop, double radius, string title,
        IReadOnlyList<(string Label, double Value, (double R, double G, double B) Color)> data, double legendX, double legendYTop)
    {
        c.Text(cx - radius, cyTop - radius - 18, title, 11, "F3", 0.12, 0.12, 0.15);
        var total = data.Sum(dd => dd.Value);
        if (total <= 0)
        {
            c.Text(cx - 50, cyTop, "No data available", 9, "F2", 0.5, 0.5, 0.5);
            return;
        }

        var startAngle = 90.0;
        foreach (var dd in data)
        {
            if (dd.Value <= 0) continue;
            var sweep = dd.Value / total * 360.0;
            var endAngle = startAngle - sweep;
            c.Fill(dd.Color.R, dd.Color.G, dd.Color.B);
            c.FillPolygon(PieSlicePoints(cx, cyTop, radius, endAngle, startAngle));
            startAngle = endAngle;
        }

        var ly = legendYTop;
        foreach (var dd in data)
        {
            c.Fill(dd.Color.R, dd.Color.G, dd.Color.B);
            c.FillRect(legendX, ly, 10, 10);
            var pct = Math.Round(dd.Value / total * 100, 1);
            c.Text(legendX + 16, ly + 9, $"{dd.Label}: {dd.Value:0} ({pct.ToString("0.#", CultureInfo.InvariantCulture)}%)", 9, "F2", 0.2, 0.2, 0.22);
            ly += 18;
        }
    }

    private static List<(double X, double YTop)> PieSlicePoints(double cx, double cyTop, double radius, double startDeg, double endDeg)
    {
        var points = new List<(double, double)> { (cx, cyTop) };
        var segments = Math.Max(2, (int)(Math.Abs(endDeg - startDeg) / 6) + 1);
        for (var i = 0; i <= segments; i++)
        {
            var t = startDeg + (endDeg - startDeg) * i / segments;
            var rad = t * Math.PI / 180.0;
            points.Add((cx + radius * Math.Cos(rad), cyTop - radius * Math.Sin(rad)));
        }
        return points;
    }

    // ---------------- Monospace table pages (programme roll-up, top exceptions) ----------------
    private static List<ReportExport.PdfCanvas> BuildMonoTablePages(string heading, string subheading, string[] columns, IReadOnlyList<object?[]> rows)
    {
        var charWidth = ReportExport.FontSize * 0.6;
        var maxChars = (int)((ReportExport.PageWidth - 2 * ReportExport.Margin) / charWidth);

        var widths = columns.Select((col, i) =>
            Math.Min(40, Math.Max(col.Length, rows.Take(500).Select(r => i < r.Length ? ReportExport.FormatCell(r[i]).Length : 0).DefaultIfEmpty(0).Max())))
            .ToArray();
        var total = widths.Sum() + widths.Length - 1;
        if (total > maxChars)
        {
            var scale = (double)(maxChars - widths.Length + 1) / widths.Sum();
            widths = widths.Select(w => Math.Max(4, (int)Math.Floor(w * scale))).ToArray();
        }

        string Line(IEnumerable<string> cells) => string.Join(" ", cells.Select((cell, i) =>
        {
            var w = widths[i];
            var text = (cell ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
            return text.Length > w ? text[..Math.Max(0, w - 1)] + "~" : text.PadRight(w);
        }));

        var headerLine = Line(columns);
        var sepLine = new string('-', Math.Min(maxChars, widths.Sum() + widths.Length - 1));
        var bodyLines = rows.Count == 0 ? new List<string> { "(no records)" } : rows.Select(r => Line(r.Select(ReportExport.FormatCell))).ToList();

        var linesPerPage = Math.Max(5, (int)((ReportExport.PageHeight - 2 * ReportExport.Margin) / ReportExport.LineHeight) - 5);
        var pages = new List<ReportExport.PdfCanvas>();
        for (var i = 0; i < bodyLines.Count; i += linesPerPage)
        {
            var canvas = new ReportExport.PdfCanvas();
            var y = ReportExport.Margin;
            canvas.Text(ReportExport.Margin, y, heading, 13, "F3", 0.09, 0.16, 0.33);
            y += 20;
            canvas.Text(ReportExport.Margin, y, subheading, 9, "F2", 0.4, 0.4, 0.42);
            y += 16;
            canvas.Text(ReportExport.Margin, y, headerLine, ReportExport.FontSize, "F1", 0, 0, 0);
            y += ReportExport.LineHeight;
            canvas.Text(ReportExport.Margin, y, sepLine, ReportExport.FontSize, "F1", 0.6, 0.6, 0.6);
            y += ReportExport.LineHeight;
            foreach (var line in bodyLines.Skip(i).Take(linesPerPage))
            {
                canvas.Text(ReportExport.Margin, y, line, ReportExport.FontSize, "F1", 0.15, 0.15, 0.18);
                y += ReportExport.LineHeight;
            }
            pages.Add(canvas);
        }

        return pages;
    }

    private static (double R, double G, double B) RagColor(string status) => status switch
    {
        "Red" => (0.82, 0.18, 0.18),
        "Amber" or "AtRisk" => (0.93, 0.62, 0.07),
        "Green" or "Achieved" => (0.16, 0.65, 0.27),
        "Info" or "OnTrack" => (0.16, 0.42, 0.80),
        _ => (0.55, 0.55, 0.55)
    };

    private static string FormatKpiValue(KpiDto k) => k.Unit switch
    {
        "ZAR" => "R " + k.Value.ToString("N0", CultureInfo.InvariantCulture),
        "%" => k.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%",
        _ => k.Value.ToString("N0", CultureInfo.InvariantCulture)
    };
}
