using Itms.Contracts.Lookups;

namespace Itms.Modules.Directory.Features.Usage;

/// <summary>
/// Fans a usage question out across every module that holds a reference, and folds the
/// answers into one ordered breakdown.
/// </summary>
/// <remarks>
/// <para>
/// The counters arrive as <c>IEnumerable&lt;IDirectoryUsageLookup&gt;</c> from the
/// container, so Directory never names Assets, Helpdesk, or Identity — the composition
/// root does, by registering them. A deployment that drops a module drops its counter and
/// the total shrinks honestly, rather than the read failing.
/// </para>
/// <para>
/// The counters run sequentially rather than through <c>Task.WhenAll</c>. Every one of
/// them is a scoped <c>DbContext</c> built on the one connection the ambient session owns
/// (STATUS.md, WP-0.4), and issuing concurrent queries on a single Npgsql connection is
/// an exception rather than a speed-up. Three indexed counts in series is the right cost
/// for a question asked once, by an administrator, before a destructive click.
/// </para>
/// </remarks>
/// <param name="counters">Every module's counter, in registration order. May be empty.</param>
internal sealed class DirectoryUsageReader(IEnumerable<IDirectoryUsageLookup> counters)
{
    /// <summary>Counts every module's references to <paramref name="departmentId"/>.</summary>
    /// <param name="departmentId">The department to report on.</param>
    /// <param name="cancellationToken">Cancels the counts.</param>
    /// <returns>The ordered breakdown and its total.</returns>
    public Task<(IReadOnlyList<UsageCountResponse> References, int Total)> ForDepartmentAsync(
        Guid departmentId,
        CancellationToken cancellationToken) =>
        CollectAsync(counter => counter.CountForDepartmentAsync(departmentId, cancellationToken));

    /// <summary>Counts every module's references to <paramref name="locationId"/>.</summary>
    /// <param name="locationId">The location to report on.</param>
    /// <param name="cancellationToken">Cancels the counts.</param>
    /// <returns>The ordered breakdown and its total.</returns>
    public Task<(IReadOnlyList<UsageCountResponse> References, int Total)> ForLocationAsync(
        Guid locationId,
        CancellationToken cancellationToken) =>
        CollectAsync(counter => counter.CountForLocationAsync(locationId, cancellationToken));

    private async Task<(IReadOnlyList<UsageCountResponse> References, int Total)> CollectAsync(
        Func<IDirectoryUsageLookup, Task<DirectoryUsage>> count)
    {
        var references = new List<UsageCountResponse>();

        foreach (var counter in counters)
        {
            var usage = await count(counter).ConfigureAwait(false);
            references.Add(new UsageCountResponse(usage.EntityName, usage.Count));
        }

        // Ordered by name so two reads of the same entry render identically. Registration
        // order is the composition root's business and is not a promise to a client.
        references.Sort(static (left, right) => string.CompareOrdinal(left.EntityName, right.EntityName));

        return (references, references.Sum(reference => reference.Count));
    }
}
