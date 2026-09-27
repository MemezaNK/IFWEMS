using Platform.Core;

namespace Teta.Ippcms.Application.Common;

/// <summary>
/// Server-side input validation (SRS §7.1: mandatory fields are validated client- AND server-side).
/// Collects all errors and throws a single <see cref="ValidationException"/>.
/// </summary>
public sealed class Validator
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    public Validator Required(string field, string? value, int maxLength = 4000)
    {
        if (string.IsNullOrWhiteSpace(value)) Add(field, "This field is required.");
        else if (value.Length > maxLength) Add(field, $"Maximum length is {maxLength} characters.");
        return this;
    }

    public Validator Optional(string field, string? value, int maxLength = 4000)
    {
        if (value is not null && value.Length > maxLength) Add(field, $"Maximum length is {maxLength} characters.");
        return this;
    }

    public Validator Required<T>(string field, T? value) where T : struct
    {
        if (value is null) Add(field, "This field is required.");
        return this;
    }

    public Validator RequiredId(string field, Guid? value)
    {
        if (value is null || value == Guid.Empty) Add(field, "This field is required.");
        return this;
    }

    public Validator NonNegative(string field, decimal? value)
    {
        if (value is < 0) Add(field, "Must be zero or greater.");
        return this;
    }

    public Validator Positive(string field, decimal? value)
    {
        if (value is null or <= 0) Add(field, "Must be greater than zero.");
        return this;
    }

    public Validator Range(string field, decimal? value, decimal min, decimal max)
    {
        if (value is not null && (value < min || value > max)) Add(field, $"Must be between {min} and {max}.");
        return this;
    }

    public Validator DateOrder(string startField, DateOnly? start, string endField, DateOnly? end)
    {
        if (start is not null && end is not null && end < start) Add(endField, $"Must be on or after {startField}.");
        return this;
    }

    public Validator OneOf(string field, string? value, IEnumerable<string> allowed)
    {
        if (value is not null && !allowed.Contains(value)) Add(field, $"'{value}' is not an allowed value.");
        return this;
    }

    public Validator Must(bool condition, string field, string message)
    {
        if (!condition) Add(field, message);
        return this;
    }

    public void ThrowIfInvalid()
    {
        if (_errors.Count > 0) throw new ValidationException(_errors.ToDictionary(k => ToCamel(k.Key), v => v.Value.ToArray()));
    }

    private void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list)) _errors[field] = list = new List<string>();
        list.Add(message);
    }

    private static string ToCamel(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];
}

public static class Fy
{
    /// <summary>South African public-sector financial year label for a date (1 April – 31 March), e.g. "2026/27".</summary>
    public static string For(DateOnly date)
    {
        var start = date.Month >= 4 ? date.Year : date.Year - 1;
        return $"{start}/{(start + 1) % 100:D2}";
    }

    public static bool IsValid(string? fy) =>
        fy is { Length: 7 } && fy[4] == '/' && int.TryParse(fy[..4], out var y) && int.TryParse(fy[5..], out var e) && (y + 1) % 100 == e;

    /// <summary>Quarter (1-4) of the financial year for a date: Q1 = Apr–Jun.</summary>
    public static int QuarterOf(DateOnly date) => ((date.Month + 8) % 12) / 3 + 1;
}
