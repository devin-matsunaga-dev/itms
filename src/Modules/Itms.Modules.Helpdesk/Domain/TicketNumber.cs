using System.Globalization;

namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// The shape of the human-readable ticket number — <c>TKT-0001</c> — and the only place
/// that shape is spelled out.
/// </summary>
/// <remarks>
/// <para>
/// One prefix for every V1 ticket, one monotonic counter, and no yearly reset. V1 has a
/// single class of ticket, so a discriminator would be a column nothing reads; if the
/// product ever introduces a second class, the numbers already issued stay exactly as
/// they are and the new class is modelled separately.
/// </para>
/// <para>
/// Padded to <see cref="MinimumDigits"/> so early numbers align with later ones, and
/// allowed to grow past it on its own — the ten-thousandth ticket is <c>TKT-10000</c>,
/// not an error.
/// </para>
/// <para>
/// The counter value comes from <c>TicketNumberGenerator</c>, which owns the concurrency
/// story. This type only renders and recognises.
/// </para>
/// </remarks>
public static class TicketNumber
{
    /// <summary>What every ticket number starts with.</summary>
    public const string Prefix = "TKT-";

    /// <summary>The narrowest a number is rendered. Longer values are not truncated.</summary>
    public const int MinimumDigits = 4;

    /// <summary>
    /// The longest a number may be, and the width of the column that stores it. Wide
    /// enough for a counter no helpdesk will reach, narrow enough to index cheaply.
    /// </summary>
    public const int MaxLength = 24;

    /// <summary>The first number a fresh installation issues.</summary>
    public const long FirstValue = 1;

    /// <summary>
    /// <see cref="FirstValue"/> as SQL text, so <c>TicketNumberGenerator</c>'s statement
    /// can stay a compile-time constant and still name the same starting point this type
    /// does. Kept beside it so the two cannot drift.
    /// </summary>
    internal const string FirstValueSql = "1";

    /// <summary>Renders a counter value as a ticket number.</summary>
    /// <param name="value">The counter value, from <c>TicketNumberGenerator</c>.</param>
    /// <returns>The number, such as <c>TKT-0042</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is below <see cref="FirstValue"/>.</exception>
    public static string Format(long value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, FirstValue);

        return Prefix + value.ToString(CultureInfo.InvariantCulture).PadLeft(MinimumDigits, '0');
    }

    /// <summary>Whether <paramref name="value"/> is a well-formed ticket number.</summary>
    /// <remarks>
    /// Written against <see cref="Prefix"/> rather than as a pattern with the literal in
    /// it, so the prefix is stated once and a change to it cannot leave a recogniser
    /// behind that still accepts the old one.
    /// </remarks>
    /// <param name="value">The candidate, or <see langword="null"/>.</param>
    /// <returns>True when it carries the prefix, at least one digit, and nothing else.</returns>
    public static bool IsWellFormed(string? value) =>
        value is not null &&
        value.Length > Prefix.Length &&
        value.Length <= MaxLength &&
        value.StartsWith(Prefix, StringComparison.Ordinal) &&
        !value.AsSpan(Prefix.Length).ContainsAnyExceptInRange('0', '9');
}
