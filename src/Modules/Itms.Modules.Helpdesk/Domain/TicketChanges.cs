namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// Works out which history entries a change to a ticket owes, by comparing the ticket
/// before it with the ticket after it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is one function rather than a line in each handler.</b> Invariant 3 says
/// every meaningful ticket change writes a history entry in the same transaction as the
/// change. A handler that decides for itself which entries its own change produces is a
/// handler that can be written, reviewed, and merged having quietly produced none —
/// WP-1.6's assignment and whatever package first moves a priority both arrive after this
/// one. Here, they get their entries by comparing two snapshots, and the only thing they
/// have to remember is to take the first one.
/// </para>
/// <para>
/// It is pure and it touches no database, which is what lets the unit suite exhaust it.
/// Its only external input is the pair of priority names, because a ticket holds its
/// priority as an id and the name is on another row.
/// </para>
/// </remarks>
public static class TicketChanges
{
    /// <summary>
    /// The entries owed for the move from <paramref name="before"/> to
    /// <paramref name="after"/>, in the order a timeline reads them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A dimension that did not move produces nothing. That is what keeps a handler which
    /// writes back every field from making the timeline look like a rewrite of the whole
    /// ticket — the same rule ARCHITECTURE.md §8 sets for the audit diff.
    /// </para>
    /// <para>
    /// One change can produce more than one entry, and that is not a duplicate: resolving
    /// a ticket both moves its status and records what was done, and a timeline that shows
    /// only the first has lost the sentence a technician actually wants to read.
    /// </para>
    /// </remarks>
    /// <param name="before">The ticket as it stood before the change.</param>
    /// <param name="after">The ticket as it stands now.</param>
    /// <param name="priorityNames">
    /// The names the two priorities carried, required when — and only when — the priority
    /// ids differ.
    /// </param>
    /// <param name="assetTags">
    /// The tags the two assets carried, required when — and only when — the related asset
    /// ids differ.
    /// </param>
    /// <returns>The entries owed, possibly none.</returns>
    /// <exception cref="ArgumentException">
    /// The priority or the related asset moved and no names were supplied to describe it.
    /// </exception>
    public static IReadOnlyList<TicketChange> Between(
        TicketSnapshot before,
        TicketSnapshot after,
        TicketPriorityNames? priorityNames = null,
        TicketAssetTags? assetTags = null)
    {
        var changes = new List<TicketChange>(capacity: 5);

        if (before.Status != after.Status)
        {
            changes.Add(new TicketChange(TicketChangeKind.Status, before.Status.ToString(), after.Status.ToString()));
        }

        if (before.PriorityId != after.PriorityId)
        {
            if (priorityNames is not { } names)
            {
                // A programming error rather than a caller's mistake: the recorder resolves
                // these before it asks, so reaching here means a new call site forgot to.
                // Recording "the priority changed" without saying to what would be worse
                // than the exception, because it looks like coverage and is not.
                throw new ArgumentException(
                    "A priority change needs the names of both priorities to describe it.",
                    nameof(priorityNames));
            }

            changes.Add(new TicketChange(TicketChangeKind.Priority, names.From, names.To));
        }

        // Compared by id, not by name: two technicians can share a display name, and a
        // reassignment between them is still a reassignment.
        if (before.AssigneeId != after.AssigneeId)
        {
            changes.Add(new TicketChange(TicketChangeKind.Assignment, before.AssigneeName, after.AssigneeName));
        }

        if (!string.Equals(before.ResolutionNotes, after.ResolutionNotes, StringComparison.Ordinal))
        {
            changes.Add(new TicketChange(TicketChangeKind.Resolution, before.ResolutionNotes, after.ResolutionNotes));
        }

        // Set when the ticket is parked and cleared when it resumes, so both directions
        // produce a line: "on hold — waiting on the vendor", and later "hold lifted".
        if (!string.Equals(before.HoldReason, after.HoldReason, StringComparison.Ordinal))
        {
            changes.Add(new TicketChange(TicketChangeKind.Hold, before.HoldReason, after.HoldReason));
        }

        // Compared by id, like the assignee and for the same reason: the tag is display
        // text resolved from another module, and two reads of it are not what decides
        // whether the ticket's link moved.
        if (before.RelatedAssetId != after.RelatedAssetId)
        {
            if (assetTags is not { } tags)
            {
                // The same programming error the priority branch describes: the recorder
                // resolves these before it asks, so reaching here means a new call site
                // forgot to. "The asset changed" without saying which asset looks like
                // coverage and is not.
                throw new ArgumentException(
                    "A change of related asset needs the tags of both assets to describe it.",
                    nameof(assetTags));
            }

            changes.Add(new TicketChange(TicketChangeKind.Asset, tags.From, tags.To));
        }

        return changes;
    }
}
