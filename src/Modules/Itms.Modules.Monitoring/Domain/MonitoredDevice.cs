using System.Net;
using Itms.Platform.Text;

namespace Itms.Modules.Monitoring.Domain;

/// <summary>
/// A piece of equipment the poller checks: the network half of an asset.
/// </summary>
/// <remarks>
/// <para>
/// <b>A monitored device is always an asset (invariant 6), and this class is one half of
/// why.</b> There is no constructor and no method that produces a device without an
/// <see cref="AssetId"/> and an <see cref="AssetTag"/>, and <see cref="NewDevice"/> — the
/// only way in — is built by a handler from what <c>IAssetLookup</c> answered. Monitoring
/// therefore cannot invent a device record of its own: it can only project one over an
/// asset that already exists. The unique index on <c>asset_id</c> is the other half, and it
/// is what stops one machine acquiring two monitoring states.
/// </para>
/// <para>
/// <b>The asset it is cannot be changed.</b> <see cref="DeviceSettings"/> has no field for
/// it, so there is no code path that re-points a device at a different asset — the same
/// structural enforcement invariant 4 gets from <c>AssetEdit</c> having no field for the
/// tag. Re-pointing one would make every check result, outage and alert already filed
/// against it describe a different machine.
/// </para>
/// <para>
/// <b>The cached asset tag can never go stale, unusually for a cached display string.</b>
/// §3 rule 6 requires an id plus a cached display string rather than a foreign key across a
/// module boundary, and every other such copy in this system decays — a renamed department
/// on a ticket, a moved room on an asset. This one does not, because invariant 4 makes an
/// asset tag immutable: there is no rename event to miss, and no refresh consumer owed.
/// It is here because <c>DeviceWentOffline</c> carries it and an alert has to be readable
/// without a lookup per row.
/// </para>
/// <para>
/// <b>What this class deliberately does not carry is the device's state.</b> There is no
/// online/offline column, no consecutive-failure counter and no last-seen timestamp:
/// deciding when a run of failures becomes "offline" and when one success restores it is
/// <c>WP-3.3</c>'s state machine, and giving it columns here would be inventing the shape
/// that package has to design — the call WP-2.3 made when it left hostname to this one.
/// WP-3.3 adds them with a migration of its own.
/// </para>
/// </remarks>
public sealed class MonitoredDevice
{
    /// <summary>
    /// The longest a hostname may be. RFC 1035 §2.3.4 bounds a fully-qualified domain name
    /// at 255 octets including the length prefix and the root label, which is 253
    /// characters written out.
    /// </summary>
    public const int HostnameMaxLength = 253;

    /// <summary>
    /// The longest a cached asset tag may be.
    /// </summary>
    /// <remarks>
    /// The same 64 characters <c>AssetTagRules.MaxLength</c> allows. Spelled again rather
    /// than shared because a module may not reference another module; if Assets ever widens
    /// its tag, a tag too long to cache here would be refused at registration rather than
    /// truncated, which is why the length is checked and not trimmed to fit.
    /// </remarks>
    public const int AssetTagMaxLength = 64;

    /// <summary>The shortest poll interval a device may be given.</summary>
    /// <remarks>
    /// Ten seconds. ARCHITECTURE.md §9 defaults to sixty and says nothing about a floor;
    /// this one exists so a mistyped value cannot turn one device into a denial-of-service
    /// against itself. A deployment that genuinely wants sub-ten-second checks wants a
    /// different tool.
    /// </remarks>
    public const int MinPollIntervalSeconds = 10;

    /// <summary>The longest poll interval a device may be given: one day.</summary>
    public const int MaxPollIntervalSeconds = 86_400;

    /// <summary>The poll interval a device gets when none is named (ARCHITECTURE.md §9).</summary>
    public const int DefaultPollIntervalSeconds = 60;

    /// <summary>The fewest consecutive failures that may declare a device offline.</summary>
    public const int MinFailureThreshold = 1;

    /// <summary>The most consecutive failures that may be required before declaring one offline.</summary>
    public const int MaxFailureThreshold = 10;

    /// <summary>The failure threshold a device gets when none is named (ARCHITECTURE.md §9).</summary>
    public const int DefaultFailureThreshold = 3;

    private MonitoredDevice()
    {
        // EF Core materialisation; the tag is non-null in the database.
        AssetTag = null!;
    }

    /// <summary>The device's id.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// The asset this device is (invariant 6). Not a foreign key — §3 rule 6 forbids one
    /// across a module boundary — and unique, so one asset has at most one device.
    /// </summary>
    public Guid AssetId { get; private set; }

    /// <summary>That asset's tag, cached per §3 rule 6 and immutable by invariant 4.</summary>
    public string AssetTag { get; private set; }

    /// <summary>The name the device answers to, or <see langword="null"/>.</summary>
    public string? Hostname { get; private set; }

    /// <summary>
    /// <see cref="Hostname"/> lower-cased, or <see langword="null"/>. Hostnames are
    /// case-insensitive (RFC 4343), so this is what searching and matching read.
    /// </summary>
    /// <remarks>
    /// Deliberately <em>not</em> unique. Two devices legitimately answer to one name across
    /// separate networks — a management VRF and a production one, two sites running the
    /// same appliance image — and refusing the second would make honest estates
    /// unrecordable. The index behind it exists for the search, not for a constraint.
    /// </remarks>
    public string? NormalizedHostname { get; private set; }

    /// <summary>
    /// The address the device is polled at, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Held as an <see cref="System.Net.IPAddress"/> and stored as PostgreSQL's
    /// <c>inet</c>, not as text: the database then refuses a value that is not an address
    /// at all, and the parse at the edge means one address has one spelling — an IPv6
    /// address written out in full and the same one abbreviated cannot be recorded as two
    /// devices. It is converted to and from a string at the API edge, so the contract
    /// describes a plain string and nothing downstream has to know the storage type.
    /// </remarks>
    public IPAddress? IpAddress { get; private set; }

    /// <summary>
    /// Whether the poller should check this device at all.
    /// </summary>
    /// <remarks>
    /// SPEC.md §6 names monitoring enabled/disabled per device. Moved only by
    /// <see cref="SetMonitoringEnabled"/>, never by an edit: switching monitoring off is an
    /// operational act with its own audit line, not a correction.
    /// </remarks>
    public bool MonitoringEnabled { get; private set; }

    /// <summary>How often the device is checked, in seconds.</summary>
    public int PollIntervalSeconds { get; private set; }

    /// <summary>
    /// How many consecutive failed checks declare the device offline (ARCHITECTURE.md §9).
    /// </summary>
    /// <remarks>
    /// Stored here and acted on by <c>WP-3.3</c>: the poller reports raw results and the
    /// host owns the state transition, so this is configuration the ingestion endpoint
    /// reads rather than something the poller decides for itself.
    /// </remarks>
    public int FailureThreshold { get; private set; }

    /// <summary>Whether the read-only SNMP checks apply to this device.</summary>
    public bool SnmpEnabled { get; private set; }

    /// <summary>The port those checks use. Defaults to 161.</summary>
    public int SnmpPort { get; private set; }

    /// <summary>
    /// The read-only SNMP community string, or <see langword="null"/> when none is set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the one secret in the module, and the design keeps it write-only.</b> It
    /// has to be a column rather than configuration — ARCHITECTURE.md §7 puts the poller's
    /// own service credential in configuration precisely because there is one of it, and
    /// this varies per device. What follows from that is a set of rules the rest of the
    /// module is built to keep:
    /// </para>
    /// <para>
    /// It is never returned by a read. No response shape in this module carries it;
    /// <c>DeviceResponse</c> answers <c>SnmpCredentialSet</c> instead, so a screen can say
    /// whether one is configured without being told what it is. It is never logged — see
    /// <c>MonitoringLog</c>, whose messages name the device and no credential. It is never
    /// audited in plaintext: <c>MonitoringAudit</c>'s two credential actions record that it
    /// was set or cleared and carry no value, because an audit trail somebody can read the
    /// secret out of is a second copy of the secret. And it cannot be reached by
    /// <see cref="Update"/>, which is what stops a full-replacement <c>PUT</c> wiping it.
    /// </para>
    /// <para>
    /// It is stored as plaintext in the column. Encryption at rest and key management are a
    /// deployment concern for <c>WP-6.3</c>, which is a defensible place for them only
    /// because nothing in the application's contract depends on the value being readable
    /// back out — the sole path off this row is <c>WP-3.2</c>'s authenticated configuration
    /// pull, which is a machine-to-machine boundary of its own.
    /// </para>
    /// <para>
    /// A write community string does not exist here and must never be added:
    /// ARCHITECTURE.md §9 says no SNMP write path exists in this codebase and no write
    /// community is ever accepted in configuration. WP-3.5's done-criterion is a grep.
    /// </para>
    /// </remarks>
    public string? SnmpCommunity { get; private set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Who created it, or <see langword="null"/> when the system did.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>When the row was last changed (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who last changed it, or <see langword="null"/> when the system did.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Whether an SNMP community string is currently set, without saying what it is.</summary>
    public bool HasSnmpCredential => SnmpCommunity is not null;

    /// <summary>Registers a device over an asset.</summary>
    /// <remarks>
    /// The device must be reachable somehow: a hostname, an address, or both. A row with
    /// neither is a device the poller could never check, and it is refused here rather than
    /// discovered as a silent no-op on the first polling cycle. The endpoint validator
    /// refuses the same thing as a 400 with a per-field message, so reaching the exception
    /// means a caller inside the module built one from unvalidated input.
    /// </remarks>
    /// <param name="device">The facts being recorded.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is registering it, or <see langword="null"/> for the system.</param>
    /// <returns>The new device, not yet persisted.</returns>
    public static MonitoredDevice Register(NewDevice device, DateTimeOffset now, Guid? actor)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (device.AssetId == Guid.Empty)
        {
            throw new ArgumentException("A monitored device is always an asset.", nameof(device));
        }

        var hostname = ReferenceText.Optional(device.Hostname, HostnameMaxLength, nameof(device));

        if (hostname is null && device.IpAddress is null)
        {
            throw new ArgumentException(
                "A monitored device needs a hostname or an IP address.",
                nameof(device));
        }

        return new MonitoredDevice
        {
            // v7 so the primary key is time-ordered and its index does not fragment.
            Id = Guid.CreateVersion7(),
            AssetId = device.AssetId,
            AssetTag = ReferenceText.Name(device.AssetTag, AssetTagMaxLength, nameof(device)),
            Hostname = hostname,
            NormalizedHostname = hostname?.ToLowerInvariant(),
            IpAddress = device.IpAddress,
            MonitoringEnabled = device.MonitoringEnabled,
            PollIntervalSeconds = BoundedInterval(device.PollIntervalSeconds, nameof(device)),
            FailureThreshold = BoundedThreshold(device.FailureThreshold, nameof(device)),
            SnmpEnabled = device.SnmpEnabled,
            SnmpPort = BoundedPort(device.SnmpPort, nameof(device)),
            SnmpCommunity = ReferenceText.Optional(
                device.SnmpCommunity,
                SnmpSettings.CommunityMaxLength,
                nameof(device)),
            CreatedAt = now,
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor,
        };
    }

    /// <summary>Corrects where the device is reached and how it is polled.</summary>
    /// <remarks>
    /// <para>
    /// <b>An edit that moves nothing writes nothing.</b> The normalised settings are
    /// compared against what the device already carries by record value and the audit
    /// columns are stamped only when they actually differ, so a form re-submitted unchanged
    /// leaves <c>xmin</c> alone — which means it does not refuse every other reader's
    /// precondition with a 412 for a change that never happened. This is the call
    /// <c>Asset.Update</c> made at WP-2.6b, for the same reason.
    /// </para>
    /// <para>
    /// The asset, the community string, and the monitoring switch are not parameters. See
    /// <see cref="DeviceSettings"/> for why each is absent rather than validated away.
    /// </para>
    /// </remarks>
    /// <param name="settings">The settings as they should now read.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is correcting it, or <see langword="null"/> for the system.</param>
    /// <returns>The settings actually applied, normalised — the "after" half of a diff.</returns>
    public DeviceSettings Update(DeviceSettings settings, DateTimeOffset now, Guid? actor)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var hostname = ReferenceText.Optional(settings.Hostname, HostnameMaxLength, nameof(settings));

        if (hostname is null && settings.IpAddress is null)
        {
            throw new ArgumentException(
                "A monitored device needs a hostname or an IP address.",
                nameof(settings));
        }

        var normalized = settings with
        {
            Hostname = hostname,
            PollIntervalSeconds = BoundedInterval(settings.PollIntervalSeconds, nameof(settings)),
            FailureThreshold = BoundedThreshold(settings.FailureThreshold, nameof(settings)),
            SnmpPort = BoundedPort(settings.SnmpPort, nameof(settings)),
        };

        // Record equality, so this is every field of the editable half at once. A field
        // added to DeviceSettings therefore joins the comparison without being named here.
        if (DeviceSettings.Of(this) == normalized)
        {
            return normalized;
        }

        Hostname = normalized.Hostname;
        NormalizedHostname = normalized.Hostname?.ToLowerInvariant();
        IpAddress = normalized.IpAddress;
        PollIntervalSeconds = normalized.PollIntervalSeconds;
        FailureThreshold = normalized.FailureThreshold;
        SnmpEnabled = normalized.SnmpEnabled;
        SnmpPort = normalized.SnmpPort;
        UpdatedAt = now;
        UpdatedBy = actor;

        return normalized;
    }

    /// <summary>Turns monitoring on or off for this device.</summary>
    /// <remarks>
    /// Its own method, and its own pair of routes, because it is the one setting whose
    /// change an operator will be asked about afterwards: a device nobody was watching is
    /// how an outage goes unnoticed. Answering whether anything moved lets the caller skip
    /// an audit row that would claim a change that did not happen.
    /// </remarks>
    /// <param name="enabled">True to have the poller pick the device up.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is doing it, or <see langword="null"/> for the system.</param>
    /// <returns>True if the switch actually moved.</returns>
    public bool SetMonitoringEnabled(bool enabled, DateTimeOffset now, Guid? actor)
    {
        if (MonitoringEnabled == enabled)
        {
            return false;
        }

        MonitoringEnabled = enabled;
        UpdatedAt = now;
        UpdatedBy = actor;
        return true;
    }

    /// <summary>Sets the read-only SNMP community string.</summary>
    /// <remarks>
    /// Its own method for the reason <see cref="SnmpCommunity"/> gives: a full-replacement
    /// edit must not be able to clear a secret it was never given. Unlike
    /// <see cref="Update"/> this does not compare against the current value and return
    /// early — that would let a caller learn the secret by watching which requests move the
    /// <c>ETag</c>.
    /// </remarks>
    /// <param name="community">The community string. Required and non-blank.</param>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is doing it, or <see langword="null"/> for the system.</param>
    public void SetSnmpCredential(string community, DateTimeOffset now, Guid? actor)
    {
        SnmpCommunity = ReferenceText.Name(community, SnmpSettings.CommunityMaxLength, nameof(community));
        UpdatedAt = now;
        UpdatedBy = actor;
    }

    /// <summary>Removes the SNMP community string.</summary>
    /// <param name="now">The current instant, from <c>IClock</c>.</param>
    /// <param name="actor">Who is doing it, or <see langword="null"/> for the system.</param>
    /// <returns>True if there was one to remove.</returns>
    public bool ClearSnmpCredential(DateTimeOffset now, Guid? actor)
    {
        if (SnmpCommunity is null)
        {
            return false;
        }

        SnmpCommunity = null;
        UpdatedAt = now;
        UpdatedBy = actor;
        return true;
    }

    private static int BoundedInterval(int seconds, string parameterName) =>
        seconds is >= MinPollIntervalSeconds and <= MaxPollIntervalSeconds
            ? seconds
            : throw new ArgumentOutOfRangeException(
                parameterName,
                seconds,
                $"A poll interval must be between {MinPollIntervalSeconds} and {MaxPollIntervalSeconds} seconds.");

    private static int BoundedThreshold(int failures, string parameterName) =>
        failures is >= MinFailureThreshold and <= MaxFailureThreshold
            ? failures
            : throw new ArgumentOutOfRangeException(
                parameterName,
                failures,
                $"A failure threshold must be between {MinFailureThreshold} and {MaxFailureThreshold}.");

    private static int BoundedPort(int port, string parameterName) =>
        port is >= SnmpSettings.MinPort and <= SnmpSettings.MaxPort
            ? port
            : throw new ArgumentOutOfRangeException(
                parameterName,
                port,
                $"A port must be between {SnmpSettings.MinPort} and {SnmpSettings.MaxPort}.");
}
