/**
 * The three built-in queue views WP-1.9 names: My tickets, Unassigned, Overdue.
 *
 * They are presets, not a fourth concept: each one writes the filter parameters somebody
 * could have set by hand, so a view is an ordinary linkable URL and there is no server
 * state, no per-account storage, and nothing that can disagree with the filter bar. A
 * view reads as selected when the URL already says what it would say.
 */

import type { TicketQuery } from './ticket-query'
import { withFilters } from './ticket-query'

/** The identifiers the chips are keyed and tested by. */
export type TicketViewId = 'mine' | 'unassigned' | 'overdue'

export interface TicketView {
  readonly id: TicketViewId
  readonly label: string
  /** What the chip promises, for its tooltip and its accessible description. */
  readonly description: string
}

export const ticketViews: readonly TicketView[] = [
  { id: 'mine', label: 'My tickets', description: 'Tickets that are your responsibility.' },
  { id: 'unassigned', label: 'Unassigned', description: 'Tickets nobody is holding yet.' },
  { id: 'overdue', label: 'Overdue', description: 'Tickets past their resolution target.' },
]

/**
 * What a view means for the person looking at it.
 *
 * "My tickets" is role-sensitive on purpose. A Technician or an Admin works a queue, so
 * theirs are the ones assigned to them; an end user has no assignments at all, so theirs
 * are the ones they raised. Same words, same usefulness, rather than a preset that is
 * permanently empty for one of the three roles.
 */
export function viewFilters(
  view: TicketViewId,
  options: { currentUserId: string; worksTheQueue: boolean },
): Partial<TicketQuery> {
  switch (view) {
    case 'mine':
      return options.worksTheQueue
        ? { assigneeId: options.currentUserId, unassigned: false, requesterId: null }
        : { requesterId: options.currentUserId, assigneeId: null, unassigned: false }

    case 'unassigned':
      return { unassigned: true, assigneeId: null, requesterId: null }

    case 'overdue':
      return { slaState: 'Breached' }
  }
}

/** The query a view produces from where the queue currently stands. */
export function applyView(
  query: TicketQuery,
  view: TicketViewId,
  options: { currentUserId: string; worksTheQueue: boolean },
): TicketQuery {
  return withFilters(query, viewFilters(view, options))
}

/**
 * True when the queue already satisfies a view.
 *
 * Deliberately a subset test rather than an equality test: somebody who picks "My
 * tickets" and then narrows it to Critical is still looking at their tickets, and the
 * chip going dark at that point would say otherwise.
 */
export function isViewActive(
  query: TicketQuery,
  view: TicketViewId,
  options: { currentUserId: string; worksTheQueue: boolean },
): boolean {
  const wanted = viewFilters(view, options)

  return Object.entries(wanted).every(([key, value]) => {
    if (value === null || value === false) {
      return true
    }
    return query[key as keyof TicketQuery] === value
  })
}
