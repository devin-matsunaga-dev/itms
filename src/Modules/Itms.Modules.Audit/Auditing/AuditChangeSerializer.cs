using System.Text.Json;
using System.Text.Json.Serialization;
using Itms.Contracts.Auditing;

namespace Itms.Modules.Audit.Auditing;

/// <summary>
/// Turns a field diff into the JSON stored in <c>audit_entries.changes</c>.
/// </summary>
/// <remarks>
/// Most of what passes through here is text somebody typed — a department description,
/// a submitted user name, a resolution summary — so it is bounded on the way in. A
/// caller cannot make one audit row large enough to make the trail unreadable, and it
/// cannot smuggle a field in under a name so long that the viewer cannot render it.
/// </remarks>
public static class AuditChangeSerializer
{
    /// <summary>The longest a single before or after value may be.</summary>
    public const int ValueMaxLength = 2000;

    /// <summary>The longest a field name may be.</summary>
    public const int FieldNameMaxLength = 128;

    /// <summary>The most fields one entry may record.</summary>
    public const int MaxFields = 100;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        // The value is stored, not displayed as HTML. Escaping is the reader's job, and
        // over-escaping here would make the stored diff differ from what was written.
        WriteIndented = false,
    };

    /// <summary>Serialises <paramref name="changes"/> to a JSON object.</summary>
    /// <param name="changes">The changed fields, or <see langword="null"/>.</param>
    /// <returns>
    /// The JSON object, or <see langword="null"/> when there is nothing to record — an
    /// action that changed no field stores SQL <c>NULL</c> rather than <c>{}</c>, so
    /// "no diff" and "an empty diff" cannot be confused.
    /// </returns>
    public static string? Serialize(IReadOnlyDictionary<string, AuditFieldChange>? changes)
    {
        if (changes is null || changes.Count == 0)
        {
            return null;
        }

        var bounded = new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal);

        foreach (var (field, change) in changes.Take(MaxFields))
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            bounded[Cap(field, FieldNameMaxLength)!] = new AuditFieldChange(
                Cap(change?.Before, ValueMaxLength),
                Cap(change?.After, ValueMaxLength));
        }

        return bounded.Count == 0 ? null : JsonSerializer.Serialize(bounded, Options);
    }

    /// <summary>Reads a stored diff back, for the viewer and for tests.</summary>
    /// <param name="json">The column value, or <see langword="null"/>.</param>
    /// <returns>The changed fields, empty when the column was null.</returns>
    public static IReadOnlyDictionary<string, AuditFieldChange> Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, AuditFieldChange>>(json, Options)
              ?? new Dictionary<string, AuditFieldChange>(StringComparer.Ordinal);

    private static string? Cap(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
