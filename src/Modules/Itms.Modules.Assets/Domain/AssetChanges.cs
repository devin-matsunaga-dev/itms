namespace Itms.Modules.Assets.Domain;

/// <summary>
/// Works out which history entries a lifecycle operation owes, by comparing the asset
/// before it with the asset after it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is one function rather than a line in each handler.</b> Invariant 5 says
/// assigning, transferring, repairing, returning to service and retiring each write an
/// asset-history entry. A handler that decides for itself which entries its own operation
/// produces is a handler that can be written, reviewed, and merged having quietly produced
/// none. Here they get their entries by comparing two snapshots, and the only thing each
/// one has to remember is to take the first.
/// </para>
/// <para>
/// <b>It is also what makes WP-2.2's done-criterion true by construction.</b> A transfer
/// between two people moves the holder and nothing else, so exactly one entry is owed,
/// carrying both parties. A first assignment out of stock moves the holder <em>and</em>
/// the lifecycle status, so two are — which is correct, and is the same shape assigning a
/// New ticket has produced since WP-1.4.
/// </para>
/// <para>
/// Pure, and it touches no database, which is what lets the unit suite exhaust it.
/// </para>
/// </remarks>
public static class AssetChanges
{
    /// <summary>
    /// The entries owed for the move from <paramref name="before"/> to
    /// <paramref name="after"/>, in the order a timeline reads them.
    /// </summary>
    /// <remarks>
    /// A dimension that did not move produces nothing, which is what keeps an operation
    /// that writes back every field from making the timeline look like a rewrite of the
    /// whole asset — the same rule ARCHITECTURE.md §8 sets for the audit diff.
    /// Assignment comes first because it is the fact an operator is looking for: "who has
    /// it" is the question an asset register is asked, and the lifecycle move is the
    /// consequence.
    /// </remarks>
    /// <param name="before">The asset as it stood before the operation.</param>
    /// <param name="after">The asset as it stands now.</param>
    /// <returns>The entries owed, possibly none.</returns>
    public static IReadOnlyList<AssetChange> Between(AssetSnapshot before, AssetSnapshot after)
    {
        var changes = new List<AssetChange>(capacity: 2);

        // Compared by id, not by name: two people can share a display name, and a transfer
        // between them is still a transfer.
        if (before.AssignedToUserId != after.AssignedToUserId)
        {
            changes.Add(new AssetChange(
                AssetChangeKind.Assignment,
                before.AssignedToUserName,
                after.AssignedToUserName));
        }

        // By id for the same reason, but recorded as the names both statuses read at the
        // time. A status that is later renamed must not silently rewrite what the timeline
        // claims happened.
        if (before.StatusId != after.StatusId)
        {
            changes.Add(new AssetChange(AssetChangeKind.Status, before.StatusName, after.StatusName));
        }

        return changes;
    }
}
