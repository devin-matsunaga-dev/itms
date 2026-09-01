import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { TicketDetail, UserSummary } from '@/lib/api/types'
import { assigneeOptions, canChangeAssignee, unassignedValue } from '../lib/ticket-assignment'

interface TicketAssigneeControlProps {
  ticket: TicketDetail
  /** Technicians and admins, from the Technician-guarded picker endpoint. */
  assignees: readonly UserSummary[]
  /** False for an end user, who reads their own ticket but hands it to nobody. */
  canAssign: boolean
  busy: boolean
  onAssign: (assigneeId: string | null) => void
}

/**
 * Who holds the ticket, and the control that changes it.
 *
 * Assignment is not a status change and does not go through the transition buttons: it is
 * its own route (WP-1.6), and `Assigned → New` — unassignment — is the reason `New` turns
 * up in `allowedNextStatuses` while never being a status button. This is the control that
 * walks that edge.
 *
 * Which options exist, and whether the control exists at all, is `ticket-assignment.ts`.
 */
export function TicketAssigneeControl({
  ticket,
  assignees,
  canAssign,
  busy,
  onAssign,
}: TicketAssigneeControlProps): React.JSX.Element {
  const held = ticket.assigneeName ?? 'Unassigned'

  if (!canChangeAssignee(ticket, canAssign)) {
    return <span className="text-cell text-heading">{held}</span>
  }

  const options = assigneeOptions(ticket, assignees)

  return (
    <Select
      items={options}
      value={ticket.assigneeId ?? unassignedValue}
      disabled={busy}
      onValueChange={(next: string | null) => {
        const chosen = next ?? unassignedValue
        if (chosen === (ticket.assigneeId ?? unassignedValue)) {
          return
        }
        onAssign(chosen === unassignedValue ? null : chosen)
      }}
    >
      <SelectTrigger id="ticket-assignee" size="default" className="w-full">
        <SelectValue placeholder="Unassigned" />
      </SelectTrigger>
      <SelectContent>
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
