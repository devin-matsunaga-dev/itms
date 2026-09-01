/**
 * Who a ticket can be handed to, and when it can be handed back.
 *
 * Assignment is its own route (WP-1.6) rather than a status change, and two of its rules
 * decide what the control may even offer. They live here, pure, because "the option was
 * not there" is a claim worth asserting directly rather than through an opened popup.
 *
 * None of this is enforcement. The server refuses every one of these cases on its own —
 * `helpdesk.ticket_not_assignable`, `helpdesk.cannot_unassign` — and would refuse a
 * hand-crafted request too. Withholding the option is the courtesy of not offering
 * somebody an action that answers 409.
 */

import type { TicketDetail, UserSummary } from '@/lib/api/types'
import { hasAnyRole, Roles } from '@/lib/roles'

/**
 * Whether a person can be given a ticket.
 *
 * `AssignTicketHandler` refuses anybody else with 400 `helpdesk.assignee_not_technician`
 * (WP-1.6), reading the same role list off `UserSummary`. Offering an end user in the
 * picker meant handing somebody a choice the server was always going to reject — the
 * picker and the rule now read the same field.
 *
 * Admin counts as well as Technician, matching `TicketScope.SeesEveryTicket`: an
 * administrator working the queue is somebody the queue can be given to.
 *
 * This is not the enforcement. The server refuses a hand-crafted request either way.
 */
export function canHoldTickets(user: UserSummary): boolean {
  return hasAnyRole(user.roles, [Roles.admin, Roles.technician])
}

/** The "nobody holds it" option. An empty string is a legitimate id, so it is a sentinel. */
export const unassignedValue = '__unassigned__'

/** One entry in the assignee picker. */
export interface AssigneeOption {
  readonly value: string
  readonly label: string
}

/**
 * Whether the assignee can be changed at all.
 *
 * A Closed or Cancelled ticket has no work left to hand anybody, and an end user reads
 * their own ticket without handing it to anyone.
 */
export function canChangeAssignee(ticket: TicketDetail, worksTheQueue: boolean): boolean {
  return worksTheQueue && ticket.status !== 'Closed' && ticket.status !== 'Cancelled'
}

/**
 * Whether the ticket can be dropped back on the queue.
 *
 * WP-1.6 allows unassignment only from `Assigned`: past that, work has started and the
 * ticket belongs to somebody until it is handed on — the answer to "this is not mine" is
 * to reassign it, not to abandon it with its history intact and its owner gone.
 */
export function canUnassign(ticket: TicketDetail): boolean {
  return ticket.status === 'Assigned' || ticket.assigneeId === null
}

/**
 * The picker's options, in the order it offers them.
 *
 * Only people who can actually hold a ticket are listed — see {@link canHoldTickets}.
 *
 * The current holder is prepended when the directory no longer lists them — a technician
 * who has since been deactivated, or whose role was removed, is out of the picker and
 * still holding the ticket, and a select whose value names no item renders blank.
 */
export function assigneeOptions(
  ticket: TicketDetail,
  assignees: readonly UserSummary[],
): AssigneeOption[] {
  const options: AssigneeOption[] = [
    ...(canUnassign(ticket) ? [{ value: unassignedValue, label: 'Unassigned' }] : []),
    ...assignees
      .filter(canHoldTickets)
      .map((user) => ({ value: user.id, label: user.displayName })),
  ]

  if (
    ticket.assigneeId !== null &&
    ticket.assigneeId !== undefined &&
    !options.some((option) => option.value === ticket.assigneeId)
  ) {
    options.unshift({ value: ticket.assigneeId, label: ticket.assigneeName ?? 'Unknown' })
  }

  return options
}
