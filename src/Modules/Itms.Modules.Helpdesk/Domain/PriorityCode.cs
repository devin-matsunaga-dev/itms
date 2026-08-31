using System.Text.RegularExpressions;

namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// The rule for a priority's stable machine identifier.
/// </summary>
/// <remarks>
/// <para>
/// The code is what everything other than a human reads: DESIGN.md §2 fixes a colour per
/// priority, later rules and integrations key off the same value, and none of them may
/// depend on a display name an administrator is free to edit. So the code is lower-cased
/// on the way in, unique, and — unlike the name — never changes after creation.
/// </para>
/// <para>
/// The shape is deliberately narrow: it appears in URLs, CSS class names, and export
/// columns, and every one of those is happier with <c>account-access</c> than with
/// <c>Account / Access</c>.
/// </para>
/// </remarks>
public static partial class PriorityCode
{
    /// <summary>The longest a code may be.</summary>
    public const int MaxLength = 32;

    /// <summary>A sentence describing the accepted shape, for validation messages.</summary>
    public const string Requirement =
        "A code must start with a letter and contain only lower-case letters, digits, and hyphens.";

    /// <summary>Lower-cases and trims a code, and checks its shape.</summary>
    /// <param name="value">The raw code.</param>
    /// <param name="parameterName">The caller's parameter name, for the exception.</param>
    /// <returns>The normalised code.</returns>
    /// <exception cref="ArgumentException">The code is blank, too long, or malformed.</exception>
    public static string Normalize(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            throw new ArgumentException($"A code may be at most {MaxLength} characters.", parameterName);
        }

        return Shape().IsMatch(normalized)
            ? normalized
            : throw new ArgumentException(Requirement, parameterName);
    }

    /// <summary>
    /// Whether <paramref name="value"/> would survive <see cref="Normalize"/>. The
    /// validator asks this so a malformed code comes back as a 400 with a field message
    /// rather than as an exception from the entity.
    /// </summary>
    /// <param name="value">The raw code, or <see langword="null"/>.</param>
    /// <returns>True when the value is a well-formed code.</returns>
    public static bool IsWellFormed(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Trim().Length <= MaxLength &&
        Shape().IsMatch(value.Trim().ToLowerInvariant());

    [GeneratedRegex("^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
