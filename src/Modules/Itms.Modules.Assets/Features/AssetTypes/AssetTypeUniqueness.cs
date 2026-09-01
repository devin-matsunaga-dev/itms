using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.AssetTypes;

/// <summary>
/// The duplicate-name check that create and update share.
/// </summary>
/// <remarks>
/// The unique index is the real guarantee — this check can lose a race with a concurrent
/// insert. It exists so the common case comes back as a 409 with a sentence an
/// administrator can act on, rather than as a database exception; the index behind it is
/// what makes the rare case safe.
/// </remarks>
internal static class AssetTypeUniqueness
{
    /// <summary>Looks for an existing type with the same normalised name.</summary>
    /// <param name="database">The assets context.</param>
    /// <param name="normalizedName">The candidate's normalised name.</param>
    /// <param name="excluding">The id being updated, so a row does not collide with itself.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The conflict to return, or <see langword="null"/> when the name is free.</returns>
    public static async Task<Error?> CheckAsync(
        AssetsDbContext database,
        string normalizedName,
        Guid? excluding,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var clash = await database.AssetTypes
            .AsNoTracking()
            .Where(candidate => candidate.Id != excluding && candidate.NormalizedName == normalizedName)
            .Select(candidate => candidate.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return clash is null ? null : AssetsErrors.DuplicateAssetTypeName(clash);
    }
}
