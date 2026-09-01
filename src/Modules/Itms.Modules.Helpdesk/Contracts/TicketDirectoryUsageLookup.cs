using Itms.Contracts.Lookups;
using Itms.Modules.Helpdesk.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itms.Modules.Helpdesk.Contracts;

/// <summary>
/// Helpdesk's half of <see cref="IDirectoryUsageLookup"/>: how many tickets are filed
/// against a department.
/// </summary>
/// <remarks>
/// <para>
/// A ticket carries a department and no location, so the location count is always zero
/// here rather than absent — a counter that answered only half the interface would make
/// every caller ask which half it implements.
/// </para>
/// <para>
/// Resolved and closed tickets are counted. A ticket keeps rendering its department for
/// as long as it is retained, and history is precisely what invariant 9 and the audit
/// trail exist to keep readable.
/// </para>
/// </remarks>
/// <param name="database">The helpdesk context.</param>
internal sealed class TicketDirectoryUsageLookup(HelpdeskDbContext database) : IDirectoryUsageLookup
{
    /// <summary>What this counter reports, as it is rendered to an administrator.</summary>
    public const string EntityName = "tickets";

    /// <inheritdoc />
    public async Task<DirectoryUsage> CountForDepartmentAsync(Guid departmentId, CancellationToken cancellationToken) =>
        new(EntityName, await database.Tickets
            .AsNoTracking()
            .CountAsync(ticket => ticket.DepartmentId == departmentId, cancellationToken)
            .ConfigureAwait(false));

    /// <inheritdoc />
    public Task<DirectoryUsage> CountForLocationAsync(Guid locationId, CancellationToken cancellationToken) =>
        // No query: a ticket has no location column. WP-2.5 links tickets to assets, and
        // an asset has a location — but that is the asset's reference, already counted by
        // AssetDirectoryUsageLookup, and counting it twice would inflate the total an
        // administrator is deciding against.
        Task.FromResult(new DirectoryUsage(EntityName, 0));
}
