using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Teta.Ippcms.Application.Reporting;

/// <summary>A tabular report result: the unit that is displayed, exported and scheduled.</summary>
public sealed record ReportTable(string Code, string Title, DateTime GeneratedAtUtc, IReadOnlyList<string> Columns,
    IReadOnlyList<object?[]> Rows, IReadOnlyDictionary<string, string?> Filters)
{
    public int RowCount => Rows.Count;
}

public sealed record ExportedFile(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Report exporters for CSV, Excel (.xlsx) and PDF (FR-REP-001 "Reports export to Excel/PDF").
/// Implemented without third-party libraries: XLSX is a minimal SpreadsheetML package and PDF is a
/// plain PDF 1.4 document using the built-in Courier font (tabular, landscape A4).
/// </summary>
public static class ReportExport
{
    public static readonly IReadOnlyList<string> Formats = new[] { "csv", "xlsx", "pdf" };

    public static ExportedFile Export(ReportTable table, string format)
    {
        var stamp = table.GeneratedAtUtc.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var baseName = $"{table.Code}-{stamp}";
        return format.ToLowerInvariant() switch
        {
            "csv" => new ExportedFile(Csv(table), "text/csv", baseName + ".csv"),
            "xlsx" => new ExportedFile(Xlsx(table), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", baseName + ".xlsx"),
            "pdf" => new ExportedFile(Pdf(table), "application/pdf", baseName + ".pdf"),
            _ => throw new Platform.Core.ValidationException(new Dictionary<string, string[]>
            {
                ["format"] = new[] { "Format must be csv, xlsx or pdf." }
            })
        };
    }

    public static string FormatCell(object? value) => value switch
    {
        null => string.Empty,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.##", CultureInfo.InvariantCulture),
        double f => f.ToString("0.##", CultureInfo.InvariantCulture),
        bool b => b ? "Yes" : "No",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    // ---------------- CSV ----------------
    public static byte[] Csv(ReportTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', table.Columns.Select(c => CsvEscape(c, false))));
        foreach (var row in table.Rows)
        {
            sb.AppendLine(string.Join(',', row.Select(v => CsvEscape(FormatCell(v), v is string))));
        }
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        return preamble.Concat(body).ToArray();
    }

    private static string CsvEscape(string value, bool isText)
    {
        // Neutralise spreadsheet formula injection for free-text cells.
        if (isText && value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    // ---------------- XLSX ----------------
    public static byte[] Xlsx(ReportTable table)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                "</Types>");
            Add(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>");
            var sheetName = SecurityElement.Escape(table.Code);
            Add(zip, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                $"<sheets><sheet name=\"{sheetName}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add(zip, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                "</Relationships>");
            Add(zip, "xl/styles.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"3\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                "<xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs>" +
                "</styleSheet>");

            var sheet = new StringBuilder();
            sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            var r = 1;
            AppendRow(sheet, r++, new object?[] { table.Title }, bold: true);
            AppendRow(sheet, r++, new object?[] { "Generated (UTC)", table.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) }, bold: false);
            foreach (var f in table.Filters.Where(f => !string.IsNullOrEmpty(f.Value)))
            {
                AppendRow(sheet, r++, new object?[] { f.Key, f.Value }, bold: false);
            }
            r++;
            AppendRow(sheet, r++, table.Columns.Cast<object?>().ToArray(), bold: true);
            foreach (var row in table.Rows) AppendRow(sheet, r++, row, bold: false);
            sheet.Append("</sheetData></worksheet>");
            Add(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
        }
        return ms.ToArray();
    }

    private static void AppendRow(StringBuilder sb, int rowNumber, object?[] values, bool bold)
    {
        sb.Append("<row r=\"").Append(rowNumber).Append("\">");
        for (var c = 0; c < values.Length; c++)
        {
            var reference = ColumnName(c) + rowNumber.ToString(CultureInfo.InvariantCulture);
            var v = values[c];
            if (v is null) continue;
            if (!bold && v is int or long or decimal or double)
            {
                var style = v is decimal or double ? " s=\"2\"" : string.Empty;
                sb.Append("<c r=\"").Append(reference).Append('"').Append(style).Append("><v>")
                  .Append(Convert.ToString(v, CultureInfo.InvariantCulture)).Append("</v></c>");
            }
            else
            {
                sb.Append("<c r=\"").Append(reference).Append("\" t=\"inlineStr\"").Append(bold ? " s=\"1\"" : string.Empty)
                  .Append("><is><t xml:space=\"preserve\">").Append(SecurityElement.Escape(StripInvalidXml(FormatCell(v)))).Append("</t></is></c>");
            }
        }
        sb.Append("</row>");
    }

    internal static string ColumnName(int index)
    {
        var name = string.Empty;
        index++;
        while (index > 0)
        {
            var mod = (index - 1) % 26;
            name = (char)('A' + mod) + name;
            index = (index - 1) / 26;
        }
        return name;
    }

    private static string StripInvalidXml(string s) => new(s.Where(ch => ch is '\t' or '\n' or '\r' || ch >= ' ').ToArray());

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    // ---------------- PDF ----------------
    private const double PageWidth = 842, PageHeight = 595, Margin = 30, FontSize = 7, LineHeight = 9;

    public static byte[] Pdf(ReportTable table)
    {
        var charWidth = FontSize * 0.6;
        var maxChars = (int)((PageWidth - 2 * Margin) / charWidth);

        // Column widths from content (bounded) so the table fits the landscape page.
        var widths = table.Columns.Select((c, i) =>
            Math.Min(40, Math.Max(c.Length, table.Rows.Take(500).Select(r => i < r.Length ? FormatCell(r[i]).Length : 0).DefaultIfEmpty(0).Max()))).ToArray();
        var total = widths.Sum() + widths.Length - 1;
        if (total > maxChars)
        {
            var scale = (double)(maxChars - widths.Length + 1) / widths.Sum();
            widths = widths.Select(w => Math.Max(4, (int)Math.Floor(w * scale))).ToArray();
        }

        string Line(IEnumerable<string> cells) => string.Join(" ", cells.Select((c, i) =>
        {
            var w = widths[i];
            var text = c.Replace('\n', ' ').Replace('\r', ' ');
            return text.Length > w ? text[..Math.Max(0, w - 1)] + "~" : text.PadRight(w);
        }));

        var header = new List<string>
        {
            table.Title,
            $"Report {table.Code} - generated {table.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC" +
                (table.Filters.Any(f => !string.IsNullOrEmpty(f.Value))
                    ? " - " + string.Join(", ", table.Filters.Where(f => !string.IsNullOrEmpty(f.Value)).Select(f => $"{f.Key}: {f.Value}"))
                    : string.Empty),
            string.Empty,
            Line(table.Columns),
            new string('-', Math.Min(maxChars, widths.Sum() + widths.Length - 1))
        };
        var body = table.Rows.Select(r => Line(r.Select(FormatCell))).ToList();
        if (body.Count == 0) body.Add("(no records)");

        var linesPerPage = (int)((PageHeight - 2 * Margin) / LineHeight) - header.Count - 1;
        var pages = new List<List<string>>();
        for (var i = 0; i < body.Count; i += linesPerPage)
        {
            pages.Add(header.Concat(body.Skip(i).Take(linesPerPage)).ToList());
        }

        var objects = new List<string>();
        // 1: catalog, 2: pages, 3: font; then per page: page object + content stream.
        var pageObjectIds = new List<int>();
        var nextId = 4;
        var pageObjects = new List<(int PageId, int ContentId, string Content)>();
        for (var p = 0; p < pages.Count; p++)
        {
            var sb = new StringBuilder();
            sb.Append("BT\n/F1 ").Append(FontSize.ToString(CultureInfo.InvariantCulture)).Append(" Tf\n")
              .Append(LineHeight.ToString(CultureInfo.InvariantCulture)).Append(" TL\n")
              .Append(Margin.ToString(CultureInfo.InvariantCulture)).Append(' ')
              .Append((PageHeight - Margin).ToString(CultureInfo.InvariantCulture)).Append(" Td\n");
            foreach (var line in pages[p]) sb.Append('(').Append(PdfEscape(line)).Append(") '\n");
            sb.Append("() '\n(").Append(PdfEscape($"Page {p + 1} of {pages.Count}")).Append(") '\nET");
            var pageId = nextId++;
            var contentId = nextId++;
            pageObjectIds.Add(pageId);
            pageObjects.Add((pageId, contentId, sb.ToString()));
        }

        var bodies = new SortedDictionary<int, string>
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = $"<< /Type /Pages /Kids [{string.Join(' ', pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pageObjectIds.Count} >>",
            [3] = "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>"
        };
        foreach (var (pageId, contentId, content) in pageObjects)
        {
            bodies[pageId] = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentId} 0 R >>";
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

    private static string PdfEscape(string s)
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
