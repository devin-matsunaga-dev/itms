/**
 * The four KPI tiles, and the queues they open.
 *
 * Each tile's link is the *same filter the server counted with* — that is what makes the
 * figure and the screen behind it agree, and `TicketCountersTests` asserts the pairing
 * server-side for all four. Writing the link by hand anywhere else is how a tile saying
 * six comes to open a list showing five.
 */

import type { TicketCounters } from '@/lib/api/types'
import { defaultDirection, defaultPageSize, defaultSort } from './ticket-query'

/**
 * The four statuses that mean a ticket is still being worked.
 *
 * The same set `TicketCountersHandler.OpenStatuses` counts. Two spellings of one rule, in
 * two languages, which is unavoidable — the integration suite is what keeps them honest,
 * by asserting the Open counter equals the total of the list this produces.
 */
export const openStatuses = ['New', 'Assigned', 'InProgress', 'Waiting'] as const

export interface KpiTile {
  readonly id: keyof Pick<TicketCounters, 'open' | 'unassigned' | 'overdue' | 'dueToday'>
  readonly label: string
  readonly tint: string
  readonly tone: string
  /** The query string its list lives at, without the leading `?`. */
  readonly query: string
}

/** The ordering and the paging every tile's link carries, so it lands on a sane queue. */
const view = `sort=${defaultSort}&direction=${defaultDirection}&pageSize=${String(defaultPageSize)}`

const open = openStatuses.map((status) => `status=${status}`).join('&')

/**
 * The tiles, in the order DESIGN.md §4 tints them: open, unassigned, overdue, and the
 * day's own deadline.
 *
 * @param dayEnd The viewer's end of day as an ISO instant — what "due today" means to
 * the person reading it.
 */
export function kpiTiles(dayEnd: string): readonly KpiTile[] {
  return [
    {
      id: 'open',
      label: 'Open',
      tint: 'bg-primary-soft',
      tone: 'text-primary',
      query: `${open}&${view}`,
    },
    {
      id: 'unassigned',
      label: 'Unassigned',
      tint: 'bg-tint-unassigned',
      tone: 'text-info',
      query: `${open}&unassigned=true&${view}`,
    },
    {
      id: 'overdue',
      label: 'Overdue',
      tint: 'bg-tint-overdue',
      tone: 'text-danger',
      query: `slaState=Breached&${view}`,
    },
    {
      id: 'dueToday',
      label: 'Due today',
      tint: 'bg-primary-soft',
      tone: 'text-primary',
      query: `dueBefore=${encodeURIComponent(dayEnd)}&${view}`,
    },
  ]
}

/**
 * The last instant of the viewer's own day, as the wire wants it.
 *
 * The server counts against whatever it is sent, deliberately: a day boundary is a fact
 * about where somebody is standing, and the wire is UTC (ARCHITECTURE.md §11). The same
 * call WP-1.9 made for the created-date filters.
 */
export function endOfLocalDay(now: Date): string {
  const end = new Date(now)
  end.setHours(23, 59, 59, 999)
  return end.toISOString()
}
