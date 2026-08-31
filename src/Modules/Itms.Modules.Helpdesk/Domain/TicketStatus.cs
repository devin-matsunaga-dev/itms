namespace Itms.Modules.Helpdesk.Domain;

/// <summary>
/// Where a ticket sits in the workflow SPEC.md §2 defines.
/// </summary>
/// <remarks>
/// <para>
/// This package declares the states because invariant 1 says a ticket always has one and
/// the column has to hold something. It deliberately declares no transitions: which move
/// is legal from which state, and the 409 an illegal one earns, is WP-1.3's state machine
/// and belongs on <see cref="Ticket"/>, not in an enum.
/// </para>
/// <para>
/// Stored as text rather than as an integer, following the call WP-0.6 made for
/// <c>LocationKind</c>: a ticket row is read at a psql prompt during an incident far more
/// often than an enum is renumbered.
/// </para>
/// </remarks>
public enum TicketStatus
{
    /// <summary>Raised and not yet picked up. Every ticket starts here.</summary>
    New,

    /// <summary>Given to a technician, who has not started on it.</summary>
    Assigned,

    /// <summary>Being worked.</summary>
    InProgress,

    /// <summary>Blocked on somebody else. WP-1.8 pauses the resolution clock here.</summary>
    Waiting,

    /// <summary>Fixed, pending the requester's acceptance. Reopening returns it to <see cref="InProgress"/>.</summary>
    Resolved,

    /// <summary>Finished. Terminal.</summary>
    Closed,

    /// <summary>Abandoned before resolution. Reachable from any pre-<see cref="Resolved"/> state.</summary>
    Cancelled,
}
