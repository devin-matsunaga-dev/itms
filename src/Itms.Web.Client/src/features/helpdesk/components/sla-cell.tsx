import { cn } from '@/lib/utils'
import type { TicketSla } from '@/lib/api/types'
import { formatDateTime } from '@/lib/datetime'
import { slaMeter } from '../lib/sla-meter'

interface SlaCellProps {
  sla: TicketSla
  /** The instant to measure against — passed in so the whole table agrees on "now". */
  now: Date
  className?: string
}

/**
 * Where a ticket's resolution clock stands: a meter over the time left, or how far past.
 *
 * The bar is the quick read down a column of forty rows; the words underneath are what
 * makes it legible without colour, which DESIGN.md §6 requires. The absolute deadline is
 * on the cell's `title`, per §6's rule that a relative value keeps its absolute one
 * within reach.
 *
 * A paused ticket says so. WP-1.8 freezes the deadline for the length of a Waiting
 * period, so a bar that kept filling — or a countdown that kept counting — would be a
 * lie about a ticket nobody is able to work on.
 *
 * The arithmetic is `sla-meter.ts`, which is where it can be tested.
 */
export function SlaCell({ sla, now, className }: SlaCellProps): React.JSX.Element {
  const meter = slaMeter(sla, now)

  return (
    <div
      className={cn('flex w-32 flex-col gap-1.5', className)}
      title={`Due ${formatDateTime(sla.resolutionDueAt)}`}
    >
      <div
        className="h-1.5 w-full overflow-hidden rounded-full bg-border"
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.round(meter.fraction * 100)}
        aria-label={`Resolution SLA: ${meter.label}`}
      >
        <div
          className={cn('h-full rounded-full transition-[width] duration-150', meter.bar)}
          style={{ width: `${String(meter.fraction * 100)}%` }}
        />
      </div>

      <span className="flex items-baseline gap-1.5 text-caption tabular">
        {/* The hue lives in the bar, never in the letterforms: `warning` reaches about
            1.9:1 as text and `danger` about 3.8:1, both under §6's AA floor. This is the
            third place that conflict has come up, after the status and priority pills. */}
        <span
          className={cn(
            'font-medium',
            meter.remaining === null ? 'text-muted-foreground' : 'text-heading',
          )}
        >
          {meter.remaining ?? meter.label}
        </span>
        {meter.paused ? <span className="text-muted-foreground">· paused</span> : null}
      </span>
    </div>
  )
}
