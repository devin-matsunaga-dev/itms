import { cn } from '@/lib/utils'
import type { TicketSla } from '@/lib/api/types'
import { formatDateTime, formatDuration, parseTimestamp } from '@/lib/datetime'
import { slaLabels, slaTones } from '../lib/ticket-display'

interface SlaCellProps {
  sla: TicketSla
  /** The instant to measure against — passed in so the whole table agrees on "now". */
  now: Date
  className?: string
}

/**
 * Where a ticket's resolution clock stands: a pill naming the state, and how long is
 * left or how far past it is.
 *
 * The state is a word as well as a hue — DESIGN.md §6 wants AA contrast, and a queue
 * that says "overdue" only in red says nothing to somebody who cannot see red. The
 * absolute deadline is on the element's title, per §6's rule that a relative value
 * always keeps its absolute one within reach.
 *
 * A paused ticket is called out on its own line: the deadline is frozen while it sits in
 * Waiting (WP-1.8), so a countdown that kept running would be a lie.
 */
export function SlaCell({ sla, now, className }: SlaCellProps): React.JSX.Element {
  const state = sla.resolutionState ?? 'Pending'
  const tone = slaTones[state]
  const due = parseTimestamp(sla.resolutionDueAt)

  // A stopped or met clock has nothing left to count down; what matters is that it
  // finished, which the pill already says.
  const counting = state === 'Pending' || state === 'Approaching' || state === 'Breached'
  const remaining = due === null ? null : due.getTime() - now.getTime()

  return (
    <span
      className={cn('inline-flex flex-col items-end gap-1', className)}
      title={due === null ? undefined : `Due ${formatDateTime(due)}`}
    >
      <span
        className={cn(
          'inline-flex items-center gap-1.5 rounded-md px-2 py-0.5 text-label font-semibold text-heading',
          tone.fill,
        )}
      >
        <span className={cn('size-1.5 shrink-0 rounded-full', tone.dot)} aria-hidden="true" />
        {slaLabels[state]}
      </span>

      {counting && remaining !== null ? (
        <span className="tabular text-caption text-muted-foreground">
          {remaining >= 0
            ? `${formatDuration(remaining)} left`
            : `${formatDuration(-remaining)} over`}
        </span>
      ) : null}

      {sla.isPaused === true ? (
        <span className="text-caption text-muted-foreground">Paused</span>
      ) : null}
    </span>
  )
}
