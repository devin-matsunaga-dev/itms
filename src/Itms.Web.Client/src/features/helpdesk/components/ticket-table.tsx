import { ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { TicketListItem, TicketSort } from '@/lib/api/types'
import { formatDateTime, formatDuration, parseTimestamp } from '@/lib/datetime'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { PriorityLabel } from './priority-dot'
import { SlaCell } from './sla-cell'
import { StatusPill } from './status-pill'
import type { TicketQuery } from '../lib/ticket-query'

interface TicketTableProps {
  tickets: readonly TicketListItem[]
  query: TicketQuery
  /** The instant every age and countdown in the table is measured against. */
  now: Date
  onSort: (column: TicketSort) => void
  /** What opening a ticket does. WP-1.10 builds the screen this will navigate to. */
  onOpen: (ticket: TicketListItem) => void
}

/**
 * The ticket queue (DESIGN.md §4, *Data table*), and `reference-dashboard.png`'s Open
 * Tickets treatment at full width: 11/600 uppercase headers over a single rule, 44px
 * rows separated by `border` with no zebra striping, a `canvas` tint on hover, the
 * identifier first as a `primary` link, priority as dot + label, status as a pill, and
 * the age and SLA columns right-aligned.
 *
 * The table scrolls horizontally inside its own card rather than reflowing (§6), which
 * is what the primitive's container already does.
 */
export function TicketTable({
  tickets,
  query,
  now,
  onSort,
  onOpen,
}: TicketTableProps): React.JSX.Element {
  return (
    <div className="overflow-hidden rounded-card border border-border bg-surface shadow-card">
      <Table className="text-cell">
        <TableHeader>
          <TableRow className="border-border hover:bg-transparent">
            <SortableHead column="Number" query={query} onSort={onSort}>
              Ticket
            </SortableHead>
            <PlainHead>Subject</PlainHead>
            <PlainHead>Requester</PlainHead>
            <PlainHead>Department</PlainHead>
            <PlainHead>Assignee</PlainHead>
            <SortableHead column="Priority" query={query} onSort={onSort}>
              Priority
            </SortableHead>
            <PlainHead>Status</PlainHead>
            <SortableHead column="CreatedAt" query={query} onSort={onSort} align="end">
              Age
            </SortableHead>
            <SortableHead column="DueAt" query={query} onSort={onSort} align="end">
              Resolution
            </SortableHead>
          </TableRow>
        </TableHeader>

        <TableBody>
          {tickets.map((ticket) => (
            <TableRow
              key={ticket.id}
              // DESIGN.md §4: a row opens the detail page. The keyboard path to the same
              // action is the ticket number, which is a real control.
              onClick={() => {
                onOpen(ticket)
              }}
              className="h-11 cursor-pointer border-border hover:bg-canvas"
            >
              <TableCell className="px-4">
                <button
                  type="button"
                  onClick={(event) => {
                    event.stopPropagation()
                    onOpen(ticket)
                  }}
                  className="rounded-sm font-semibold text-primary transition-colors hover:text-primary-hover focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
                >
                  {ticket.number}
                </button>
              </TableCell>

              <TableCell className="max-w-[22rem] truncate px-4 text-heading" title={ticket.subject}>
                {ticket.subject}
              </TableCell>

              <TableCell className="px-4 text-body">{ticket.requesterName}</TableCell>
              <TableCell className="px-4 text-body">{ticket.departmentName}</TableCell>

              <TableCell className="px-4 text-body">
                {ticket.assigneeName ?? (
                  <span className="text-muted-foreground italic">Unassigned</span>
                )}
              </TableCell>

              <TableCell className="px-4">
                <PriorityLabel code={ticket.priorityCode} name={ticket.priorityName} />
              </TableCell>

              <TableCell className="px-4">
                <StatusPill status={ticket.status} />
              </TableCell>

              <TableCell
                className="px-4 text-right text-muted-foreground tabular"
                title={formatDateTime(ticket.createdAt)}
              >
                {age(ticket.createdAt, now)}
              </TableCell>

              <TableCell className="px-4 text-right">
                <SlaCell sla={ticket.sla} now={now} />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

/** How long ago the ticket was raised — the reference table's AGE column. */
function age(createdAt: string, now: Date): string {
  const created = parseTimestamp(createdAt)
  return created === null ? '—' : formatDuration(now.getTime() - created.getTime())
}

const headClass =
  'h-10 bg-surface px-4 text-label font-semibold tracking-[0.06em] text-muted-foreground uppercase'

function PlainHead({ children }: { children: React.ReactNode }): React.JSX.Element {
  return <TableHead className={headClass}>{children}</TableHead>
}

interface SortableHeadProps {
  column: TicketSort
  query: TicketQuery
  onSort: (column: TicketSort) => void
  align?: 'start' | 'end'
  children: React.ReactNode
}

/**
 * A column header that orders the queue.
 *
 * The sort is server-side and lives in the URL, so the header reports what the address
 * says rather than any state of its own — and `aria-sort` says the same thing to a
 * screen reader that the arrow says to everyone else.
 */
function SortableHead({
  column,
  query,
  onSort,
  align = 'start',
  children,
}: SortableHeadProps): React.JSX.Element {
  const active = query.sort === column
  const ascending = query.direction === 'Ascending'
  const Icon = active ? (ascending ? ArrowUp : ArrowDown) : ChevronsUpDown

  return (
    <TableHead
      className={cn(headClass, align === 'end' && 'text-right')}
      aria-sort={active ? (ascending ? 'ascending' : 'descending') : 'none'}
    >
      <button
        type="button"
        onClick={() => {
          onSort(column)
        }}
        className={cn(
          'inline-flex items-center gap-1.5 rounded-sm tracking-[0.06em] uppercase transition-colors hover:text-heading focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none',
          align === 'end' && 'flex-row-reverse',
          active && 'text-heading',
        )}
      >
        {children}
        <Icon className={cn('size-3', !active && 'opacity-50')} aria-hidden="true" />
      </button>
    </TableHead>
  )
}
