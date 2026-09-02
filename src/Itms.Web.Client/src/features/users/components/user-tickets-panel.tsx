import { Link } from 'react-router'
import type { LucideIcon } from 'lucide-react'
import { Panel } from '@/components/common/panel'
import { Skeleton } from '@/components/ui/skeleton'
import type { TicketStatus, TicketSummary } from '@/lib/api/types'
import { formatDateTime, formatRelative } from '@/lib/datetime'
import { StatusPill } from '@/features/helpdesk/components/status-pill'
import { statusOrder } from '@/features/helpdesk/lib/ticket-display'

interface UserTicketsPanelProps {
  icon: LucideIcon
  title: string
  tickets: readonly TicketSummary[]
  /** How many the server says there are, so the panel can say what it is not showing. */
  total: number
  loading: boolean
  failed: boolean
  /** What to say when this person has none of this kind. */
  emptyMessage: string
  /** Where "View all" goes: the queue, filtered to the tickets they raised. */
  queueHref: string
  /** The instant relative times are measured against, threaded from the page. */
  now: Date
}

/**
 * One half of somebody's support history (SPEC.md §4, WP-2.5).
 *
 * Rendered twice on the user page — the open tickets above the previous ones — from the
 * two complementary reads `state=Open` and `state=Past`. They are complementary by
 * construction server-side, so the pair is the whole history and nothing appears in both;
 * this component holds no notion of which statuses are open, which is Helpdesk's business
 * and deliberately never restated here.
 *
 * **The status pill is imported from the helpdesk feature rather than reimplemented**, the
 * call WP-2.6a made for the asset detail: a ticket status is Helpdesk's fact and DESIGN.md
 * §2 fixes one hue per status across the whole product.
 *
 * The panel shows the first page and says how many more there are; "View all" goes to the
 * queue filtered to this requester, which is where a hundred tickets are read.
 */
export function UserTicketsPanel({
  icon,
  title,
  tickets,
  total,
  loading,
  failed,
  emptyMessage,
  queueHref,
  now,
}: UserTicketsPanelProps): React.JSX.Element {
  return (
    <Panel
      icon={icon}
      title={title}
      action={
        total === 0 ? undefined : (
          <Link
            to={queueHref}
            className="rounded-sm text-cell font-medium text-primary transition-colors hover:text-primary-hover focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
          >
            View all
          </Link>
        )
      }
    >
      {loading ? (
        <div className="flex flex-col gap-4" aria-busy="true">
          <span className="sr-only">Loading tickets…</span>
          {[0, 1, 2].map((row) => (
            <div key={row} className="flex flex-col gap-1.5">
              <Skeleton className="h-3 w-24" />
              <Skeleton className="h-4 w-full" />
            </div>
          ))}
        </div>
      ) : failed ? (
        <p role="alert" className="text-copy text-body">
          These tickets could not be loaded.
        </p>
      ) : tickets.length === 0 ? (
        <p className="text-copy text-body">{emptyMessage}</p>
      ) : (
        <>
          <ul aria-label={title} className="flex flex-col">
            {tickets.map((ticket, index) => (
              <li
                key={ticket.id}
                className={index === 0 ? 'py-3 first:pt-0' : 'border-t border-border py-3'}
              >
                <div className="flex items-start justify-between gap-3">
                  <Link
                    to={`/tickets/${ticket.id}`}
                    className="rounded-sm text-cell font-semibold text-primary transition-colors hover:text-primary-hover focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
                  >
                    {ticket.number}
                  </Link>
                  <TicketStatusPill status={ticket.status} />
                </div>
                <p className="mt-1 text-cell text-heading">{ticket.subject}</p>
                <p className="text-caption text-muted-foreground" title={formatDateTime(ticket.createdAt)}>
                  Raised {formatRelative(ticket.createdAt, now)}
                </p>
              </li>
            ))}
          </ul>

          {total > tickets.length ? (
            <p className="mt-3 border-t border-border pt-3 text-caption text-muted-foreground">
              Showing the {tickets.length} most recent of {total}.
            </p>
          ) : null}
        </>
      )}
    </Panel>
  )
}

/**
 * A ticket's status, narrowed from the string the contract carries.
 *
 * `TicketSummary.status` is a plain string — WP-2.5 kept the enum out of `Itms.Contracts`
 * on purpose — so it is checked against the known set rather than cast blindly, and a
 * value the design system does not name renders in the muted unmapped treatment rather
 * than as somebody else's colour.
 *
 * **The second copy of this wrapper**, the first being `asset-tickets-panel.tsx`'s. Two is
 * where this repository leaves a shape alone; the third is the one that hoists it, and the
 * trigger is recorded in STATUS.md.
 */
function TicketStatusPill({ status }: { status: string }): React.JSX.Element {
  if ((statusOrder as readonly string[]).includes(status)) {
    return <StatusPill status={status as TicketStatus} />
  }

  return (
    <span className="inline-flex items-center gap-1.5 rounded-md bg-muted-foreground/12 px-2 py-0.5 text-label font-semibold text-heading dark:bg-muted-foreground/15">
      <span className="size-1.5 shrink-0 rounded-full bg-muted-foreground" aria-hidden="true" />
      {status}
    </span>
  )
}
