import { Pause } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { TicketDetail } from '@/lib/api/types'
import { formatDateTime, formatDuration, parseTimestamp } from '@/lib/datetime'
import { slaLabels, slaTones } from '../lib/ticket-display'
import { PriorityLabel } from './priority-pill'
import { StatusPill } from './status-pill'

interface TicketDetailHeaderProps {
  ticket: TicketDetail
  /** The instant to measure the clocks against, threaded from the page. */
  now: Date
}

/**
 * The ticket's state at a glance: status, priority, and both SLA clocks as pills, over
 * what the requester reported and — once there is one — the resolution.
 *
 * The pills follow the treatment `ticket-display.ts` sets out and the queue already
 * uses: the hue is carried by the fill and a dot, with the label in `heading`. DESIGN.md
 * §4 asks for the label itself at full hue and §6 makes AA contrast on status pills
 * non-negotiable in both schemes; several of the semantic hues cannot do both, and §6 is
 * the one labelled non-negotiable. The queue and the detail must agree regardless — the
 * same status being two different treatments on two screens would be worse than either.
 */
export function TicketDetailHeader({ ticket, now }: TicketDetailHeaderProps): React.JSX.Element {
  return (
    <section className="rounded-card border border-border bg-surface p-5 shadow-card">
      <div className="flex flex-wrap items-center gap-x-5 gap-y-3">
        <StatusPill status={ticket.status} />
        <PriorityLabel code={ticket.priorityCode} name={ticket.priorityName} />

        <SlaPill
          label="Response"
          state={ticket.sla.responseState ?? 'Pending'}
          dueAt={ticket.sla.responseDueAt}
          now={now}
        />
        <SlaPill
          label="Resolution"
          state={ticket.sla.resolutionState ?? 'Pending'}
          dueAt={ticket.sla.resolutionDueAt}
          now={now}
        />

        {ticket.sla.isPaused === true ? (
          <span
            className="inline-flex items-center gap-1.5 text-caption text-muted-foreground"
            title="The resolution clock is parked while the ticket is Waiting."
          >
            <Pause className="size-3.5" aria-hidden="true" />
            Clock paused
          </span>
        ) : null}
      </div>

      <div className="mt-5 border-t border-border pt-5">
        <h2 className="text-label font-semibold text-muted-foreground uppercase">Description</h2>
        {/* Plain text from the requester. React escapes it; nothing here is rendered as
            markup, which is what keeps a ticket body from becoming stored XSS. */}
        <p className="mt-2 text-copy whitespace-pre-wrap text-body">{ticket.description}</p>
      </div>

      {ticket.holdReason === null || ticket.holdReason === undefined ? null : (
        <div className="mt-5 rounded-tile bg-teal/12 p-4 dark:bg-teal/15">
          <h2 className="flex items-center gap-1.5 text-label font-semibold text-muted-foreground uppercase">
            <Pause className="size-3.5" aria-hidden="true" />
            On hold
          </h2>
          <p className="mt-2 text-copy whitespace-pre-wrap text-body">{ticket.holdReason}</p>
        </div>
      )}

      {ticket.resolutionNotes === null || ticket.resolutionNotes === undefined ? null : (
        <div className="mt-5 rounded-tile bg-violet/12 p-4 dark:bg-violet/15">
          <h2 className="text-label font-semibold text-muted-foreground uppercase">
            Resolution
            {ticket.resolvedAt === null || ticket.resolvedAt === undefined
              ? null
              : ` · ${formatDateTime(ticket.resolvedAt)}`}
          </h2>
          <p className="mt-2 text-copy whitespace-pre-wrap text-body">{ticket.resolutionNotes}</p>
        </div>
      )}
    </section>
  )
}

interface SlaPillProps {
  label: string
  state: NonNullable<TicketDetail['sla']['resolutionState']>
  dueAt: string
  now: Date
}

/** One SLA clock: what it is, where it stands, and how long is left or how far over. */
function SlaPill({ label, state, dueAt, now }: SlaPillProps): React.JSX.Element {
  const tone = slaTones[state]
  const due = parseTimestamp(dueAt)
  const counting = state === 'Pending' || state === 'Approaching' || state === 'Breached'
  const remaining = due === null ? null : due.getTime() - now.getTime()

  return (
    <span className="inline-flex items-center gap-2" title={due === null ? undefined : `Due ${formatDateTime(due)}`}>
      <span className="text-label font-semibold text-muted-foreground uppercase">{label}</span>
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
          {remaining >= 0 ? `${formatDuration(remaining)} left` : `${formatDuration(-remaining)} over`}
        </span>
      ) : null}
    </span>
  )
}
