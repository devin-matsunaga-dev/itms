using System.Text.RegularExpressions;

namespace Itms.Modules.Assets.Domain;

/// <summary>
/// The rule for an asset status's stable machine identifier, and the codes SPEC.md §3
/// names.
/// </summary>
/// <remarks>
/// <para>
/// The code is what everything other than a person reads. WP-2.2's lifecycle methods have
/// to be able to say "this asset is now in repair" without depending on a display name an
/// administrator is free to edit, and DESIGN.md's semantic colours are keyed the same way
/// — which is exactly the argument WP-1.1 made for giving a ticket priority a code and
/// withholding one from a category.
/// </para>
/// <para>
/// So the code is lower-cased on the way in, unique, and — unlike the name — never changes
/// after creation.
/// </para>
/// </remarks>
public static partial class AssetStatusCode
{
    /// <summary>The longest a code may be.</summary>
    public const int MaxLength = 32;

    /// <summary>A sentence describing the accepted shape, for validation messages.</summary>
    public const string Requirement =
        "A code must start with a letter and contain only lower-case letters, digits, and hyphens.";

    /// <summary>Not yet issued to anybody. The status an asset is created in by default.</summary>
    public const string InStock = "in-stock";

    /// <summary>Issued and in service.</summary>
    public const string Deployed = "deployed";

    /// <summary>Away being fixed.</summary>
    public const string Repair = "repair";

    /// <summary>Taken out of service and kept on the books.</summary>
    public const string Retired = "retired";

    /// <summary>Unaccounted for.</summary>
    public const string Lost = "lost";

    /// <summary>Physically gone — scrapped, sold, or destroyed.</summary>
    public const string Disposed = "disposed";

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
