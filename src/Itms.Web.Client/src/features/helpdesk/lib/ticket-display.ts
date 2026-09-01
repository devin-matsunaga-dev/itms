/**
 * How a ticket's status, priority, and SLA are rendered — the one place the semantic
 * colour map from DESIGN.md §2 is spelled out.
 *
 * The map is fixed across the whole product: the same status is the same hue on the
 * queue, on the detail page, and in a chart. Nothing here builds a class name by
 * concatenation, because Tailwind only emits classes it can see written down.
 *
 * ## Where this departs from DESIGN.md §4, and why
 *
 * §4 describes a status pill as "soft background at ~12% of the semantic hue, text at
 * full hue". §6 makes WCAG AA contrast on status pills non-negotiable in both colour
 * schemes — and several of the hues cannot carry 11px text at AA against a 12% wash of
 * themselves: `warning` (#F5B22D) reaches about 1.8:1 and `neutral-chart` (#CBD5E1) is
 * worse. A pill nobody can read is not a lighter version of a pill.
 *
 * So a pill here is: the soft fill at the hue, a 6px dot in the *full* hue, and the
 * label in `heading`. The hue is still what identifies the status — it is simply
 * carried by the fill and the dot rather than by the letterforms. §6 is labelled
 * non-negotiable and §4 is a pattern description, which is the order they were read in.
 * The same reasoning applies to the SLA column: the state is named in words and marked
 * with a pill, never signalled by the colour of the text alone.
 */

import type { SlaState, TicketStatus } from '@/lib/api/types'

/** Sentence-case names for the seven statuses. `InProgress` is two words to a person. */
export const statusLabels: Record<TicketStatus, string> = {
  New: 'New',
  Assigned: 'Assigned',
  InProgress: 'In progress',
  Waiting: 'Waiting',
  Resolved: 'Resolved',
  Closed: 'Closed',
  Cancelled: 'Cancelled',
}

/** The order the status filter offers them in — the workflow's own order. */
export const statusOrder: readonly TicketStatus[] = [
  'New',
  'Assigned',
  'InProgress',
  'Waiting',
  'Resolved',
  'Closed',
  'Cancelled',
]

interface Tone {
  /** The soft pill fill: 12% of the hue in light, 15% in dark (DESIGN.md §5). */
  readonly fill: string
  /** The full hue, worn by the dot. */
  readonly dot: string
}

/**
 * DESIGN.md §2's ticket-status map. `Assigned` is the one status the document does not
 * name a colour for — the dashboard donut it was written against groups five states —
 * so it takes `info`, the adjacent blue: acknowledged, but not yet started.
 */
export const statusTones: Record<TicketStatus, Tone> = {
  New: { fill: 'bg-primary/12 dark:bg-primary/15', dot: 'bg-primary' },
  Assigned: { fill: 'bg-info/12 dark:bg-info/15', dot: 'bg-info' },
  InProgress: { fill: 'bg-warning/12 dark:bg-warning/15', dot: 'bg-warning' },
  Waiting: { fill: 'bg-teal/12 dark:bg-teal/15', dot: 'bg-teal' },
  Resolved: { fill: 'bg-violet/12 dark:bg-violet/15', dot: 'bg-violet' },
  Closed: { fill: 'bg-neutral-chart/25 dark:bg-neutral-chart/30', dot: 'bg-neutral-chart' },
  Cancelled: {
    fill: 'bg-muted-foreground/12 dark:bg-muted-foreground/15',
    dot: 'bg-muted-foreground',
  },
}

/**
 * DESIGN.md §2's priority map, keyed on the priority's immutable `code` (WP-1.1) rather
 * than its name, so an administrator renaming "High" to "Urgent" does not repaint it.
 *
 * A priority an administrator adds beyond the seeded four has no colour in the design
 * system and takes `muted` — a hue nothing else claims, so an unmapped priority reads as
 * unmapped rather than as somebody else's severity.
 */
const priorityDots: Record<string, string> = {
  critical: 'bg-critical',
  high: 'bg-danger',
  medium: 'bg-warning',
  low: 'bg-success',
}

/** The dot colour for a priority code. */
export function priorityDot(code: string): string {
  return priorityDots[code.toLowerCase()] ?? 'bg-muted-foreground'
}

/**
 * The pill treatment for a priority: a soft fill and an arrow in the full hue.
 *
 * The arrow is a second encoding of the same fact, which DESIGN.md §6 wants — a queue
 * that says "critical" only in red says nothing to somebody who cannot see red, and the
 * label alone cannot carry the hue at AA (the reasoning at the top of this file). The
 * direction reads as severity: two chevrons up for Critical, one for High, a dash for
 * Medium, one down for Low.
 */
export type PriorityArrow = 'up-double' | 'up' | 'flat' | 'down'

export interface PriorityTone extends Tone {
  readonly arrow: PriorityArrow
  /** The arrow's colour — the full hue, which only a 14px glyph has to carry. */
  readonly icon: string
}

const priorityTones: Record<string, PriorityTone> = {
  critical: {
    fill: 'bg-critical/12 dark:bg-critical/15',
    dot: 'bg-critical',
    icon: 'text-critical',
    arrow: 'up-double',
  },
  high: {
    fill: 'bg-danger/12 dark:bg-danger/15',
    dot: 'bg-danger',
    icon: 'text-danger',
    arrow: 'up',
  },
  medium: {
    fill: 'bg-warning/12 dark:bg-warning/15',
    dot: 'bg-warning',
    icon: 'text-warning',
    arrow: 'flat',
  },
  low: {
    fill: 'bg-success/12 dark:bg-success/15',
    dot: 'bg-success',
    icon: 'text-success',
    arrow: 'down',
  },
}

/**
 * The pill treatment for a priority code.
 *
 * Keyed on the immutable `code` (WP-1.1) rather than the name, so renaming "High" moves
 * the word and not the hue. A priority an administrator adds beyond the seeded four has
 * no colour in the design system and takes `muted` with a flat arrow — it reads as
 * unmapped rather than as somebody else's severity.
 */
export function priorityTone(code: string): PriorityTone {
  return (
    priorityTones[code.toLowerCase()] ?? {
      fill: 'bg-muted-foreground/12 dark:bg-muted-foreground/15',
      dot: 'bg-muted-foreground',
      icon: 'text-muted-foreground',
      arrow: 'flat',
    }
  )
}

/** What each SLA state is called on screen. */
export const slaLabels: Record<SlaState, string> = {
  Pending: 'On track',
  Approaching: 'Due soon',
  Breached: 'Overdue',
  Met: 'Met',
  Stopped: 'Stopped',
}

/** The pill treatment for each SLA state. */
export const slaTones: Record<SlaState, Tone> = {
  Pending: {
    fill: 'bg-muted-foreground/12 dark:bg-muted-foreground/15',
    dot: 'bg-muted-foreground',
  },
  Approaching: { fill: 'bg-warning/12 dark:bg-warning/15', dot: 'bg-warning' },
  Breached: { fill: 'bg-danger/12 dark:bg-danger/15', dot: 'bg-danger' },
  Met: { fill: 'bg-success/12 dark:bg-success/15', dot: 'bg-success' },
  Stopped: {
    fill: 'bg-muted-foreground/12 dark:bg-muted-foreground/15',
    dot: 'bg-muted-foreground',
  },
}

/**
 * The SLA states worth filtering a queue by, in the order a person triaging one would
 * reach for them. All five the API accepts are offered — "Met" and "Stopped" answer
 * "what has already left the queue", which is a real question about a closed month.
 */
export const slaStateOrder: readonly SlaState[] = [
  'Breached',
  'Approaching',
  'Pending',
  'Met',
  'Stopped',
]
