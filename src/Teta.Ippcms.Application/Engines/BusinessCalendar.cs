namespace Teta.Ippcms.Application.Engines;

/// <summary>
/// Working-day calendar (FR-ADM-007): weekends and configured public holidays are excluded from SLA
/// and deadline calculations.
/// </summary>
public sealed class BusinessCalendar
{
    private readonly HashSet<DateOnly> _holidays;

    public BusinessCalendar(IEnumerable<DateOnly> holidays) => _holidays = holidays.ToHashSet();

    public bool IsWorkingDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !_holidays.Contains(date);

    public DateOnly AddWorkingDays(DateOnly start, int days)
    {
        var date = start;
        var added = 0;
        while (added < days)
        {
            date = date.AddDays(1);
            if (IsWorkingDay(date)) added++;
        }
        return date;
    }

    /// <summary>Adds an SLA expressed in hours, counting each 24 hours as one working day (72h = 3 working days).</summary>
    public DateTime AddSlaHours(DateTime startUtc, int hours)
    {
        var days = (int)Math.Ceiling(hours / 24.0);
        var start = DateOnly.FromDateTime(startUtc);
        var due = AddWorkingDays(start, Math.Max(days, 0));
        return due.ToDateTime(TimeOnly.FromDateTime(startUtc), DateTimeKind.Utc);
    }

    public int WorkingDaysBetween(DateOnly from, DateOnly to)
    {
        if (to <= from) return 0;
        var count = 0;
        for (var d = from.AddDays(1); d <= to; d = d.AddDays(1))
        {
            if (IsWorkingDay(d)) count++;
        }
        return count;
    }
}
