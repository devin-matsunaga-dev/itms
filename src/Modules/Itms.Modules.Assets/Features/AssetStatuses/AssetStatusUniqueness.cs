using Itms.Modules.Assets.Domain;
using Itms.Modules.Assets.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Assets.Features.AssetStatuses;

/// <summary>
/// The duplicate-name and duplicate-code checks that create and update share.
/// </summary>
/// <remarks>
/// The unique indexes are the real guarantee — these checks can lose a race with a
/// concurrent insert. They exist so the common case comes back as a 409 with a sentence an
/// administrator can act on, rather than as a database exception.
/// </remarks>
internal static class AssetStatusUniqueness
{
    /// <summary>Looks for an existing status with the same normalised name.</summary>
    /// <param name="database">The assets context.</param>
    /// <param name="normalizedName">The candidate's normalised name.</param>
    /// <param name="excluding">The id being updated, so a row does not collide with itself.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The conflict to return, or <see langword="null"/> when the name is free.</returns>
    public static async Task<Error?> CheckNameAsync(
        AssetsDbContext database,
        string normalizedName,
        Guid? excluding,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var clash = await database.AssetStatuses
            .AsNoTracking()
            .Where(candidate => candidate.Id != excluding && candidate.NormalizedName == normalizedName)
            .Select(candidate => candidate.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return clash is null ? null : AssetsErrors.DuplicateAssetStatusName(clash);
    }

    /// <summary>
    /// Looks for an existing status with the same code.
    /// </summary>
    /// <remarks>
    /// Only creation calls this: the code is immutable, so an update has nothing to check.
    /// </remarks>
    /// <param name="database">The assets context.</param>
    /// <param name="code">The candidate's normalised code.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The conflict to return, or <see langword="null"/> when the code is free.</returns>
    public static async Task<Error?> CheckCodeAsync(
        AssetsDbContext database,
        string code,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var taken = await database.AssetStatuses
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Code == code, cancellationToken)
            .ConfigureAwait(false);

        return taken ? AssetsErrors.DuplicateAssetStatusCode(code) : null;
    }
}
