using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Platform.Audit;

/// <summary>Marks a property whose value must never be written to the audit trail (e.g. password hashes, secrets).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveDataAttribute : Attribute { }

/// <summary>Marks an entity type that should not produce audit entries (e.g. the audit table itself, caches).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class NotAuditedAttribute : Attribute { }

/// <summary>A captured change, independent of any application's audit table shape.</summary>
public sealed record CapturedChange(
    string EntityType,
    string? EntityId,
    string Action,
    string? OldValuesJson,
    string? NewValuesJson,
    IReadOnlyList<string> ChangedProperties);

public static class AuditCapture
{
    private const string Redacted = "***";

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "Password", "Secret", "MfaSecret", "SecretEncrypted", "Token", "RefreshToken",
        "ApiKey", "ProtectedIdentifier", "IdentifierEncrypted"
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>
    /// Captures Added/Modified/Deleted entries. For modifications only changed properties are
    /// recorded (old and new values), which keeps the trail readable and reconstructable.
    /// </summary>
    public static IReadOnlyList<CapturedChange> Capture(ChangeTracker changeTracker, Func<Type, bool>? include = null)
    {
        var results = new List<CapturedChange>();
        foreach (var entry in changeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            var type = entry.Entity.GetType();
            if (type.GetCustomAttribute<NotAuditedAttribute>() is not null) continue;
            if (include is not null && !include(type)) continue;

            var id = string.Join(",", entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue?.ToString()));

            switch (entry.State)
            {
                case EntityState.Added:
                    results.Add(new CapturedChange(type.Name, id, "Created", null,
                        Serialize(entry, entry.Properties.Select(p => p.Metadata.Name), current: true),
                        Array.Empty<string>()));
                    break;
                case EntityState.Deleted:
                    results.Add(new CapturedChange(type.Name, id, "Deleted",
                        Serialize(entry, entry.Properties.Select(p => p.Metadata.Name), current: false), null,
                        Array.Empty<string>()));
                    break;
                case EntityState.Modified:
                    var changed = entry.Properties
                        .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                        .Select(p => p.Metadata.Name)
                        .ToList();
                    if (changed.Count == 0) continue;
                    results.Add(new CapturedChange(type.Name, id, "Updated",
                        Serialize(entry, changed, current: false),
                        Serialize(entry, changed, current: true),
                        changed));
                    break;
            }
        }
        return results;
    }

    private static string Serialize(EntityEntry entry, IEnumerable<string> propertyNames, bool current)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var name in propertyNames)
        {
            var prop = entry.Property(name);
            var clrProp = prop.Metadata.PropertyInfo;
            var sensitive = SensitiveNames.Contains(name) || clrProp?.GetCustomAttribute<SensitiveDataAttribute>() is not null;
            var value = current ? prop.CurrentValue : prop.OriginalValue;
            dict[name] = sensitive && value is not null ? Redacted : value;
        }
        return JsonSerializer.Serialize(dict, JsonOptions);
    }
}

/// <summary>
/// Computes and verifies an HMAC-SHA256 seal over an audit record's material fields. A business
/// user (or DBA) who edits an audit row without the server-held key produces a seal mismatch,
/// which the audit-integrity check reports.
/// </summary>
public sealed class AuditSealer
{
    private readonly byte[] _key;

    public AuditSealer(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("An audit sealing key is required (Audit:SealKey).");
        _key = SHA256.HashData(Encoding.UTF8.GetBytes("platform/audit-seal/" + key));
    }

    public string Seal(params object?[] fields)
    {
        var canonical = string.Join("\u001F", fields.Select(f => f switch
        {
            null => string.Empty,
            // Values read back from the database come back with Kind=Unspecified; all audit times are UTC.
            DateTime dt => (dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime())
                .ToString("yyyy-MM-ddTHH:mm:ss.fffffff"),
            _ => Convert.ToString(f, System.Globalization.CultureInfo.InvariantCulture)
        }));
        return Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(canonical)));
    }

    public bool Verify(string? seal, params object?[] fields) =>
        seal is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(seal), Encoding.ASCII.GetBytes(Seal(fields)));
}
