namespace Itms.Modules.Directory.Domain;

/// <summary>
/// Reads the materialised id path back into the ids it was composed from.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Location.Path"/> is written as <c>/{id}/{id}/</c> with each id in "N"
/// format, root first. A node's ancestors are therefore already in its own row, which is
/// what lets the ancestor chain a cascading picker needs be one query on the primary key
/// rather than one query per level. Parsing is pure, so the arithmetic that composes the
/// path and the arithmetic that reads it are asserted against each other without a
/// database.
/// </para>
/// <para>
/// <b>Public, following WP-1.5's call for <c>TicketETag</c> and WP-2.3's for
/// <c>WarrantyWindow</c>.</b> This repository has no <c>InternalsVisibleTo</c>, and path
/// arithmetic that has never been executed against the arithmetic that writes it is
/// arithmetic nobody has checked. No module can reference Directory in any case.
/// </para>
/// </remarks>
public static class LocationPath
{
    /// <summary>How many characters an id occupies in a path, in "N" format.</summary>
    private const int IdLength = 32;

    /// <summary>
    /// The ids in <paramref name="path"/>, root first and including the node's own id.
    /// </summary>
    /// <param name="path">A materialised path, as <see cref="Location.Path"/> holds it.</param>
    /// <returns>The ids, in depth order. Empty when the path holds none.</returns>
    /// <exception cref="FormatException">
    /// A segment is not a 32-character hexadecimal id. The column is written only by
    /// <see cref="Location"/> and rewritten only by a prefix update, so this means the
    /// row has been edited by hand.
    /// </exception>
    public static IReadOnlyList<Guid> ParseIds(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var ids = new List<Guid>();

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.Length != IdLength)
            {
                throw new FormatException($"'{segment}' is not a location path segment.");
            }

            ids.Add(Guid.ParseExact(segment, "N"));
        }

        return ids;
    }
}
