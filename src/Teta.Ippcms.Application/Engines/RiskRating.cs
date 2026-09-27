using Platform.Core;

namespace Teta.Ippcms.Application.Engines;

public sealed record RatingBand(string Name, int MinScore, int MaxScore, string Colour);

/// <summary>Consistent risk rating from a configurable 5×5 likelihood/impact matrix (FR-RSK-002).</summary>
public static class RiskRating
{
    public const int ScaleMax = 5;

    public static readonly IReadOnlyList<RatingBand> DefaultBands = new[]
    {
        new RatingBand("Low", 1, 4, "#0ca30c"),
        new RatingBand("Medium", 5, 9, "#fab219"),
        new RatingBand("High", 10, 16, "#ec835a"),
        new RatingBand("Critical", 17, 25, "#d03b3b")
    };

    public static (int Score, string Rating) Rate(int likelihood, int impact, IReadOnlyList<RatingBand> bands)
    {
        if (likelihood is < 1 or > ScaleMax || impact is < 1 or > ScaleMax)
            throw new DomainException($"Likelihood and impact must be between 1 and {ScaleMax}.", "FR-RSK-002");
        var score = likelihood * impact;
        var band = bands.FirstOrDefault(b => score >= b.MinScore && score <= b.MaxScore)
                   ?? throw new DomainException($"No rating band covers score {score}. Check the risk matrix configuration.", "FR-RSK-002");
        return (score, band.Name);
    }

    /// <summary>Validates that bands cover 1..25 without gaps or overlaps.</summary>
    public static void ValidateBands(IReadOnlyList<RatingBand> bands)
    {
        var ordered = bands.OrderBy(b => b.MinScore).ToList();
        var expected = 1;
        foreach (var band in ordered)
        {
            if (band.MinScore != expected || band.MaxScore < band.MinScore)
                throw new DomainException("Risk rating bands must cover scores 1–25 contiguously without overlaps.", "FR-RSK-002");
            expected = band.MaxScore + 1;
        }
        if (expected != ScaleMax * ScaleMax + 1)
            throw new DomainException("Risk rating bands must cover scores 1–25 contiguously without overlaps.", "FR-RSK-002");
    }

    public static bool IsHighOrCritical(string rating) => rating is "High" or "Critical";
}
