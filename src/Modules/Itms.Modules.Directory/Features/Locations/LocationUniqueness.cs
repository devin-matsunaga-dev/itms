using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Locations;

/// <summary>The sibling-name check that create, update, and move share.</summary>
/// <remarks>
/// A unique index on <c>(parent_id, normalized_name)</c> is the real guarantee, with
/// nulls treated as equal so two roots cannot share a name either. This check exists so
/// the ordinary case returns a sentence rather than a database exception.
/// </remarks>
internal static class LocationUniqueness
{
    /// <summary>Looks for a sibling of the same name under <paramref name="parentId"/>.</summary>
    /// <param name="database">The directory context.</param>
    /// <param name="parentId">The prospective parent, or <see langword="null"/> at the root.</param>
    /// <param name="normalizedName">The candidate's normalised name.</param>
    /// <param name="excluding">The id being changed, so a node does not collide with itself.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The conflict to return, or <see langword="null"/> when the name is free.</returns>
    public static async Task<Error?> CheckAsync(
        DirectoryDbContext database,
        Guid? parentId,
        string normalizedName,
        Guid? excluding,
        CancellationToken cancellationToken)
    {
        var clash = await database.Locations
            .AsNoTracking()
            .Where(candidate =>
                candidate.Id != excluding &&
                candidate.ParentId == parentId &&
                candidate.NormalizedName == normalizedName)
            .Select(candidate => candidate.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return clash is null ? null : DirectoryErrors.DuplicateLocationName(clash);
    }
}
