/**
 * The "My tickets" filter — the one saved view the queue still has.
 *
 * WP-1.9 shipped three chips: My tickets, Unassigned, Overdue. WP-1.12 then gave the
 * screen a KPI row whose Unassigned and Overdue tiles link to *exactly* those two
 * queries, so two of the three chips were a second way to ask a question already on
 * screen, one row above. They are gone; this is what is left, and it lives in the filter
 * bar rather than on a row of its own.
 *
 * It is still a preset rather than a fourth concept: it writes filter parameters somebody
 * could have set by hand, so it is an ordinary linkable URL with no server state, nothing
 * per-account, and nothing that can disagree with the filter bar beside it.
 */

import type { TicketQuery } from './ticket-query'
import { withFilters } from './ticket-query'

/** Who is asking, which is what "mine" means. */
export interface ViewerOptions {
  readonly currentUserId: string
  /** True for a Technician or an Admin. */
  readonly worksTheQueue: boolean
}

/**
 * What "my tickets" means for the person looking at it.
 *
 * Role-sensitive on purpose. A Technician or an Admin works a queue, so theirs are the
 * ones assigned to them; an end user has no assignments at all, so theirs are the ones
 * they raised. Same words, same usefulness, rather than a preset that is permanently
 * empty for one of the three roles.
 */
export function myTicketsFilters(options: ViewerOptions): Partial<TicketQuery> {
  return options.worksTheQueue
    ? { assigneeId: options.currentUserId, unassigned: false, requesterId: null }
    : { requesterId: options.currentUserId, assigneeId: null, unassigned: false }
}

/** The query "my tickets" produces from where the queue currently stands. */
export function applyMyTickets(query: TicketQuery, options: ViewerOptions): TicketQuery {
  return withFilters(query, myTicketsFilters(options))
}

/** The query with "my tickets" taken back off, leaving every other filter alone. */
export function clearMyTickets(query: TicketQuery, options: ViewerOptions): TicketQuery {
  return withFilters(query, options.worksTheQueue ? { assigneeId: null } : { requesterId: null })
}

/**
 * True when the address already says what "my tickets" would say.
 *
 * A subset test rather than an equality one: somebody who picked their own tickets and
 * then narrowed to Critical is still looking at their tickets, and the control going dark
 * would say otherwise.
 */
export function isMyTickets(query: TicketQuery, options: ViewerOptions): boolean {
  const filters = myTicketsFilters(options)

  return (Object.keys(filters) as (keyof TicketQuery)[]).every(
    (key) => query[key] === filters[key],
  )
}
