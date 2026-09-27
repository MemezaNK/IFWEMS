using Platform.Core;
using Teta.Ippcms.Application.Engines;
using Teta.Ippcms.Domain.Projects;

namespace Teta.Ippcms.UnitTests;

public class EvaluationEngineTests
{
    private static readonly TechnicalCriterion A = new(Guid.NewGuid(), "Methodology", 60, 5);
    private static readonly TechnicalCriterion B = new(Guid.NewGuid(), "Experience", 40, 5);

    [Fact]
    public void Technical_score_is_weighted_out_of_100()
    {
        var score = EvaluationEngine.CalculateTechnicalScore(new[] { A, B }, new Dictionary<Guid, decimal> { [A.Id] = 4, [B.Id] = 5 });
        Assert.Equal(88m, score); // 4/5*60 + 5/5*40
    }

    [Fact]
    public void Weights_must_total_100()
    {
        var bad = new[] { A with { Weight = 50 }, B };
        var ex = Assert.Throws<DomainException>(() => EvaluationEngine.CalculateTechnicalScore(bad, new Dictionary<Guid, decimal> { [A.Id] = 1, [B.Id] = 1 }));
        Assert.Equal("FR-SCM-008", ex.Code);
    }

    [Fact]
    public void Score_outside_range_is_rejected()
    {
        Assert.Throws<DomainException>(() => EvaluationEngine.CalculateTechnicalScore(new[] { A, B }, new Dictionary<Guid, decimal> { [A.Id] = 6, [B.Id] = 1 }));
    }

    [Fact]
    public void Consolidation_uses_mean_of_evaluators()
    {
        var e1 = new Dictionary<Guid, decimal> { [A.Id] = 5, [B.Id] = 3 };
        var e2 = new Dictionary<Guid, decimal> { [A.Id] = 3, [B.Id] = 5 };
        Assert.Equal(80m, EvaluationEngine.ConsolidateTechnicalScore(new[] { A, B }, new[] { e1, e2 })); // means 4,4 -> 48+32
    }

    [Fact]
    public void Price_points_follow_80_20_formula_and_rank_bids()
    {
        var b1 = Guid.NewGuid();
        var b2 = Guid.NewGuid();
        var b3 = Guid.NewGuid();
        var result = EvaluationEngine.CalculatePricePreference(new[]
        {
            new BidPriceInput(b1, 100_000m, 10m, true),
            new BidPriceInput(b2, 110_000m, 20m, true),
            new BidPriceInput(b3, 90_000m, 0m, false)
        }, 80m, 20m);

        Assert.Equal(2, result.Count); // ineligible bid excluded
        var first = result.Single(r => r.BidId == b1);
        var second = result.Single(r => r.BidId == b2);
        Assert.Equal(80m, first.PricePoints);
        Assert.Equal(72m, second.PricePoints); // 80 * (1 - 10000/100000)
        Assert.Equal(92m, second.TotalPoints);
        Assert.Equal(1, second.Rank); // preference points change the ranking
        Assert.Equal(2, first.Rank);
    }

    [Fact]
    public void Preference_points_are_capped_and_price_points_never_negative()
    {
        var cheap = Guid.NewGuid();
        var expensive = Guid.NewGuid();
        var result = EvaluationEngine.CalculatePricePreference(new[]
        {
            new BidPriceInput(cheap, 100m, 50m, true),
            new BidPriceInput(expensive, 300m, 0m, true)
        }, 90m, 10m);
        Assert.Equal(10m, result.Single(r => r.BidId == cheap).PreferencePoints);
        Assert.Equal(0m, result.Single(r => r.BidId == expensive).PricePoints);
    }

    [Fact]
    public void Compliance_requires_all_mandatory_items()
    {
        Assert.True(EvaluationEngine.IsCompliant(new (bool, bool?)[] { (true, true), (false, false) }));
        Assert.False(EvaluationEngine.IsCompliant(new (bool, bool?)[] { (true, true), (true, null) }));
    }
}

public class HealthCalculatorTests
{
    private static readonly HealthThresholds T = new();

    [Fact]
    public void All_within_thresholds_is_green()
    {
        var r = HealthCalculator.Calculate(new ProjectMetrics(5, 2m, 0, 1, 1, 0), T);
        Assert.Equal(HealthStatus.Green, r.Overall);
    }

    [Theory]
    [InlineData(15, 0, 0, 0, 0, 0, HealthStatus.Amber)]
    [InlineData(31, 0, 0, 0, 0, 0, HealthStatus.Red)]
    [InlineData(0, 6, 0, 0, 0, 0, HealthStatus.Amber)]
    [InlineData(0, 11, 0, 0, 0, 0, HealthStatus.Red)]
    [InlineData(0, 0, 1, 0, 0, 0, HealthStatus.Red)]
    [InlineData(0, 0, 0, 3, 0, 0, HealthStatus.Amber)]
    [InlineData(0, 0, 0, 0, 3, 0, HealthStatus.Amber)]
    [InlineData(0, 0, 0, 0, 0, 1, HealthStatus.Red)]
    public void Worst_dimension_drives_overall_rag(int scheduleDays, int costPct, int critical, int high, int overdue, int overdueCritical, HealthStatus expected)
    {
        var r = HealthCalculator.Calculate(new ProjectMetrics(scheduleDays, costPct, critical, high, overdue, overdueCritical), T);
        Assert.Equal(expected, r.Overall);
        Assert.False(string.IsNullOrWhiteSpace(r.Explanation));
    }
}

public class RiskRatingTests
{
    [Theory]
    [InlineData(1, 1, 1, "Low")]
    [InlineData(2, 3, 6, "Medium")]
    [InlineData(4, 4, 16, "High")]
    [InlineData(5, 4, 20, "Critical")]
    public void Rates_likelihood_times_impact(int l, int i, int score, string rating)
    {
        var (s, r) = RiskRating.Rate(l, i, RiskRating.DefaultBands);
        Assert.Equal(score, s);
        Assert.Equal(rating, r);
    }

    [Fact]
    public void Out_of_range_inputs_are_rejected()
    {
        Assert.ThrowsAny<Exception>(() => RiskRating.Rate(0, 3, RiskRating.DefaultBands));
        Assert.ThrowsAny<Exception>(() => RiskRating.Rate(6, 3, RiskRating.DefaultBands));
    }

    [Fact]
    public void Bands_must_cover_1_to_25_without_gaps()
    {
        RiskRating.ValidateBands(RiskRating.DefaultBands);
        var gap = new[] { new RatingBand("Low", 1, 4, "#0f0"), new RatingBand("High", 6, 25, "#f00") };
        Assert.ThrowsAny<Exception>(() => RiskRating.ValidateBands(gap));
    }
}

public class ScheduleValidatorTests
{
    [Fact]
    public void Detects_dependency_cycles()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        Assert.Null(ScheduleValidator.FindCycle(new[] { (a, b), (b, c) }));
        var cycle = ScheduleValidator.FindCycle(new[] { (a, b), (b, c), (c, a) });
        Assert.NotNull(cycle);
        Assert.Contains(a, cycle!);
    }

    [Fact]
    public void Roll_up_is_weighted()
    {
        Assert.Equal(75m, ScheduleValidator.RollUp(new[] { (100m, 1m), (50m, 1m) }));
        Assert.Equal(80m, ScheduleValidator.RollUp(new[] { (100m, 3m), (20m, 1m) }));
        Assert.Equal(0m, ScheduleValidator.RollUp(Array.Empty<(decimal, decimal)>()));
    }
}

public class BusinessCalendarTests
{
    private static readonly BusinessCalendar Calendar = new(new[] { new DateOnly(2026, 12, 25), new DateOnly(2026, 12, 16) });

    [Fact]
    public void Skips_weekends_and_holidays()
    {
        // Fri 11 Dec 2026 + 3 working days: Mon 14, Tue 15, (Wed 16 holiday) Thu 17
        Assert.Equal(new DateOnly(2026, 12, 17), Calendar.AddWorkingDays(new DateOnly(2026, 12, 11), 3));
        Assert.False(Calendar.IsWorkingDay(new DateOnly(2026, 12, 25)));
        Assert.False(Calendar.IsWorkingDay(new DateOnly(2026, 12, 12)));
    }

    [Fact]
    public void Sla_hours_count_working_days()
    {
        var due = Calendar.AddSlaHours(new DateTime(2026, 12, 11, 9, 0, 0, DateTimeKind.Utc), 72);
        Assert.Equal(new DateTime(2026, 12, 17, 9, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public void Working_days_between()
    {
        Assert.Equal(3, Calendar.WorkingDaysBetween(new DateOnly(2026, 12, 11), new DateOnly(2026, 12, 17)));
    }
}

public class DuplicateDetectionTests
{
    [Fact]
    public void Similar_names_score_high()
    {
        Assert.Equal(1, DuplicateDetection.Levenshtein("kitten", "sitten"));
        Assert.True(DuplicateDetection.Similarity("ACME TRAINING", "ACME TRAINNG") > 0.88);
        Assert.True(DuplicateDetection.Similarity("ACME TRAINING", "ZULU LOGISTICS") < 0.5);
    }
}
