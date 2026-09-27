namespace Teta.Ippcms.Application.Engines;

/// <summary>Duplicate detection helpers for supplier master data (FR-SCM-018) and beneficiaries (FR-ME-008).</summary>
public static class DuplicateDetection
{
    /// <summary>Normalised similarity 0..1 based on Levenshtein distance.</summary>
    public static double Similarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1;
        var distance = Levenshtein(a, b);
        return 1.0 - (double)distance / Math.Max(a.Length, b.Length);
    }

    public static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    /// <summary>Configured threshold above which two normalised supplier names are treated as potential duplicates.</summary>
    public const double SupplierNameThreshold = 0.88;
}
