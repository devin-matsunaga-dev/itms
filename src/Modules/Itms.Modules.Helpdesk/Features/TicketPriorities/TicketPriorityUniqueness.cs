using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Features.TicketPriorities;

/// <summary>
/// The duplicate-name and duplicate-code check that create and update share.
/// </summary>
/// <remarks>
/// The unique indexes are the real guarantee — this check can lose a race with a
/// concurrent insert. It exists so the common case comes back as a 409 with a sentence an
/// administrator can act on, rather than as a database exception; the indexes behind it
/// are what make the rare case safe.
/// </remarks>
internal static class TicketPriorityUniqueness
{
    /// <summary>Looks for an existing priority with the same normalised name or code.</summary>
    /// <param name="database">The helpdesk context.</param>
    /// <param name="normalizedName">The candidate's normalised name.</param>
    /// <param name="code">The candidate's normalised code, or <see langword="null"/> when the code is not changing.</param>
    /// <param name="excluding">The id being updated, so a row does not collide with itself.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The conflict to return, or <see langword="null"/> when the fields are free.</returns>
    public static async Task<Error?> CheckAsync(
        HelpdeskDbContext database,
        string normalizedName,
        string? code,
        Guid? excluding,
        CancellationToken cancellationToken)
    {
        var clash = await database.TicketPriorities
            .AsNoTracking()
            .Where(candidate => candidate.Id != excluding)
            .Where(candidate =>
                candidate.NormalizedName == normalizedName ||
                (code != null && candidate.Code == code))
            .Select(candidate => new { candidate.Name, candidate.NormalizedName, candidate.Code })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (clash is null)
        {
            return null;
        }

        return string.Equals(clash.NormalizedName, normalizedName, StringComparison.Ordinal)
            ? HelpdeskErrors.DuplicatePriorityName(clash.Name)
            : HelpdeskErrors.DuplicatePriorityCode(clash.Code);
    }
}
