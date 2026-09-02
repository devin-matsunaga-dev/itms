import { Link } from 'react-router'
import { LifeBuoy } from 'lucide-react'
import { Panel } from '@/components/common/panel'
import { Skeleton } from '@/components/ui/skeleton'
import type { TicketStatus, TicketSummary } from '@/lib/api/types'
import { formatDateTime, formatRelative } from '@/lib/datetime'
import { StatusPill } from '@/features/helpdesk/components/status-pill'
import { statusOrder } from '@/features/helpdesk/lib/ticket-display'

interface AssetTicketsPanelProps {
  tickets: readonly TicketSummary[]
  /** How many the server says there are, so the panel can say what it is not showing. */
  total: number
  loading: boolean
  failed: boolean
  /** The instant relative times are measured against, threaded from the page. */
  now: Date
}

/**
 * The tickets raised about this asset (WP-2.5).
 *
 * Every linked ticket, whatever its status and whoever raised it — at the human's
 * direction, because an asset's support history is the whole story of that machine and
 * the route is Technician-only anyway. Open tickets come first by virtue of the server's
 * newest-first ordering being what it is; the pill is what says which are still live.
 *
 * **The status pill is imported from the helpdesk feature rather than reimplemented.**
 * A ticket status is Helpdesk's fact and DESIGN.md §2 fixes one hue per status across the
 * whole product; a second copy of that map living in the assets feature is exactly how
 * two screens end up disagreeing about what "Waiting" looks like.
 *
 * There is no "View all" link, because there is nothing to link to: the ticket queue has
 * no filter for the asset a ticket relates to. If one is ever added, this header is where
 * the link belongs.
 */
export function AssetTicketsPanel({
  tickets,
  total,
  loading,
  failed,
  now,
}: AssetTicketsPanelProps): React.JSX.Element {
  return (
    <Panel icon={LifeBuoy} title="Support history">
      {loading ? (
        <div className="flex flex-col gap-4" aria-busy="true">
          <span className="sr-only">Loading the support history…</span>
          {[0, 1, 2].map((row) => (
            <div key={row} className="flex flex-col gap-1.5">
              <Skeleton className="h-3 w-24" />
              <Skeleton className="h-4 w-full" />
            </div>
          ))}
        </div>
      ) : failed ? (
        <p role="alert" className="text-copy text-body">
          The support history could not be loaded.
        </p>
      ) : tickets.length === 0 ? (
        <p className="text-copy text-body">No tickets have been raised about this asset.</p>
      ) : (
        <>
          <ul aria-label="Tickets about this asset" className="flex flex-col">
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
                <p
                  className="text-caption text-muted-foreground"
                  title={formatDateTime(ticket.createdAt)}
                >
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
 * `TicketSummary.status` is a plain string on the wire, deliberately: WP-2.5 kept the
 * `TicketStatus` enum inside Helpdesk rather than putting it in `Itms.Contracts`, so a
 * consumer reads the workflow's *value* without depending on its shape. That is the right
 * boundary and it has a price here — this screen has to check the value before it can
 * colour it.
 *
 * A value the workflow does not name is rendered as itself, in the muted treatment an
 * unmapped priority gets: it reads as unmapped rather than as somebody else's status,
 * and the row still says which ticket it is.
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
