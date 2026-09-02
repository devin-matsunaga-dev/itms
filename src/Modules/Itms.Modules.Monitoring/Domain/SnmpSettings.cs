namespace Itms.Modules.Monitoring.Domain;

/// <summary>
/// The bounds and defaults this module puts on SNMP configuration.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read-only by construction, and there is nothing here to make writable.</b>
/// ARCHITECTURE.md §9 and WP-3.5 both say the SNMP surface is read-only, that no SNMP set
/// operation exists in this codebase, and that no write community string is ever accepted
/// in configuration. This type carries a port and the bounds on one community string, and
/// the module carries no notion of a second, writable one — the absence is the enforcement.
/// </para>
/// <para>
/// <b>No version field, deliberately.</b> SPEC.md §7 describes the SNMP scope as "narrow by
/// design" and names no version; a community string is a v1/v2c concept, which is what this
/// shape assumes. SNMPv3's user, authentication and privacy credentials are a different
/// shape entirely, and inventing a version enum now would be designing for a credential
/// model nothing has asked for. WP-3.5, which is the package that actually speaks the
/// protocol, is where that decision belongs.
/// </para>
/// </remarks>
public static class SnmpSettings
{
    /// <summary>The IANA-registered SNMP agent port, and the default for a new device.</summary>
    public const int DefaultPort = 161;

    /// <summary>The lowest port number a device may be polled on.</summary>
    public const int MinPort = 1;

    /// <summary>The highest port number a device may be polled on.</summary>
    public const int MaxPort = 65_535;

    /// <summary>The longest a community string may be.</summary>
    public const int CommunityMaxLength = 128;
}
