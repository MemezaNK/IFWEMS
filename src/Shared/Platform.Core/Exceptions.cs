namespace Platform.Core;

/// <summary>Base type for expected, user-correctable failures. Mapped to 4xx problem responses.</summary>
public abstract class PlatformException : Exception
{
    protected PlatformException(string message, string? code = null) : base(message) => Code = code;

    /// <summary>Stable machine-readable code, e.g. a business rule ID such as "BR-006".</summary>
    public string? Code { get; }
}

/// <summary>A business/domain rule was violated (HTTP 422).</summary>
public sealed class DomainException : PlatformException
{
    public DomainException(string message, string? ruleCode = null) : base(message, ruleCode) { }
}

/// <summary>Input failed validation (HTTP 400). Carries per-field errors.</summary>
public sealed class ValidationException : PlatformException
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("Validation failed", "VALIDATION") => Errors = new Dictionary<string, string[]>(errors);

    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = new[] { error } }) { }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>The caller is authenticated but not allowed to act on this record (HTTP 403).</summary>
public sealed class ForbiddenException : PlatformException
{
    public ForbiddenException(string message = "You are not authorised to perform this action.", string? code = null)
        : base(message, code) { }
}

/// <summary>The record does not exist or is outside the caller's data scope (HTTP 404).</summary>
public sealed class NotFoundException : PlatformException
{
    public NotFoundException(string entity, object key) : base($"{entity} '{key}' was not found.", "NOT_FOUND") { }
}

/// <summary>A conflicting/duplicate state or a lost concurrent update (HTTP 409).</summary>
public sealed class ConflictException : PlatformException
{
    public ConflictException(string message, string? code = null) : base(message, code) { }
}
