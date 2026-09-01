namespace Itms.Platform.Data;

/// <summary>
/// Builds the <c>LIKE</c> and <c>ILIKE</c> patterns a module's queries use, escaping the
/// wildcards a person may have typed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is in the shared kernel.</b> An unescaped <c>%</c> or <c>_</c> in a search
/// box quietly becomes a wildcard: at best it returns rows nobody asked for, at worst it
/// turns a filter into a scan of the whole table. Every module with a picker or a filter
/// needs the same three replacements in the same order, and getting them wrong is silent
/// — the query still runs and still returns something.
/// </para>
/// <para>
/// It arrived here at WP-1.12, on the trigger WP-0.6 set when it wrote the second copy:
/// Directory's <c>LikePattern</c> and the escaping inlined in Identity's
/// <c>UserLookupService</c> were the first two, and the ticket queue's search would have
/// been the third. Both are now pointed here. This is shared-kernel material by
/// <c>ARCHITECTURE.md</c> §3 rule 4's definition — a genuinely shared primitive that
/// references no module.
/// </para>
/// </remarks>
public static class SearchPattern
{
    /// <summary>
    /// The escape character the patterns use. Pass it as the third argument to
    /// <c>EF.Functions.Like</c> or <c>EF.Functions.ILike</c>.
    /// </summary>
    /// <remarks>
    /// PostgreSQL defaults to a backslash already, but stating it means the pattern and the
    /// call cannot drift apart if a caller copies one without the other.
    /// </remarks>
    public const string Escape = "\\";

    /// <summary>Wraps <paramref name="term"/> in wildcards, escaping any it contains.</summary>
    /// <param name="term">The raw search term, as typed.</param>
    /// <returns>A pattern safe to hand to <c>EF.Functions.ILike</c>.</returns>
    public static string Containing(string term)
    {
        ArgumentNullException.ThrowIfNull(term);
        return $"%{EscapeWildcards(term.Trim())}%";
    }

    /// <summary>
    /// The pattern matching <paramref name="value"/> and anything that extends it — which
    /// is exactly the definition of a subtree, given a materialised id path.
    /// </summary>
    /// <param name="value">The prefix to match from.</param>
    /// <returns>A pattern safe to hand to <c>EF.Functions.Like</c>.</returns>
    /// <remarks>
    /// Written as <c>LIKE 'prefix%'</c> rather than <c>string.StartsWith</c> because the
    /// provider is free to translate <c>StartsWith</c> into a <c>strpos</c> or
    /// <c>left()</c> call, and neither of those can use a <c>varchar_pattern_ops</c> index
    /// — which is what Directory's subtree reads and rename rewrites depend on.
    /// </remarks>
    public static string StartingWith(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return EscapeWildcards(value) + "%";
    }

    private static string EscapeWildcards(string value) =>
        // The backslash goes first: escaping it after the others would double the escapes
        // this method has just introduced.
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
