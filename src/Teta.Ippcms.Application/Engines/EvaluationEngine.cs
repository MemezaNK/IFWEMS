using Platform.Core;

namespace Teta.Ippcms.Application.Engines;

public sealed record TechnicalCriterion(Guid Id, string Name, decimal Weight, decimal MaxScore);

public sealed record BidPriceInput(Guid BidId, decimal Price, decimal PreferencePointsClaimed, bool Eligible);

public sealed record BidPriceResult(Guid BidId, decimal Price, decimal PricePoints, decimal PreferencePoints, decimal TotalPoints, int Rank);

/// <summary>
/// Procurement evaluation calculations (SRS §28, FR-SCM-007/008/009). All results are reproducible
/// from stored inputs (criteria, individual scores, price system configuration), which the audit
/// requirement for FR-SCM-009 relies on.
/// </summary>
public static class EvaluationEngine
{
    /// <summary>
    /// Weighted functionality score out of 100 for one evaluator's (or the consolidated average)
    /// criterion scores. Weights must total 100 and each score must be within 0..MaxScore.
    /// </summary>
    public static decimal CalculateTechnicalScore(
        IReadOnlyCollection<TechnicalCriterion> criteria,
        IReadOnlyDictionary<Guid, decimal> scores)
    {
        if (criteria.Count == 0) throw new DomainException("No technical criteria are configured.", "FR-SCM-008");
        if (criteria.Sum(x => x.Weight) != 100m) throw new DomainException("Technical weights must total 100.", "FR-SCM-008");

        decimal total = 0m;
        foreach (var criterion in criteria)
        {
            if (criterion.MaxScore <= 0) throw new DomainException($"Criterion '{criterion.Name}' has no maximum score.", "FR-SCM-008");
            if (!scores.TryGetValue(criterion.Id, out var score))
                throw new DomainException($"No score captured for criterion '{criterion.Name}'.", "FR-SCM-008");
            if (score < 0 || score > criterion.MaxScore)
                throw new DomainException($"Score for '{criterion.Name}' is outside the permitted range 0–{criterion.MaxScore}.", "FR-SCM-008");

            total += score / criterion.MaxScore * criterion.Weight;
        }
        return Math.Round(total, 2);
    }

    /// <summary>
    /// Consolidates individual evaluators' scores: each criterion's score is the mean across
    /// evaluators, then the weighted score is calculated on the means.
    /// </summary>
    public static decimal ConsolidateTechnicalScore(
        IReadOnlyCollection<TechnicalCriterion> criteria,
        IReadOnlyCollection<IReadOnlyDictionary<Guid, decimal>> evaluatorScores)
    {
        if (evaluatorScores.Count == 0) throw new DomainException("No evaluator scores have been captured.", "FR-SCM-008");
        var means = criteria.ToDictionary(
            c => c.Id,
            c =>
            {
                var values = evaluatorScores.Where(s => s.ContainsKey(c.Id)).Select(s => s[c.Id]).ToList();
                if (values.Count == 0) throw new DomainException($"No scores captured for criterion '{c.Name}'.", "FR-SCM-008");
                return values.Average();
            });
        return CalculateTechnicalScore(criteria, means);
    }

    /// <summary>
    /// Price points using the configured maximum price points: Ps = Pmax × (1 − (Pt − Pmin) / Pmin),
    /// never below zero, where Pmin is the lowest acceptable eligible price. Preference points are
    /// capped at the configured maximum. Bids are ranked on total points (ties broken by price).
    /// </summary>
    public static IReadOnlyList<BidPriceResult> CalculatePricePreference(
        IReadOnlyCollection<BidPriceInput> bids,
        decimal maxPricePoints,
        decimal maxPreferencePoints)
    {
        if (maxPricePoints <= 0) throw new DomainException("The price/preference system has no price points configured.", "FR-SCM-009");
        var eligible = bids.Where(b => b.Eligible).ToList();
        if (eligible.Count == 0) return Array.Empty<BidPriceResult>();
        if (eligible.Any(b => b.Price <= 0)) throw new DomainException("Bid prices must be positive.", "FR-SCM-009");

        var lowest = eligible.Min(b => b.Price);
        var scored = eligible.Select(b =>
        {
            var pricePoints = Math.Max(0m, maxPricePoints * (1m - (b.Price - lowest) / lowest));
            var preference = Math.Clamp(b.PreferencePointsClaimed, 0m, maxPreferencePoints);
            pricePoints = Math.Round(pricePoints, 2);
            return (b.BidId, b.Price, PricePoints: pricePoints, Preference: preference, Total: Math.Round(pricePoints + preference, 2));
        })
        .OrderByDescending(x => x.Total)
        .ThenBy(x => x.Price)
        .ToList();

        return scored.Select((x, i) => new BidPriceResult(x.BidId, x.Price, x.PricePoints, x.Preference, x.Total, i + 1)).ToList();
    }

    /// <summary>A bid passes compliance only if every mandatory item passed for every evaluator who assessed it.</summary>
    public static bool IsCompliant(IEnumerable<(bool Mandatory, bool? Passed)> results) =>
        results.Where(r => r.Mandatory).All(r => r.Passed == true);
}
