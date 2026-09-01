import { CircleAlert, Clock, Inbox, UserX } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { KpiCard } from '@/components/common/kpi-card'
import type { TicketCounters } from '@/lib/api/types'
import { kpiTiles } from '../lib/ticket-kpis'

const icons: Record<string, LucideIcon> = {
  open: Inbox,
  unassigned: UserX,
  overdue: CircleAlert,
  dueToday: Clock,
}

interface TicketKpiRowProps {
  /** The counters, or null while they load. */
  counters: TicketCounters | null
  /** The viewer's end of day, so "due today" links where it counted. */
  dayEnd: string
}

/**
 * The queue's four headline figures (DESIGN.md §2's KPI row: 4 × 3 columns).
 *
 * Scope-wide and unaffected by the filter bar below them, by decision — they say what is
 * waiting, not what is on screen. Each one links to the list the server counted, so a
 * figure is always one click from the rows behind it.
 */
export function TicketKpiRow({ counters, dayEnd }: TicketKpiRowProps): React.JSX.Element {
  return (
    <div
      role="group"
      aria-label="Queue summary"
      className="grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-4"
    >
      {kpiTiles(dayEnd).map((tile) => (
        <KpiCard
          key={tile.id}
          label={tile.label}
          value={counters === null ? null : counters[tile.id]}
          icon={icons[tile.id] ?? Inbox}
          tint={tile.tint}
          tone={tile.tone}
          to={`/tickets?${tile.query}`}
          // Above a working list: every pixel here is a ticket somebody cannot see.
          dense
        />
      ))}
    </div>
  )
}
