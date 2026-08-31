namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// The trimming and length rules both reference-data entities apply to the text they
/// are handed.
/// </summary>
/// <remarks>
/// These throw rather than return a failure. The endpoint validators reject over-long
/// input as a 400 with per-field messages long before a handler runs, so reaching one of
/// these means a caller inside the module built an entity from unvalidated text — a
/// programming error, which CONVENTIONS.md says is what exceptions are for.
/// </remarks>
internal static class ReferenceText
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
