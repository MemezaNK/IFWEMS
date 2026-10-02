using System.Globalization;
using System.Text;

namespace IFWEMS.Application.Common.Reporting;

/// <summary>
/// Minimal, dependency-free PDF 1.4 writer used to build client-ready reports with vector bar/pie
/// charts and tables (no third-party PDF/charting library). Pages are A4 portrait. Coordinates for
/// drawing operations are "distance from the top of the page" for caller convenience and are
/// flipped to PDF's bottom-left origin internally.
/// </summary>
public sealed class PdfCanvas
{
    public const double PageWidth = 595;
    public const double PageHeight = 842;
    public const double Margin = 36;

    private readonly StringBuilder _sb = new();
    internal string Content => _sb.ToString();

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    public void Fill(double r, double g, double b) => _sb.Append(F(r)).Append(' ').Append(F(g)).Append(' ').Append(F(b)).Append(" rg\n");

    public void Stroke(double r, double g, double b) => _sb.Append(F(r)).Append(' ').Append(F(g)).Append(' ').Append(F(b)).Append(" RG\n");

    public void LineWidth(double w) => _sb.Append(F(w)).Append(" w\n");

    /// <summary>Fills a rectangle; <paramref name="yTop"/> is the distance from the top of the page to the rectangle's top edge.</summary>
    public void FillRect(double x, double yTop, double w, double h) =>
        _sb.Append(F(x)).Append(' ').Append(F(PageHeight - yTop - h)).Append(' ').Append(F(w)).Append(' ').Append(F(h)).Append(" re f\n");

    public void StrokeRect(double x, double yTop, double w, double h) =>
        _sb.Append(F(x)).Append(' ').Append(F(PageHeight - yTop - h)).Append(' ').Append(F(w)).Append(' ').Append(F(h)).Append(" re S\n");

    public void Line(double x1, double yTop1, double x2, double yTop2) =>
        _sb.Append(F(x1)).Append(' ').Append(F(PageHeight - yTop1)).Append(" m ").Append(F(x2)).Append(' ').Append(F(PageHeight - yTop2)).Append(" l S\n");

    /// <summary>Fills a closed polygon (used for pie/donut slices), given top-down coordinates.</summary>
    public void FillPolygon(IReadOnlyList<(double X, double YTop)> points)
    {
        if (points.Count < 3) return;
        _sb.Append(F(points[0].X)).Append(' ').Append(F(PageHeight - points[0].YTop)).Append(" m ");
        foreach (var p in points.Skip(1)) _sb.Append(F(p.X)).Append(' ').Append(F(PageHeight - p.YTop)).Append(" l ");
        _sb.Append("h f\n");
    }

    /// <summary>Draws left-aligned text at (x, yTop); font is "F1" (Courier), "F2" (Helvetica) or "F3" (Helvetica-Bold).</summary>
    public void Text(double x, double yTop, string text, double size, string font = "F2", double r = 0, double g = 0, double b = 0)
    {
        if (string.IsNullOrEmpty(text)) return;
        _sb.Append("BT ").Append(F(r)).Append(' ').Append(F(g)).Append(' ').Append(F(b)).Append(" rg /").Append(font).Append(' ')
           .Append(F(size)).Append(" Tf ").Append(F(x)).Append(' ').Append(F(PageHeight - yTop)).Append(" Td (")
           .Append(PdfEscape(text)).Append(") Tj ET\n");
    }

    public void TextCentered(double xCenter, double yTop, string text, double size, string font = "F2", double r = 0, double g = 0, double b = 0) =>
        Text(xCenter - EstimateWidth(text, size, font) / 2, yTop, text, size, font, r, g, b);

    public void TextRightAligned(double xRight, double yTop, string text, double size, string font = "F2", double r = 0, double g = 0, double b = 0) =>
        Text(xRight - EstimateWidth(text, size, font), yTop, text, size, font, r, g, b);

    /// <summary>Rough glyph-width estimate (Helvetica averages ~0.5em, bold ~0.56em); good enough for non-interactive report layout.</summary>
    public static double EstimateWidth(string text, double size, string font) => text.Length * size * (font == "F3" ? 0.56 : font == "F1" ? 0.6 : 0.5);

    internal static string PdfEscape(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch is '(' or ')' or '\\') sb.Append('\\').Append(ch);
            else if (ch < 32 || ch > 126) sb.Append('?');
            else sb.Append(ch);
        }
        return sb.ToString();
    }
}

/// <summary>Assembles a PDF from pre-built <see cref="PdfCanvas"/> pages and provides reusable chart/table drawing helpers.</summary>
public static class PdfReportBuilder
{
    /// <summary>Assembles a PDF from pre-built pages (Courier/Helvetica/Helvetica-Bold fonts available as F1/F2/F3).</summary>
    public static byte[] Build(IReadOnlyList<PdfCanvas> pages)
    {
        var pageObjectIds = new List<int>();
        var nextId = 6;
        var pageObjects = new List<(int PageId, int ContentId, string Content)>();
        foreach (var page in pages)
        {
            var pageId = nextId++;
            var contentId = nextId++;
            pageObjectIds.Add(pageId);
            pageObjects.Add((pageId, contentId, page.Content));
        }

        var bodies = new SortedDictionary<int, string>
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = $"<< /Type /Pages /Kids [{string.Join(' ', pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pageObjectIds.Count} >>",
            [3] = "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>",
            [4] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            [5] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
        };
        foreach (var (pageId, contentId, content) in pageObjects)
        {
            bodies[pageId] =
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PdfCanvas.PageWidth} {PdfCanvas.PageHeight}] /Resources << /Font << /F1 3 0 R /F2 4 0 R /F3 5 0 R >> >> /Contents {contentId} 0 R >>";
            bodies[contentId] = $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream";
        }

        var output = new StringBuilder();
        output.Append("%PDF-1.4\n");
        var offsets = new Dictionary<int, int>();
        foreach (var (id, text) in bodies)
        {
            offsets[id] = output.Length;
            output.Append(id).Append(" 0 obj\n").Append(text).Append("\nendobj\n");
        }
        var xref = output.Length;
        var count = bodies.Count + 1;
        output.Append("xref\n0 ").Append(count).Append('\n').Append("0000000000 65535 f \n");
        for (var id = 1; id < count; id++) output.Append(offsets[id].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        output.Append("trailer\n<< /Size ").Append(count).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(output.ToString());
    }

    /// <summary>Vertical bar chart. <paramref name="data"/> is (label, value, RGB colour).</summary>
    public static void DrawBarChart(PdfCanvas c, double x, double yTop, double w, double h, string title,
        IReadOnlyList<(string Label, double Value, (double R, double G, double B) Color)> data, Func<double, string>? formatValue = null)
    {
        formatValue ??= v => v.ToString("0", CultureInfo.InvariantCulture);
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

        var maxVal = Math.Max(1, data.Max(d => d.Value));
        var gap = 16.0;
        var barWidth = Math.Max(4, (w - gap * (data.Count + 1)) / data.Count);
        for (var i = 0; i < data.Count; i++)
        {
            var d = data[i];
            var barH = d.Value <= 0 ? 0 : d.Value / maxVal * chartHeight;
            var bx = x + gap + i * (barWidth + gap);
            var topY = chartBottom - barH;
            if (barH > 0)
            {
                c.Fill(d.Color.R, d.Color.G, d.Color.B);
                c.FillRect(bx, topY, barWidth, barH);
            }
            c.TextCentered(bx + barWidth / 2, topY - 6, formatValue(d.Value), 8, "F3", 0.15, 0.15, 0.18);
            var label = d.Label.Length > 12 ? d.Label[..11] + "~" : d.Label;
            c.TextCentered(bx + barWidth / 2, chartBottom + 14, label, 7.5, "F2", 0.35, 0.35, 0.38);
        }
    }

    /// <summary>Pie chart with a side legend. <paramref name="data"/> is (label, value, RGB colour).</summary>
    public static void DrawPieChart(PdfCanvas c, double cx, double cyTop, double radius, string title,
        IReadOnlyList<(string Label, double Value, (double R, double G, double B) Color)> data, double legendX, double legendYTop)
    {
        c.Text(cx - radius, cyTop - radius - 18, title, 11, "F3", 0.12, 0.12, 0.15);
        var total = data.Sum(d => d.Value);
        if (total <= 0)
        {
            c.Text(cx - 50, cyTop, "No data available", 9, "F2", 0.5, 0.5, 0.5);
            return;
        }

        var startAngle = 90.0;
        foreach (var d in data)
        {
            if (d.Value <= 0) continue;
            var sweep = d.Value / total * 360.0;
            var endAngle = startAngle - sweep;
            c.Fill(d.Color.R, d.Color.G, d.Color.B);
            c.FillPolygon(PieSlicePoints(cx, cyTop, radius, endAngle, startAngle));
            startAngle = endAngle;
        }

        var ly = legendYTop;
        foreach (var d in data)
        {
            c.Fill(d.Color.R, d.Color.G, d.Color.B);
            c.FillRect(legendX, ly, 10, 10);
            var pct = Math.Round(d.Value / total * 100, 1);
            c.Text(legendX + 16, ly + 9, $"{d.Label}: {d.Value:0} ({pct.ToString("0.#", CultureInfo.InvariantCulture)}%)", 9, "F2", 0.2, 0.2, 0.22);
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

    /// <summary>
    /// Draws a simple ruled table (shaded header row, alternating row bands) starting at (x, yTop) and
    /// returns the Y position immediately below the table. <paramref name="columnWidths"/> must sum to
    /// the table width.
    /// </summary>
    public static double DrawTable(PdfCanvas c, double x, double yTop, IReadOnlyList<double> columnWidths, IReadOnlyList<string> headers,
        IReadOnlyList<string[]> rows, double rowHeight = 16)
    {
        var tableWidth = columnWidths.Sum();
        c.Fill(0.12, 0.16, 0.33);
        c.FillRect(x, yTop, tableWidth, rowHeight);
        var cx = x;
        for (var i = 0; i < headers.Count; i++)
        {
            c.Text(cx + 4, yTop + rowHeight - 5, headers[i], 8, "F3", 1, 1, 1);
            cx += columnWidths[i];
        }

        var y = yTop + rowHeight;
        for (var r = 0; r < rows.Count; r++)
        {
            if (r % 2 == 1)
            {
                c.Fill(0.95, 0.95, 0.97);
                c.FillRect(x, y, tableWidth, rowHeight);
            }
            cx = x;
            for (var i = 0; i < rows[r].Length && i < columnWidths.Count; i++)
            {
                var text = rows[r][i] ?? string.Empty;
                var maxChars = Math.Max(4, (int)(columnWidths[i] / (7 * 0.5)));
                if (text.Length > maxChars) text = text[..Math.Max(0, maxChars - 1)] + "~";
                c.Text(cx + 4, y + rowHeight - 5, text, 7.5, "F2", 0.2, 0.2, 0.24);
                cx += columnWidths[i];
            }
            y += rowHeight;
        }

        c.Stroke(0.82, 0.82, 0.85);
        c.LineWidth(0.5);
        c.StrokeRect(x, yTop, tableWidth, rowHeight * (rows.Count + 1));
        return y;
    }
}
