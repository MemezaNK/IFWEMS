using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Platform.Audit;
using Platform.Security.Crypto;
using Platform.Security.Identity;
using Platform.Security.Mfa;
using Teta.Ippcms.Application.Admin;
using Teta.Ippcms.Application.Common;
using Teta.Ippcms.Application.Reporting;
using Teta.Ippcms.Domain.Suppliers;

namespace Teta.Ippcms.UnitTests;

public class TotpTests
{
    // RFC 6238 Appendix B test secret ("12345678901234567890") with SHA-1, 30 s steps, 8 digits truncated to 6.
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1234567890, "005924")]
    [InlineData(2000000000, "279037")]
    public void Matches_rfc6238_reference_vectors(long unixSeconds, string expected)
    {
        var totp = new TotpService();
        var at = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        Assert.Equal(expected, totp.ComputeCode(RfcSecret, at));
        Assert.True(totp.Verify(RfcSecret, expected, at));
    }

    [Fact]
    public void Allows_one_step_of_drift_only()
    {
        var totp = new TotpService();
        var secret = totp.GenerateSecret();
        var now = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);
        var code = totp.ComputeCode(secret, now);
        Assert.True(totp.Verify(secret, code, now.AddSeconds(30)));
        Assert.False(totp.Verify(secret, code, now.AddSeconds(95)));
        Assert.False(totp.Verify(secret, "000000x", now));
    }

    [Fact]
    public void Base32_round_trips()
    {
        var bytes = Encoding.ASCII.GetBytes("12345678901234567890");
        Assert.Equal(RfcSecret, Base32.Encode(bytes));
        Assert.Equal(bytes, Base32.Decode(RfcSecret));
    }
}

public class FieldProtectionTests
{
    [Fact]
    public void Protect_and_unprotect_round_trip_with_random_nonce()
    {
        var p = new AesGcmFieldProtector("unit-test-key-that-is-long-enough-123456");
        var a = p.Protect("8001015009087");
        var b = p.Protect("8001015009087");
        Assert.NotEqual(a, b);
        Assert.StartsWith("v1:", a);
        Assert.Equal("8001015009087", p.Unprotect(a));
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected()
    {
        var p = new AesGcmFieldProtector("unit-test-key-that-is-long-enough-123456");
        var value = p.Protect("secret");
        var raw = Convert.FromBase64String(value[3..]);
        raw[^1] ^= 0xFF;
        Assert.ThrowsAny<Exception>(() => p.Unprotect("v1:" + Convert.ToBase64String(raw)));
    }

    [Fact]
    public void Keyed_hash_is_deterministic_per_key()
    {
        var p1 = new AesGcmFieldProtector("key-one-key-one-key-one-key-one-1");
        var p2 = new AesGcmFieldProtector("key-two-key-two-key-two-key-two-2");
        Assert.Equal(p1.KeyedHash("8001015009087"), p1.KeyedHash("8001015009087"));
        Assert.NotEqual(p1.KeyedHash("8001015009087"), p2.KeyedHash("8001015009087"));
    }

    [Fact]
    public void Masking_keeps_last_characters()
    {
        Assert.Equal("**********087", Masking.Mask("8001015009087"));
    }
}

public class AuditSealTests
{
    [Fact]
    public void Seal_detects_any_field_change()
    {
        var sealer = new AuditSealer("audit-key-audit-key-audit-key-audit-key");
        var at = new DateTime(2026, 9, 27, 8, 30, 0, DateTimeKind.Utc);
        var seal = sealer.Seal(at, "user", "Project", "Update", "{\"Name\":\"A\"}");
        Assert.True(sealer.Verify(seal, at, "user", "Project", "Update", "{\"Name\":\"A\"}"));
        Assert.False(sealer.Verify(seal, at, "user", "Project", "Update", "{\"Name\":\"B\"}"));
        // Values read back from SQL Server have Kind=Unspecified; they must verify identically.
        Assert.True(sealer.Verify(seal, DateTime.SpecifyKind(at, DateTimeKind.Unspecified), "user", "Project", "Update", "{\"Name\":\"A\"}"));
        Assert.False(new AuditSealer("another-key-another-key-another-key-xx").Verify(seal, at, "user", "Project", "Update", "{\"Name\":\"A\"}"));
    }
}

public class PasswordTests
{
    [Fact]
    public void Hashes_are_compatible_with_aspnet_identity_used_by_ifwems()
    {
        var identityHash = new PasswordHasher<object>().HashPassword(new object(), "Str0ng!Passw0rd");
        var platform = new PlatformPasswordHasher();
        Assert.NotEqual(PasswordCheck.Failed, platform.Verify(identityHash, "Str0ng!Passw0rd"));
        Assert.Equal(PasswordCheck.Failed, platform.Verify(identityHash, "wrong"));

        var platformHash = platform.Hash("Another!Passw0rd");
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<object>().VerifyHashedPassword(new object(), platformHash, "Another!Passw0rd"));
    }

    [Theory]
    [InlineData("short1!A", false)]
    [InlineData("alllowercase1!", false)]
    [InlineData("NoDigits!!Here", false)]
    [InlineData("NoSymbols123Ab", false)]
    [InlineData("Valid!Passw0rd", true)]
    public void Policy_requires_length_and_complexity(string password, bool valid)
    {
        Assert.Equal(valid, PasswordPolicy.Validate(password).Count == 0);
    }
}

public class ReportExportTests
{
    private static ReportTable Sample() => new("RPT-TEST", "Test report", new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc),
        new[] { "Name", "Amount", "Date", "Flag" },
        new List<object?[]>
        {
            new object?[] { "=cmd|' /C calc'!A0", 1234.5m, new DateOnly(2026, 4, 1), true },
            new object?[] { "Comma, \"quoted\"", null, null, false }
        },
        new Dictionary<string, string?> { ["Financial year"] = "2026/27" });

    [Fact]
    public void Csv_escapes_and_neutralises_formula_injection()
    {
        var csv = Encoding.UTF8.GetString(ReportExport.Csv(Sample())).TrimStart('﻿');
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Name,Amount,Date,Flag", lines[0].TrimEnd('\r'));
        Assert.StartsWith("'=cmd", lines[1]);
        Assert.Contains("1234.5,2026-04-01,Yes", lines[1]);
        Assert.StartsWith("\"Comma, \"\"quoted\"\"\"", lines[2]);
    }

    [Fact]
    public void Xlsx_is_a_valid_spreadsheetml_package()
    {
        var bytes = ReportExport.Xlsx(Sample());
        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
        var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
        var doc = System.Xml.Linq.XDocument.Parse(sheet); // well-formed XML
        Assert.Contains("1234.5", sheet);
        Assert.Contains("Comma, &quot;quoted&quot;", sheet.Replace("\"", "&quot;"));
        Assert.NotNull(doc.Root);
    }

    [Fact]
    public void Pdf_has_valid_structure_and_xref()
    {
        var bytes = ReportExport.Pdf(Sample());
        var text = Encoding.ASCII.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);
        var startXref = int.Parse(text[(text.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
        Assert.StartsWith("xref", text[startXref..]);
        Assert.Contains("(Test report)", text);
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(701, "ZZ")]
    public void Column_names(int index, string expected) => Assert.Equal(expected, ReportExport.ColumnName(index));
}

public class CommonTests
{
    [Theory]
    [InlineData(2026, 3, 31, "2025/26", 4)]
    [InlineData(2026, 4, 1, "2026/27", 1)]
    [InlineData(2026, 12, 31, "2026/27", 3)]
    [InlineData(2027, 1, 15, "2026/27", 4)]
    public void Financial_year_runs_april_to_march(int y, int m, int d, string fy, int quarter)
    {
        var date = new DateOnly(y, m, d);
        Assert.Equal(fy, Fy.For(date));
        Assert.Equal(quarter, Fy.QuarterOf(date));
        Assert.True(Fy.IsValid(fy));
        Assert.False(Fy.IsValid("2026/28"));
    }

    [Fact]
    public void Csv_parser_handles_quotes_and_newlines()
    {
        var rows = Csv.Parse("A,B\r\n\"x, y\",\"he said \"\"hi\"\"\"\n\"multi\nline\",2");
        Assert.Equal(3, rows.Count);
        Assert.Equal("x, y", rows[1][0]);
        Assert.Equal("he said \"hi\"", rows[1][1]);
        Assert.Equal("multi\nline", rows[2][0]);
    }

    [Fact]
    public void Supplier_names_normalise_for_duplicate_matching()
    {
        Assert.Equal(Supplier.Normalize("Acme Training (Pty) Ltd"), Supplier.Normalize("ACME TRAINING PTY LTD"));
    }

    [Fact]
    public void Validator_collects_all_errors()
    {
        var ex = Assert.Throws<Platform.Core.ValidationException>(() => new Validator()
            .Required("name", null).Positive("amount", 0).DateOrder("start", new DateOnly(2026, 5, 1), "end", new DateOnly(2026, 4, 1)).ThrowIfInvalid());
        Assert.Equal(3, ex.Errors.Count);
        Assert.True(ex.Errors.ContainsKey("end"));
    }

    [Fact]
    public void Scheduled_report_next_run_is_in_the_future()
    {
        var now = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc); // Sunday
        Assert.Equal(new DateTime(2026, 9, 28, 5, 0, 0), ReportingService.NextRun(now, "Daily"));
        Assert.Equal(new DateTime(2026, 9, 28, 5, 0, 0), ReportingService.NextRun(now, "Weekly"));
        Assert.Equal(new DateTime(2026, 10, 1, 5, 0, 0), ReportingService.NextRun(now, "Monthly"));
        Assert.Equal(new DateTime(2026, 10, 1, 5, 0, 0), ReportingService.NextRun(now, "Quarterly"));
    }
}
