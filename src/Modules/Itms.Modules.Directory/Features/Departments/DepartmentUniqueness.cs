using Itms.Modules.Directory.Domain;
using Itms.Modules.Directory.Persistence;
using Itms.Platform.Results;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Directory.Features.Departments;

/// <summary>
/// The duplicate-name and duplicate-code check that create and update share.
/// </summary>
/// <remarks>
/// The unique indexes are the real guarantee — this check can lose a race with a
/// concurrent insert. It exists so the common case comes back as a 409 with a sentence
/// an administrator can act on, rather than as a database exception; the index behind it
/// is what makes the rare case safe.
/// </remarks>
internal static class DepartmentUniqueness
{
    /// <summary>Looks for an existing department with the same normalised name or code.</summary>
    /// <param name="database">The directory context.</param>
    /// <param name="normalizedName">The candidate's normalised name.</param>
    /// <param name="normalizedCode">The candidate's normalised code, or <see langword="null"/>.</param>
    /// <param name="excluding">The id being updated, so a row does not collide with itself.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The conflict to return, or <see langword="null"/> when the fields are free.</returns>
    public static async Task<Error?> CheckAsync(
        DirectoryDbContext database,
        string normalizedName,
        string? normalizedCode,
        Guid? excluding,
        CancellationToken cancellationToken)
    {
        var clash = await database.Departments
            .AsNoTracking()
            .Where(candidate => candidate.Id != excluding)
            .Where(candidate =>
                candidate.NormalizedName == normalizedName ||
                (normalizedCode != null && candidate.NormalizedCode == normalizedCode))
            .Select(candidate => new { candidate.Name, candidate.NormalizedName, candidate.Code })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (clash is null)
        {
            return null;
        }

        return string.Equals(clash.NormalizedName, normalizedName, StringComparison.Ordinal)
            ? DirectoryErrors.DuplicateDepartmentName(clash.Name)
            : DirectoryErrors.DuplicateDepartmentCode(clash.Code!);
    }
}
