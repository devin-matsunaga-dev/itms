using System.Net;

namespace Itms.Modules.Monitoring.Features.Devices;

/// <summary>
/// Reads the address a request carries as text into the <see cref="IPAddress"/> the entity
/// and the <c>inet</c> column hold.
/// </summary>
/// <remarks>
/// <para>
/// Written once because both write shapes need it and because the parse has one wrinkle
/// worth stating in one place: <see cref="IPAddress.TryParse(string, out IPAddress)"/>
/// accepts a bare integer such as <c>"1"</c> and reads it as <c>0.0.0.1</c>, which is not
/// what anybody typing in a hostname field meant. Requiring a dot or a colon rejects that
/// without rejecting anything real — every IPv4 address has three dots and every IPv6
/// address has at least two colons.
/// </para>
/// <para>
/// Both the validator and the handler call this: the validator so the caller gets a 400
/// with the message on the right field, the handler because it is the thing that actually
/// needs the value and a shape that re-parsed by hand would be a second place for the rule
/// to differ.
/// </para>
/// </remarks>
public static class DeviceAddress
{
    /// <summary>Whether <paramref name="value"/> is absent or is a well-formed address.</summary>
    /// <param name="value">The text from the request.</param>
    /// <returns>True when there is nothing to parse, or when what is there parses.</returns>
    public static bool IsAbsentOrWellFormed(string? value) =>
        string.IsNullOrWhiteSpace(value) || TryParse(value, out _);

    /// <summary>Parses <paramref name="value"/>, treating blank as absent.</summary>
    /// <param name="value">The text from the request.</param>
    /// <param name="address">The parsed address, or <see langword="null"/>.</param>
    /// <returns>False only when there was text and it was not an address.</returns>
    public static bool TryParse(string? value, out IPAddress? address)
    {
        address = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();

        // "1" parses as 0.0.0.1 and "0x7f000001" as 127.0.0.1. Neither is something an
        // operator meant to type into an address field.
        if (!trimmed.Contains('.', StringComparison.Ordinal)
            && !trimmed.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        if (!IPAddress.TryParse(trimmed, out var parsed))
        {
            return false;
        }

        address = parsed;
        return true;
    }
}
