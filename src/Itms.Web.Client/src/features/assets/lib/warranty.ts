/**
 * How long an asset's warranty has left, and how that reads.
 *
 * DESIGN.md §4's *expiration list row* fixes the treatment: "in N days" with the hue
 * shifting to `warning` under 30 days and `danger` under 7, over the absolute date in
 * `muted`. This is that rule, kept pure — the thresholds are the part that can be wrong,
 * and an off-by-one is not something a test can read back off a rendered row. It is the
 * same call WP-2.3 made server-side when it put the query's bounds in `WarrantyWindow`
 * rather than inline in the filter.
 *
 * ## A warranty expiry is a calendar date, not an instant
 *
 * The wire carries `warrantyExpiresAt` as a `DateOnly` — `2026-09-01`, with no time and
 * no zone — and WP-2.3 recorded why: unlike a ticket's due time, there is nothing here
 * for a clock to decide. So the string is split into its three numbers and rebuilt in the
 * viewer's own calendar rather than passed to `new Date()`, which would read it as UTC
 * midnight and shift the day for everybody west of Greenwich. "Expires today" has to mean
 * today where the person is standing.
 *
 * ## The server's window and this one agree at both edges
 *
 * `?warrantyExpiringInDays=30` is `today <= expiry <= today + 30`, inclusive at both ends
 * and excluding the already-lapsed (WP-2.3). So a row returned by that filter always has
 * `daysRemaining` between 0 and 30 here, and a row this module calls `expired` is one the
 * filter would never have returned. The two are the same arithmetic on the same calendar.
 */

/** Where a warranty stands, worst first. */
export type WarrantyState = 'expired' | 'critical' | 'soon' | 'covered' | 'none'

export interface Warranty {
  readonly state: WarrantyState
  /**
   * Whole days from today to the expiry, in the viewer's calendar. Zero means it lapses
   * today; negative means it already has. Null when no date was recorded.
   */
  readonly daysRemaining: number | null
  /** The expiry as a local `Date` at midnight, for the shared formatter. Null when none. */
  readonly expiresAt: Date | null
}

/** DESIGN.md §4: `danger` under 7 days. */
export const criticalDays = 7

/** DESIGN.md §4: `warning` under 30 days. */
export const soonDays = 30

/**
 * Reads a warranty date against a reference instant.
 *
 * @param value The `DateOnly` the API sent, or null when none was recorded.
 * @param now The instant to measure from — threaded from the screen so every row in a
 * table agrees on what "in 12 days" means, the same way the queue threads `now`.
 */
export function readWarranty(value: string | null | undefined, now: Date): Warranty {
  const expiresAt = parseDateOnly(value)
  if (expiresAt === null) {
    // No date recorded is not "expiring imminently" and not "covered" — it is silence,
    // and the server's filters treat it the same way: `warrantyExpiringInDays` never
    // matches it, and `warrantyExpired=false` does.
    return { state: 'none', daysRemaining: null, expiresAt: null }
  }

  const today = startOfLocalDay(now)
  const daysRemaining = Math.round((expiresAt.getTime() - today.getTime()) / 86_400_000)

  return { state: stateFor(daysRemaining), daysRemaining, expiresAt }
}

function stateFor(daysRemaining: number): WarrantyState {
  if (daysRemaining < 0) {
    return 'expired'
  }
  if (daysRemaining < criticalDays) {
    return 'critical'
  }
  if (daysRemaining < soonDays) {
    return 'soon'
  }
  return 'covered'
}

/**
 * How the remaining time reads: "in 12 days", "today", "3 days ago".
 *
 * Sentence case and no abbreviation, unlike the queue's `3h ago` — a warranty is read
 * once in a column of six rows, not scanned down a queue of forty.
 */
export function warrantyLabel(warranty: Warranty): string {
  const { daysRemaining } = warranty
  if (daysRemaining === null) {
    return 'No warranty recorded'
  }
  if (daysRemaining === 0) {
    return 'Expires today'
  }
  if (daysRemaining === 1) {
    return 'in 1 day'
  }
  if (daysRemaining > 1) {
    return `in ${String(daysRemaining)} days`
  }

  const elapsed = -daysRemaining
  return elapsed === 1 ? 'Expired yesterday' : `Expired ${String(elapsed)} days ago`
}

/** The text colour for each state (DESIGN.md §4). */
export function warrantyTone(state: WarrantyState): string {
  switch (state) {
    case 'expired':
    case 'critical':
      return 'text-danger'
    case 'soon':
      return 'text-warning'
    case 'covered':
      return 'text-body'
    case 'none':
      return 'text-muted-foreground'
  }
}

/**
 * `2026-09-01` as local midnight on that calendar day.
 *
 * Deliberately not `new Date(value)`: that reads a bare date as UTC, so `2026-09-01`
 * becomes the 31st of August for anybody behind Greenwich and every countdown on the
 * screen is a day out. Returns null on anything that is not exactly a `DateOnly`.
 */
export function parseDateOnly(value: string | null | undefined): Date | null {
  if (value === null || value === undefined) {
    return null
  }

  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value)
  if (match === null) {
    return null
  }

  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]))
  return Number.isNaN(date.getTime()) ? null : date
}

function startOfLocalDay(now: Date): Date {
  return new Date(now.getFullYear(), now.getMonth(), now.getDate())
}
