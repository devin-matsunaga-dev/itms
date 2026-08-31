using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Itms.Modules.Helpdesk.Features.Tickets;

/// <summary>
/// Turns a ticket's <c>xmin</c> row version into an HTTP entity tag, and reads one back
/// off an <c>If-Match</c> header.
/// </summary>
/// <remarks>
/// <para>
/// ARCHITECTURE.md §6 asks for optimistic concurrency on tickets "via <c>xmin</c>/rowversion
/// returned as an ETag". WP-1.2 mapped the token, WP-1.3 caught the exception it raises,
/// and this is the last piece: the version travels to the client as an opaque string and
/// comes back as a precondition, so a stale editor is told <em>before</em> it has typed a
/// resolution rather than after.
/// </para>
/// <para>
/// <b>Strong tags, not weak.</b> A weak tag promises only semantic equivalence, and
/// <c>If-Match</c> is defined to ignore one. The tag here is exact by construction — it is
/// the row version the database itself moved — so it is quoted and strong.
/// </para>
/// <para>
/// The format is deliberately opaque: a client compares tags, it never parses one. That
/// the value happens to be a decimal <c>xid</c> is this module's business and is free to
/// change.
/// </para>
/// <para>
/// <b>Public rather than internal</b>, following the call WP-1.3 made for
/// <c>TicketStateMachine</c> and WP-1.4 for <c>TicketHistoryRecorder</c>: the header
/// parsing is what decides whether a stale precondition is honoured, it has to be asserted
/// against the code the endpoint actually calls, and this repository has no
/// <c>InternalsVisibleTo</c>. No module can reference Helpdesk anyway.
/// </para>
/// </remarks>
public static class TicketETag
{
    /// <summary>The tag for a row at version <paramref name="version"/>.</summary>
    /// <param name="version">The <c>xmin</c> value read back through <c>EF.Property</c>.</param>
    /// <returns>A quoted, strong entity tag.</returns>
    public static string For(uint version) =>
        string.Create(CultureInfo.InvariantCulture, $"\"{version}\"");

    /// <summary>
    /// Reads the <c>If-Match</c> precondition off a request.
    /// </summary>
    /// <remarks>
    /// Three answers, and the caller has to tell them apart: the header was absent (no
    /// precondition was stated, so the request proceeds unconditionally — this is WP-1.3's
    /// behaviour, unchanged); the header was <c>*</c> (matches any existing row, which is
    /// every row this endpoint can reach, so it also proceeds); or it names one or more
    /// versions, in which case the row has to be one of them.
    /// </remarks>
    /// <param name="request">The incoming request.</param>
    /// <returns>
    /// The versions the caller will accept, or <see langword="null"/> when it stated no
    /// precondition at all. An empty set means the header was present and named nothing
    /// this row could ever match — a malformed tag — which RFC 9110 §13.1.1 makes a failed
    /// precondition rather than a bad request.
    /// </returns>
    public static IReadOnlySet<uint>? PreconditionFrom(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var header = request.Headers.IfMatch;

        if (header.Count == 0)
        {
            return null;
        }

        if (!EntityTagHeaderValue.TryParseStrictList(header, out var tags))
        {
            // Present but unparseable. Not null — the caller did state a precondition, and
            // an empty set is what fails it.
            return new HashSet<uint>();
        }

        var versions = new HashSet<uint>();

        foreach (var tag in tags)
        {
            if (tag == EntityTagHeaderValue.Any)
            {
                // "*" matches any existing representation. The row was found, so it matches.
                return null;
            }

            if (tag.IsWeak)
            {
                // RFC 9110 §13.1.1: If-Match uses the strong comparison function, so a weak
                // tag can never satisfy it. Skipping it rather than parsing its value is the
                // difference between refusing a stale write and waving it through.
                continue;
            }

            var value = tag.Tag.Value;

            if (value is { Length: > 2 }
                && uint.TryParse(value.AsSpan(1, value.Length - 2), CultureInfo.InvariantCulture, out var version))
            {
                versions.Add(version);
            }
        }

        return versions;
    }
}
