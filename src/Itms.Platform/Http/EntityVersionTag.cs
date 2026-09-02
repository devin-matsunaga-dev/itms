using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Itms.Platform.Http;

/// <summary>
/// Turns a row's PostgreSQL <c>xmin</c> version into an HTTP entity tag, and reads one
/// back off an <c>If-Match</c> header.
/// </summary>
/// <remarks>
/// <para>
/// ARCHITECTURE.md §6 asks for optimistic concurrency "via <c>xmin</c>/rowversion returned
/// as an ETag": the version travels to the client as an opaque string and comes back as a
/// precondition, so two people looking at the same record are told before the second write
/// rather than after it.
/// </para>
/// <para>
/// <b>This is the hoist <c>TicketETag</c> and <c>AssetETag</c> asked for.</b> Both carry
/// this code verbatim and both say the same thing: it is generic HTTP plumbing with no
/// ticket or asset semantics in it, a module may not reference another module, and the
/// <em>third</em> copy is when it moves to the shared kernel rather than being written
/// again. WP-3.1 is that third copy. The two existing copies are deliberately left in
/// place — repointing them means editing two merged packages from a monitoring package —
/// and a package already touching them should delete them in favour of this.
/// </para>
/// <para>
/// <b>Strong tags, not weak.</b> A weak tag promises only semantic equivalence, and
/// <c>If-Match</c> is defined to ignore one. The tag here is exact by construction — it is
/// the row version the database itself moved — so it is quoted and strong.
/// </para>
/// <para>
/// The format is deliberately opaque: a client compares tags, it never parses one. That
/// the value happens to be a decimal <c>xid</c> is the server's business and is free to
/// change.
/// </para>
/// </remarks>
public static class EntityVersionTag
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
    /// precondition was stated, so the request proceeds unconditionally); the header was
    /// <c>*</c> (matches any existing row, which is every row an endpoint can reach after a
    /// successful load, so it also proceeds); or it names one or more versions, in which
    /// case the row has to be one of them.
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
