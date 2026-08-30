namespace Itms.Modules.Directory.Persistence;

/// <summary>
/// Builds the <c>LIKE</c> and <c>ILIKE</c> patterns this module's queries use.
/// </summary>
/// <remarks>
/// An unescaped <c>%</c> or <c>_</c> typed into a filter would otherwise quietly become
/// a wildcard, which at best returns the wrong rows and at worst scans the whole table.
/// </remarks>
internal static class LikePattern
{
    /// <summary>The escape character the patterns use. Pass it as <c>ILike</c>'s third argument.</summary>
    public const string Escape = "\\";

    /// <summary>Wraps <paramref name="term"/> in wildcards, escaping any it contains.</summary>
    /// <param name="term">The raw search term.</param>
    /// <returns>A pattern safe to hand to <c>EF.Functions.ILike</c>.</returns>
    public static string Containing(string term)
    {
        ArgumentNullException.ThrowIfNull(term);
        return $"%{EscapeWildcards(term.Trim())}%";
    }

    /// <summary>
    /// The pattern matching <paramref name="value"/> and anything that extends it —
    /// which is exactly the definition of a subtree, given a materialised id path.
    /// </summary>
    /// <param name="value">The ancestor's path.</param>
    /// <returns>A pattern safe to hand to <c>EF.Functions.Like</c>.</returns>
    /// <remarks>
    /// Written as <c>LIKE 'prefix%'</c> rather than <c>string.StartsWith</c> because the
    /// provider is free to translate <c>StartsWith</c> into a <c>strpos</c> or
    /// <c>left()</c> call, and neither of those can use the <c>varchar_pattern_ops</c>
    /// index on <c>path</c> that the subtree reads and the rename rewrite depend on.
    /// </remarks>
    public static string StartingWith(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return EscapeWildcards(value) + "%";
    }

    private static string EscapeWildcards(string value) =>
        // The backslash goes first: escaping it after the others would double the
        // escapes this method just introduced.
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
