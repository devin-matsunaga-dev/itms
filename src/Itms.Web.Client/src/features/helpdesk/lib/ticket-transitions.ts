/**
 * Which transition buttons a ticket offers, derived from what the server said.
 *
 * WP-1.10's criterion is that illegal transitions are *not rendered*, and WP-1.3 settled
 * how that stays true: the buttons come from `allowedNextStatuses`, which
 * `TicketStateMachine.DestinationsFrom` fills, so the table is never written a second
 * time in TypeScript. Nothing here decides whether a move is legal — it decides only how
 * a legal one is worded.
 *
 * ## The two destinations that are not buttons
 *
 * Two of the seven statuses can appear in that list without being transitions a person
 * presses, and both are recorded traps rather than oversights:
 *
 * - **`New`** is in the list from `Assigned`, because WP-1.6 made `Assigned → New` a real
 *   edge so that unassigning writes its history line like every other move. It is the
 *   *unassign* operation, and the status endpoint refuses it outright with
 *   `helpdesk.unassign_to_return_to_new`. It belongs to the assignee control.
 * - **`Assigned`** is in the list from `New`, because the entity's mover holds that edge
 *   open so every downstream state is reachable. The endpoint refuses it too: a ticket
 *   becomes assigned by being given to somebody, so the status and the assignee arrive in
 *   one call (WP-1.6).
 *
 * Rendering either would put a button on screen that answers 409 every time it is
 * pressed.
 */

import type { TicketStatus } from '@/lib/api/types'

/** One transition, as a button. */
export interface TransitionAction {
  /** The destination to send to `POST /tickets/{id}/status-changes`. */
  readonly status: TicketStatus
  /** The verb on the button. Sentence case, per DESIGN.md §2. */
  readonly label: string
  /** True when the server requires non-blank resolution notes with the move (WP-1.3). */
  readonly requiresNotes: boolean
  /** True when the move is worth confirming before it is made. */
  readonly confirms: boolean
  /** True for the one destructive action, which DESIGN.md §4 paints in `danger`. */
  readonly destructive: boolean
}

/**
 * The order transitions are offered in: forward through the workflow, then the way out.
 * `New` and `Assigned` are absent by construction — see the note above.
 */
const offered: readonly TicketStatus[] = ['InProgress', 'Waiting', 'Resolved', 'Closed', 'Cancelled']

/**
 * The transition buttons for a ticket.
 *
 * @param from The status the ticket is in now, which decides the wording — moving to
 * `InProgress` is starting work, resuming, or reopening depending on where it came from.
 * @param allowed `allowedNextStatuses`, exactly as the server sent it.
 * @returns The buttons to render, in order. Empty from a terminal state.
 */
export function transitionActions(
  from: TicketStatus,
  allowed: readonly TicketStatus[] | undefined,
): TransitionAction[] {
  const destinations = new Set(allowed ?? [])

  return offered
    .filter((status) => destinations.has(status))
    .map((status) => ({
      status,
      label: label(from, status),
      requiresNotes: status === 'Resolved',
      confirms: status === 'Cancelled' || status === 'Closed',
      destructive: status === 'Cancelled',
    }))
}

function label(from: TicketStatus, to: TicketStatus): string {
  switch (to) {
    case 'InProgress':
      // The same destination is three different acts, and the ticket's current state is
      // what tells them apart — which is why the request names the destination and not
      // the transition (WP-1.3).
      return from === 'Waiting' ? 'Resume' : from === 'Resolved' ? 'Reopen' : 'Start work'
    case 'Waiting':
      return 'Put on hold'
    case 'Resolved':
      return 'Resolve'
    case 'Closed':
      return 'Close'
    case 'Cancelled':
      return 'Cancel ticket'
    default:
      return to
  }
}
