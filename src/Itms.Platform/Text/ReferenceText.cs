namespace Itms.Platform.Text;

/// <summary>
/// The trimming and length rules an entity applies to the text it is handed.
/// </summary>
/// <remarks>
/// <para>
/// These throw rather than return a failure. The endpoint validators reject over-long
/// input as a 400 with per-field messages long before a handler runs, so reaching one of
/// these means a caller inside a module built an entity from unvalidated text — a
/// programming error, which CONVENTIONS.md says is what exceptions are for.
/// </para>
/// <para>
/// <b>This is the hoist Helpdesk's and Assets' copies asked for.</b> Both modules carry a
/// private <c>ReferenceText</c> with these two methods, and both say the same thing: a
/// module may not reference another module, so the duplication is forced until the
/// <em>third</em> copy, which is when it moves here rather than being written again. WP-3.1
/// is that third copy. The two existing copies are deliberately left in place — repointing
/// them means editing two merged packages' entities from a monitoring package, which is the
/// call this repository made when <c>AuthClient.SignedInAsync</c> was hoisted at WP-1.4. A
/// package already touching those files should point them here and delete them; the count
/// is capped rather than growing.
/// </para>
/// </remarks>
public static class ReferenceText
{
    /// <summary>Trims a required name and bounds its length.</summary>
    /// <param name="value">The raw value.</param>
    /// <param name="maxLength">The longest it may be once trimmed.</param>
    /// <param name="parameterName">The caller's parameter name, for the exception.</param>
    /// <returns>The trimmed name.</returns>
    public static string Name(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var trimmed = value.Trim();

        return trimmed.Length <= maxLength
            ? trimmed
            : throw new ArgumentException($"The value may be at most {maxLength} characters.", parameterName);
    }

    /// <summary>Trims optional text, turning blank into <see langword="null"/>, and bounds its length.</summary>
    /// <param name="value">The raw value, or <see langword="null"/>.</param>
    /// <param name="maxLength">The longest it may be once trimmed.</param>
    /// <param name="parameterName">The caller's parameter name, for the exception.</param>
    /// <returns>The trimmed text, or <see langword="null"/>.</returns>
    public static string? Optional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength
            ? trimmed
            : throw new ArgumentException($"The value may be at most {maxLength} characters.", parameterName);
    }
}
