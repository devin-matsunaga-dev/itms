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
}: KpiCardProps): React.JSX.Element {
  return (
    <Link
      to={to}
      className="flex items-center gap-4 rounded-card border border-border bg-surface p-5 shadow-card transition-shadow duration-150 hover:shadow-card-hover focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
    >
      <span className={cn('flex size-12 shrink-0 items-center justify-center rounded-tile', tint)}>
        <Icon className={cn('size-[22px]', tone)} aria-hidden="true" />
      </span>

      <span className="min-w-0">
        {value === null ? (
          <Skeleton className="h-8 w-12" />
        ) : (
          <span className="block text-kpi font-bold text-heading tabular">{value}</span>
        )}
        <span className="block text-label font-semibold text-muted-foreground uppercase">
          {label}
        </span>
      </span>
    </Link>
  )
}
