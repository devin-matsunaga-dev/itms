namespace Itms.Modules.Assets.Domain;

/// <summary>
/// The rule for an asset's tag — the identifier stuck on the physical object.
/// </summary>
/// <remarks>
/// <para>
/// ARCHITECTURE.md §11 invariant 4 makes the tag unique and <em>immutable once created</em>.
/// <see cref="Asset"/> enforces the immutability by exposing no method that moves it; this
/// type enforces the shape, and the unique index enforces the uniqueness.
/// </para>
/// <para>
/// Normalisation is upper-casing and trimming, and the uniqueness is asserted on the
/// normalised form so <c>lap-0042</c> and <c>LAP-0042</c> cannot both exist. The displayed
/// value keeps whatever case the operator typed, because an asset tag is copied off a
/// physical label and reading it back in a different case invites a second look.
/// </para>
/// <para>
/// The shape is deliberately permissive about <em>what</em> the tag says — every
/// organisation numbers its estate differently, and refusing somebody's existing scheme
/// would make the first CSV import (WP-5.7) impossible. It refuses only whitespace inside
/// the tag, which is what turns one tag into two when it is scanned, pasted, or put in a
/// URL.
/// </para>
/// </remarks>
public static class AssetTagRules
{
    /// <summary>The longest an asset tag may be.</summary>
    public const int MaxLength = 64;

    /// <summary>A sentence describing the accepted shape, for validation messages.</summary>
    public const string Requirement = "An asset tag cannot contain spaces.";

    /// <summary>Trims an asset tag and checks its shape.</summary>
    /// <param name="value">The raw tag.</param>
    /// <param name="parameterName">The caller's parameter name, for the exception.</param>
    /// <returns>The trimmed tag, in the case it was given in.</returns>
    /// <exception cref="ArgumentException">The tag is blank, too long, or contains whitespace.</exception>
    public static string Clean(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"An asset tag may be at most {MaxLength} characters.", parameterName);
        }

        return trimmed.Any(char.IsWhiteSpace)
            ? throw new ArgumentException(Requirement, parameterName)
            : trimmed;
    }

    /// <summary>The form uniqueness is enforced on.</summary>
    /// <param name="value">A tag that has already been through <see cref="Clean"/>.</param>
    /// <returns>The tag, upper-cased.</returns>
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.ToUpperInvariant();
    }

    /// <summary>
    /// Whether <paramref name="value"/> would survive <see cref="Clean"/>. The validator
    /// asks this so a malformed tag comes back as a 400 with a field message rather than
    /// as an exception from the entity.
    /// </summary>
    /// <param name="value">The raw tag, or <see langword="null"/>.</param>
    /// <returns>True when the value is a well-formed asset tag.</returns>
    public static bool IsWellFormed(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Trim().Length <= MaxLength &&
        !value.Trim().Any(char.IsWhiteSpace);
}
