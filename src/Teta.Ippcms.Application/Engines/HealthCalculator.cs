using Teta.Ippcms.Domain.Projects;

namespace Teta.Ippcms.Application.Engines;

public sealed record ProjectMetrics(
    int ScheduleVarianceDays,
    decimal ForecastCostVariancePercent,
    int CriticalOpenRisks,
    int HighOpenRisks,
    int OverdueMilestones,
    int OverdueCriticalMilestones);

/// <summary>Configurable RAG thresholds (FR-EXE-011; SRS §30 notes TETA approves the production values).</summary>
public sealed record HealthThresholds(
    int ScheduleAmberDays = 10,
    int ScheduleRedDays = 30,
    decimal CostAmberPercent = 5m,
    decimal CostRedPercent = 10m,
    int HighRisksAmber = 2,
    int OverdueMilestonesAmber = 2);

public sealed record HealthResult(
    HealthStatus Overall,
    int ScheduleScore,
    int CostScore,
    int RiskScore,
    int DeliveryScore,
    string Explanation);

/// <summary>Project health (RAG) per SRS §30: the worst of schedule, cost, risk and delivery dimensions.</summary>
public static class HealthCalculator
{
    public static HealthResult Calculate(ProjectMetrics m, HealthThresholds t)
    {
        var schedule = m.ScheduleVarianceDays > t.ScheduleRedDays ? 3 : m.ScheduleVarianceDays > t.ScheduleAmberDays ? 2 : 1;
        var cost = m.ForecastCostVariancePercent > t.CostRedPercent ? 3 : m.ForecastCostVariancePercent > t.CostAmberPercent ? 2 : 1;
        var risk = m.CriticalOpenRisks > 0 ? 3 : m.HighOpenRisks > t.HighRisksAmber ? 2 : 1;
        var delivery = m.OverdueCriticalMilestones > 0 ? 3 : m.OverdueMilestones > t.OverdueMilestonesAmber ? 2 : 1;

        var worst = new[] { schedule, cost, risk, delivery }.Max();
        var overall = worst switch
        {
            3 => HealthStatus.Red,
            2 => HealthStatus.Amber,
            _ => HealthStatus.Green
        };

        var reasons = new List<string>
        {
            $"Schedule {Label(schedule)}: {m.ScheduleVarianceDays} day(s) variance (amber > {t.ScheduleAmberDays}, red > {t.ScheduleRedDays})",
            $"Cost {Label(cost)}: forecast variance {m.ForecastCostVariancePercent:0.#}% (amber > {t.CostAmberPercent}%, red > {t.CostRedPercent}%)",
            $"Risk {Label(risk)}: {m.CriticalOpenRisks} critical, {m.HighOpenRisks} high open risk(s)",
            $"Delivery {Label(delivery)}: {m.OverdueMilestones} overdue milestone(s), {m.OverdueCriticalMilestones} critical"
        };
        return new HealthResult(overall, schedule, cost, risk, delivery, string.Join("; ", reasons));
    }

    private static string Label(int score) => score switch { 3 => "RED", 2 => "AMBER", _ => "GREEN" };
}
