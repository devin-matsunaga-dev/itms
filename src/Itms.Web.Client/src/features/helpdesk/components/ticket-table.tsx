import { ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { TicketListItem, TicketSort } from '@/lib/api/types'
import { formatDateTime, formatRelative } from '@/lib/datetime'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { PriorityLabel } from './priority-pill'
import { PersonCell } from './person-cell'
import { SlaCell } from './sla-cell'
import { StatusPill } from './status-pill'
import { isVisible, type TicketColumnId, type TicketTablePreferences } from '../lib/ticket-columns'
import type { TicketQuery } from '../lib/ticket-query'

interface TicketTableProps {
  tickets: readonly TicketListItem[]
  query: TicketQuery
  /** The instant every age and countdown in the table is measured against. */
  now: Date
  preferences: TicketTablePreferences
  onSort: (column: TicketSort) => void
  onOpen: (ticket: TicketListItem) => void
}

/**
 * The ticket queue (DESIGN.md §4, *Data table*).
 *
 * The identifying column is two lines — the ticket number as a `primary` link over the
 * subject, with a caption saying how long ago it was raised. That is what buys the rest
 * of the row its space: the subject no longer competes with eight other columns for
 * width, and the age stops needing a column of its own at the far end where nobody reads
 * it.
 *
 * Which of the remaining columns are drawn, and how tall a row is, come from the
 * reader's own stored preferences rather than from the URL — `ticket-columns.ts` says
 * why. Everything that decides *which rows* is still in the address.
 *
 * The table scrolls horizontally inside its own card rather than reflowing (§6), which is
 * what the primitive's container already does.
 */
export function TicketTable({
  tickets,
  query,
  now,
  preferences,
  onSort,
  onOpen,
}: TicketTableProps): React.JSX.Element {
  const shows = (id: TicketColumnId): boolean => isVisible(preferences, id)
  const compact = preferences.density === 'compact'
  const cell = compact ? 'px-4 py-1.5' : 'px-4 py-3'

  return (
    <div className="overflow-hidden rounded-card border border-border bg-surface shadow-card">
      <Table className="text-cell">
        <TableHeader>
          <TableRow className="border-border hover:bg-transparent">
            <SortableHead column="Number" query={query} onSort={onSort}>
              Ticket
            </SortableHead>
            {shows('status') ? <PlainHead>Status</PlainHead> : null}
            {shows('priority') ? (
              <SortableHead column="Priority" query={query} onSort={onSort}>
                Priority
              </SortableHead>
            ) : null}
            {shows('requester') ? <PlainHead>Requester</PlainHead> : null}
            {shows('assignee') ? <PlainHead>Assignee</PlainHead> : null}
            {shows('department') ? <PlainHead>Department</PlainHead> : null}
            {shows('category') ? <PlainHead>Category</PlainHead> : null}
            {shows('created') ? (
              <SortableHead column="CreatedAt" query={query} onSort={onSort}>
                Age
              </SortableHead>
            ) : null}
            {shows('sla') ? (
              <SortableHead column="DueAt" query={query} onSort={onSort}>
                SLA
              </SortableHead>
            ) : null}
            {shows('updated') ? (
              <SortableHead column="UpdatedAt" query={query} onSort={onSort} align="end">
                Updated
              </SortableHead>
            ) : null}
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
              className="cursor-pointer border-border hover:bg-canvas"
            >
              <TableCell className={cn(cell, 'max-w-[26rem]')}>
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
                <p className="truncate text-cell text-heading" title={ticket.subject}>
                  {ticket.subject}
                </p>
                {compact ? null : (
                  <p className="text-caption text-muted-foreground">
                    Created {formatRelative(ticket.createdAt, now)}
                  </p>
                )}
              </TableCell>

              {shows('status') ? (
                <TableCell className={cell}>
                  <StatusPill status={ticket.status} />
                </TableCell>
              ) : null}

              {shows('priority') ? (
                <TableCell className={cell}>
                  <PriorityLabel code={ticket.priorityCode} name={ticket.priorityName} />
                </TableCell>
              ) : null}

              {shows('requester') ? (
                <TableCell className={cell}>
                  <PersonCell name={ticket.requesterName} />
                </TableCell>
              ) : null}

              {shows('assignee') ? (
                <TableCell className={cell}>
                  <PersonCell name={ticket.assigneeName} absent="Unassigned" />
                </TableCell>
              ) : null}

              {shows('department') ? (
                <TableCell className={cn(cell, 'text-body')}>{ticket.departmentName}</TableCell>
              ) : null}

              {shows('category') ? (
                <TableCell className={cn(cell, 'text-body')}>{ticket.categoryName}</TableCell>
              ) : null}

              {shows('created') ? (
                <TableCell
                  className={cn(cell, 'text-muted-foreground tabular')}
                  title={formatDateTime(ticket.createdAt)}
                >
                  {formatRelative(ticket.createdAt, now)}
                </TableCell>
              ) : null}

              {shows('sla') ? (
                <TableCell className={cell}>
                  <SlaCell sla={ticket.sla} now={now} />
                </TableCell>
              ) : null}

              {shows('updated') ? (
                <TableCell
                  className={cn(cell, 'text-right text-muted-foreground tabular')}
                  title={formatDateTime(ticket.updatedAt)}
                >
                  {formatRelative(ticket.updatedAt, now)}
                </TableCell>
              ) : null}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
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
 * screen reader that the arrow says to everyone else. The toolbar's sort select is the
 * other door to the same two parameters.
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
