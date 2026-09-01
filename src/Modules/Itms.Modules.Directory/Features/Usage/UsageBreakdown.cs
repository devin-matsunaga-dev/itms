namespace Itms.Modules.Directory.Features.Usage;

/// <summary>
/// Renders a set of reference counts as the sentence fragment a refusal ends with —
/// "3 assets and 1 user".
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of what an administrator sees when a delete is refused, so it has to
/// drop the modules reporting zero (a message reading "0 tickets" alongside the real
/// count sends them looking in the wrong module) and it has to say "1 user" rather than
/// "1 users".
/// </para>
/// <para>
/// <b>Public, following WP-2.3's call for <c>WarrantyWindow</c>.</b> This repository has
/// no <c>InternalsVisibleTo</c>, the string is a user-facing message rather than an
/// internal detail, and no module can reference Directory in any case.
/// </para>
/// </remarks>
public static class UsageBreakdown
{
    /// <summary>Renders <paramref name="references"/> as a readable fragment.</summary>
    /// <param name="references">The per-module counts. Entries counting zero are dropped.</param>
    /// <returns>The fragment, or an empty string when nothing references the entry.</returns>
    public static string Describe(IReadOnlyList<UsageCountResponse> references)
    {
        ArgumentNullException.ThrowIfNull(references);

        var parts = references
            .Where(reference => reference.Count > 0)
            .Select(reference => $"{reference.Count} {Quantify(reference.EntityName, reference.Count)}")
            .ToArray();

        return parts.Length switch
        {
            0 => string.Empty,
            1 => parts[0],
            _ => string.Join(", ", parts[..^1]) + " and " + parts[^1],
        };
    }

    /// <summary>
    /// The singular form when there is exactly one of something.
    /// </summary>
    /// <remarks>
    /// Trimming a trailing "s" is right for the three names in use — assets, tickets,
    /// users — and <c>DirectoryUsage.EntityName</c> is documented as lower-case plural for
    /// exactly that reason. A counter reporting an irregular plural is the point at which
    /// this stops being a two-line function, and it should carry both forms rather than
    /// have a pluralisation library owned here for three words.
    /// </remarks>
    private static string Quantify(string entityName, int count) =>
        count == 1 && entityName.EndsWith('s') ? entityName[..^1] : entityName;
}
