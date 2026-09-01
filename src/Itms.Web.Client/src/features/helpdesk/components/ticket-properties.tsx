import { ClipboardList } from 'lucide-react'
import { Panel } from '@/components/common/panel'
import { Label } from '@/components/ui/label'
import type { TicketDetail, UserSummary } from '@/lib/api/types'
import { formatDateTime, formatDuration } from '@/lib/datetime'
import { canChangeAssignee } from '../lib/ticket-assignment'
import { PriorityLabel } from './priority-dot'
import { TicketAssigneeControl } from './ticket-assignee-control'

interface TicketPropertiesProps {
  ticket: TicketDetail
  assignees: readonly UserSummary[]
  canAssign: boolean
  busy: boolean
  onAssign: (assigneeId: string | null) => void
}

/**
 * The properties panel: who and what the ticket is about, and both SLA clocks in full.
 *
 * The assignee is the one row that is also a control — assignment is its own route and
 * not a status change, so it does not belong with the transition buttons.
 *
 * Every instant is rendered through the shared formatter, in the viewer's own timezone
 * (DESIGN.md §6). The wire is UTC without exception (ARCHITECTURE.md §11); nothing here
 * does arithmetic on a timestamp, so nothing here can get a timezone wrong.
 */
export function TicketProperties({
  ticket,
  assignees,
  canAssign,
  busy,
  onAssign,
}: TicketPropertiesProps): React.JSX.Element {
  const pausedSeconds = ticket.sla.pausedSeconds ?? 0

  return (
    <Panel icon={ClipboardList} title="Details">
      <dl className="flex flex-col gap-4">
        <Row term="Requester">{ticket.requesterName}</Row>
        <Row term="Department">{ticket.departmentName}</Row>
        <Row term="Category">{ticket.categoryName}</Row>
        <Row term="Priority">
          <PriorityLabel code={ticket.priorityCode} name={ticket.priorityName} />
        </Row>

        <div className="flex flex-col gap-1.5">
          {canChangeAssignee(ticket, canAssign) ? (
            <Label htmlFor="ticket-assignee" className="text-label font-semibold text-muted-foreground uppercase">
              Assignee
            </Label>
          ) : (
            <dt className="text-label font-semibold text-muted-foreground uppercase">Assignee</dt>
          )}
          <dd>
            <TicketAssigneeControl
              ticket={ticket}
              assignees={assignees}
              canAssign={canAssign}
              busy={busy}
              onAssign={onAssign}
            />
          </dd>
        </div>

        <hr className="border-border" />

        <Row term="Raised">{formatDateTime(ticket.createdAt)}</Row>
        <Row term="Last updated">{formatDateTime(ticket.updatedAt)}</Row>
        {ticket.resolvedAt === null || ticket.resolvedAt === undefined ? null : (
          <Row term="Resolved">{formatDateTime(ticket.resolvedAt)}</Row>
        )}
        {ticket.closedAt === null || ticket.closedAt === undefined ? null : (
          <Row term="Closed">{formatDateTime(ticket.closedAt)}</Row>
        )}

        <hr className="border-border" />

        <Row term="Response due">
          {formatDateTime(ticket.sla.responseDueAt)}
          <span className="block text-caption text-muted-foreground">
            {`Target ${formatDuration(ticket.sla.responseTargetMinutes * 60_000)}`}
            {ticket.sla.respondedAt === null || ticket.sla.respondedAt === undefined
              ? ''
              : ` · answered ${formatDateTime(ticket.sla.respondedAt)}`}
          </span>
        </Row>
        <Row term="Resolution due">
          {formatDateTime(ticket.sla.resolutionDueAt)}
          <span className="block text-caption text-muted-foreground">
            {`Target ${formatDuration(ticket.sla.resolutionTargetMinutes * 60_000)}`}
            {pausedSeconds > 0 ? ` · paused ${formatDuration(pausedSeconds * 1000)} so far` : ''}
          </span>
        </Row>
      </dl>
    </Panel>
  )
}

function Row({ term, children }: { term: string; children: React.ReactNode }): React.JSX.Element {
  return (
    <div className="flex flex-col gap-1">
      <dt className="text-label font-semibold text-muted-foreground uppercase">{term}</dt>
      <dd className="tabular text-cell text-heading">{children}</dd>
    </div>
  )
}
