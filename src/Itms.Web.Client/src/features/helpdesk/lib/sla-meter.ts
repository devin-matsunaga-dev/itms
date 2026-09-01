/**
 * A ticket's resolution clock as a meter: how much of the target is spent, and what to
 * say about it.
 *
 * Pure, and separate from the cell that draws it, because the arithmetic is the part that
 * can be wrong. Three things make it less obvious than "elapsed over total":
 *
 * 1. **The deadline moves.** WP-1.8 pushes `resolutionDueAt` forward by the length of
 *    every pause rather than accumulating a debt, so the *remaining* time is honest at any
 *    instant but the original target is not recoverable from the due date alone. The
 *    target minutes are what the span is measured against.
 * 2. **A parked clock is judged at the instant it was parked.** A Waiting ticket's
 *    deadline is frozen, and a meter that kept filling would say a ticket was running out
 *    of time while nobody is able to work on it.
 * 3. **A finished clock is full, not overflowing.** Met, Stopped, and a resolved ticket
 *    have nothing left to count down.
 *
 * The hue comes from the state the *server* computed (`SlaAssessment` in memory,
 * `TicketSlaFilter` in SQL — WP-1.8 keeps the two honest with a test). Nothing here
 * re-decides whether a ticket is breached; it only decides how wide the bar is.
 */

import type { SlaState, TicketSla } from '@/lib/api/types'
import { parseTimestamp } from '@/lib/datetime'

/** What the meter should draw and say. */
export interface SlaMeter {
  /** How much of the target is spent, clamped to 0–1. A breach pins it at 1. */
  readonly fraction: number
  /** The bar's fill class, from the semantic map. */
  readonly bar: string
  /** The state as a word — DESIGN.md §6: never signalled by colour alone. */
  readonly label: string
  /** `42m left`, `Overdue 1h`, or null when the clock has stopped. */
  readonly remaining: string | null
  /** True while the resolution clock is parked in Waiting. */
  readonly paused: boolean
}

/** DESIGN.md §2's semantic map, for the meter's fill. */
const bars: Record<SlaState, string> = {
  Pending: 'bg-success',
  Approaching: 'bg-warning',
  Breached: 'bg-danger',
  Met: 'bg-success',
  Stopped: 'bg-neutral-chart',
}

const labels: Record<SlaState, string> = {
  Pending: 'On track',
  Approaching: 'Due soon',
  Breached: 'Overdue',
  Met: 'Met',
  Stopped: 'Stopped',
}

/** The states whose clock is still running, and so still worth a countdown. */
const counting: ReadonlySet<SlaState> = new Set<SlaState>(['Pending', 'Approaching', 'Breached'])

export function slaMeter(sla: TicketSla, now: Date): SlaMeter {
  const state = sla.resolutionState ?? 'Pending'
  const paused = sla.isPaused === true
  const due = parseTimestamp(sla.resolutionDueAt)

  // A parked clock is read at the instant it was parked; a running one at `now`.
  const at = paused ? (parseTimestamp(sla.pausedAt) ?? now) : now

  const remainingMs = due === null ? null : due.getTime() - at.getTime()
  const targetMs = sla.resolutionTargetMinutes * 60_000

  return {
    fraction: fraction(state, remainingMs, targetMs),
    bar: bars[state],
    label: labels[state],
    remaining: counting.has(state) ? describe(remainingMs) : null,
    paused,
  }
}

function fraction(state: SlaState, remainingMs: number | null, targetMs: number): number {
  // A clock that has stopped has run its course, whatever the arithmetic says about a
  // deadline that no longer applies.
  if (!counting.has(state)) {
    return 1
  }

  if (remainingMs === null || targetMs <= 0) {
    return 0
  }

  const spent = targetMs - remainingMs
  return Math.min(1, Math.max(0, spent / targetMs))
}

function describe(remainingMs: number | null): string | null {
  if (remainingMs === null) {
    return null
  }

  return remainingMs >= 0 ? `${span(remainingMs)} left` : `Overdue ${span(-remainingMs)}`
}

/**
 * A coarse span — `42m`, `6h`, `2d`.
 *
 * Deliberately one unit rather than `lib/datetime`'s `4h 20m`: this sits under a 13.5px
 * table cell beside eight other columns, and the minute inside a two-day span is noise.
 * The exact deadline is on the cell's `title`.
 */
function span(ms: number): string {
  const minutes = Math.floor(ms / 60_000)
  if (minutes < 60) {
    return `${String(Math.max(minutes, 1))}m`
  }

  const hours = Math.floor(minutes / 60)
  if (hours < 24) {
    return `${String(hours)}h`
  }

  return `${String(Math.floor(hours / 24))}d`
}
