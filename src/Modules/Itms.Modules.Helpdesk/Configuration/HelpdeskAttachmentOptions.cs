namespace Itms.Modules.Helpdesk.Configuration;

/// <summary>
/// What a deployment may decide about ticket attachments: where the bytes go, how large
/// one may be, and which kinds are accepted.
/// </summary>
/// <remarks>
/// <para>
/// Bound from the <c>Helpdesk:Attachments</c> configuration section and validated at
/// startup, so a deployment that gets one of these wrong fails to start rather than
/// failing on the first upload.
/// </para>
/// <para>
/// <b><see cref="RootPath"/>'s default is safe, not sufficient.</b> <c>App_Data</c> is the
/// conventional name for a directory the framework never serves, and nothing in this host
/// maps static files out of it, so an unconfigured deployment satisfies CONVENTIONS.md's
/// "outside the web root" and cannot leak a file by URL. What it does not satisfy is
/// durability: the path is inside the deployment directory, so a container that does not
/// mount a volume there loses every attachment on the next release. <b>A production
/// deployment must set this to a mounted path</b> — that is WP-6.6's runbook, and it is
/// recorded in STATUS.md so it cannot be discovered after the first redeploy.
/// </para>
/// <para>
/// A default was necessary rather than merely convenient: the OpenAPI document is
/// generated at build by booting this host with no configuration at all, so an option with
/// no valid default fails the build rather than the deployment — the same shape of problem
/// WP-1.1 hit with the reference-data seeder.
/// </para>
/// <para>
/// <b><see cref="AllowedExtensions"/> can only narrow, never widen.</b> Startup rejects an
/// extension that <c>AttachmentContentRules</c> has no signature rule for. Configuration
/// decides policy; it does not get to introduce a file type nothing knows how to check.
/// </para>
/// </remarks>
public sealed class HelpdeskAttachmentOptions
{
    /// <summary>The configuration section these are bound from.</summary>
    public const string SectionName = "Helpdesk:Attachments";

    /// <summary>The default size cap: 10 MB, which is a screenshot or a log, generously.</summary>
    public const long DefaultMaxBytes = 10L * 1024 * 1024;

    /// <summary>
    /// Where attachments go when nobody says. Relative to the content root, and inside the
    /// directory convention reserves for content that is never served.
    /// </summary>
    public const string DefaultRootPath = "App_Data/ticket-attachments";

    /// <summary>
    /// Where the bytes are written. Absolute, or relative to the host's content root.
    /// Must be outside the web root (CONVENTIONS.md); nothing here serves it statically.
    /// </summary>
    public string RootPath { get; set; } = DefaultRootPath;

    /// <summary>The largest single attachment accepted.</summary>
    public long MaxBytes { get; set; } = DefaultMaxBytes;

    /// <summary>
    /// The file extensions accepted, lower case and dotted. What a helpdesk actually
    /// receives: screenshots, logs, exports, and the occasional document.
    /// </summary>
    public IList<string> AllowedExtensions { get; set; } =
    [
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".txt",
        ".log",
        ".csv",
        ".zip",
        ".docx",
        ".xlsx",
    ];
}
