import type { LucideIcon } from 'lucide-react'
import { Link } from 'react-router'
import { cn } from '@/lib/utils'
import { Skeleton } from '@/components/ui/skeleton'

interface KpiCardProps {
  /** The uppercase label under the figure — "OPEN", "OVERDUE". */
  label: string
  /** The figure itself, or null while it is still being counted. */
  value: number | null
  icon: LucideIcon
  /** The soft tile behind the icon. DESIGN.md §4 fixes one per card. */
  tint: string
  /** The icon's own hue. */
  tone: string
  /** Where the figure's own list lives. A KPI nobody can open is a number, not a link. */
  to: string
  /**
   * The tighter treatment, for a KPI row that sits above a working list rather than on a
   * dashboard of its own (DESIGN.md §4).
   *
   * The dashboard's row is the screen; the queue's row is a summary above the thing
   * somebody came to read, and every pixel it takes is a ticket they cannot see. Same
   * card, same tints, same figures — 40px tile, 18px icon, 24px figure, tighter padding.
   */
  dense?: boolean
}

/**
 * DESIGN.md §4's KPI card: a soft-tinted tile holding the icon, then the uppercase label
 * and the figure.
 *
 * **There is no delta line.** §4 describes one, and the queue's mockup asks for it — but
 * the schema cannot answer "how many were open this time last week" honestly, for the
 * reasons `TicketCountersHandler` sets out at length: a reopen clears `resolvedAt`, and a
 * cancellation records no instant at all. A number that looked like a trend and was not
 * would be worse than none. When a counters snapshot or a history replay exists, the line
 * goes here and this comment goes away.
 *
 * The whole card is a link, because a figure somebody cannot open is a figure they have
 * to go and reproduce by hand.
 */
export function KpiCard({
  label,
  value,
  icon: Icon,
  tint,
  tone,
  to,
  dense = false,
}: KpiCardProps): React.JSX.Element {
  return (
    <Link
      to={to}
      className={cn(
        'flex items-center rounded-card border border-border bg-surface shadow-card transition-shadow duration-150 hover:shadow-card-hover focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none',
        dense ? 'gap-3 p-4' : 'gap-4 p-5',
      )}
    >
      <span
        className={cn(
          'flex shrink-0 items-center justify-center rounded-tile',
          dense ? 'size-10' : 'size-12',
          tint,
        )}
      >
        <Icon className={cn(dense ? 'size-[18px]' : 'size-[22px]', tone)} aria-hidden="true" />
      </span>

      <span className="min-w-0">
        {value === null ? (
          <Skeleton className={dense ? 'h-7 w-10' : 'h-8 w-12'} />
        ) : (
          <span
            className={cn(
              'block font-bold text-heading tabular',
              dense ? 'text-kpi-dense' : 'text-kpi',
            )}
          >
            {value}
          </span>
        )}
        <span className="block text-label font-semibold text-muted-foreground uppercase">
          {label}
        </span>
      </span>
    </Link>
  )
}
