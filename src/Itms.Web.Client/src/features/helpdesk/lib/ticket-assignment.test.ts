import { describe, expect, it } from 'vitest'
import type { UserSummary } from '@/lib/api/types'
import {
  assigneeOptions,
  canChangeAssignee,
  canHoldTickets,
  canUnassign,
  unassignedValue,
} from './ticket-assignment'
import { ticketDetail, technicianId } from '../test/ticket-fixtures'

const mark: UserSummary = {
  id: technicianId,
  displayName: 'Mark Reyes',
  email: 'tech@itms.local',
  departmentId: null,
  locationId: null,
  isActive: true,
  roles: ['Technician'],
}

const uma: UserSummary = {
  id: 'end-user',
  displayName: 'Uma User',
  email: 'user@itms.local',
  departmentId: null,
  locationId: null,
  isActive: true,
  roles: ['User'],
}

const avery: UserSummary = { ...mark, id: 'admin-1', displayName: 'Avery Admin', roles: ['Admin'] }

describe('canHoldTickets', () => {
  it('accepts a technician and an administrator', () => {
    // Admin counts too, matching TicketScope.SeesEveryTicket — somebody working the queue
    // is somebody the queue can be given to.
    expect(canHoldTickets(mark)).toBe(true)
    expect(canHoldTickets(avery)).toBe(true)
  })

  it('refuses an end user, whom the server refuses too', () => {
    // AssignTicketHandler answers 400 helpdesk.assignee_not_technician for exactly this.
    expect(canHoldTickets(uma)).toBe(false)
  })

  it('refuses somebody holding no role at all', () => {
    expect(canHoldTickets({ ...uma, roles: [] })).toBe(false)
  })
})

describe('canChangeAssignee', () => {
  it('is false for an end user, who follows their own ticket and hands it to nobody', () => {
    expect(canChangeAssignee(ticketDetail(), false)).toBe(false)
  })

  it('is false once the ticket is settled, because there is no work left to hand on', () => {
    expect(canChangeAssignee(ticketDetail({ status: 'Closed' }), true)).toBe(false)
    expect(canChangeAssignee(ticketDetail({ status: 'Cancelled' }), true)).toBe(false)
  })

  it('is true for somebody working the queue on a live ticket', () => {
    expect(canChangeAssignee(ticketDetail({ status: 'InProgress' }), true)).toBe(true)
  })
})

describe('canUnassign', () => {
  it('allows dropping a ticket back on the queue only from Assigned', () => {
    expect(canUnassign(ticketDetail({ status: 'Assigned', assigneeId: technicianId }))).toBe(true)
  })

  it('refuses it once work has started — the answer to “not mine” is to reassign', () => {
    for (const status of ['InProgress', 'Waiting', 'Resolved'] as const) {
      expect(canUnassign(ticketDetail({ status, assigneeId: technicianId }))).toBe(false)
    }
  })

  it('leaves the option on a ticket nobody holds, where it is the current value', () => {
    expect(canUnassign(ticketDetail({ status: 'New', assigneeId: null }))).toBe(true)
  })
})

describe('assigneeOptions', () => {
  it('does not offer somebody the server would refuse to assign', () => {
    // The picker used to list every active account, so choosing an end user produced a
    // 400 for a choice the interface had offered.
    const options = assigneeOptions(ticketDetail(), [mark, uma])

    expect(options.map((option) => option.label)).not.toContain('Uma User')
    expect(options.map((option) => option.label)).toContain('Mark Reyes')
  })

  it('offers Unassigned first when the ticket may be handed back', () => {
    const options = assigneeOptions(
      ticketDetail({ status: 'Assigned', assigneeId: technicianId, assigneeName: 'Mark Reyes' }),
      [mark],
    )

    expect(options[0]).toEqual({ value: unassignedValue, label: 'Unassigned' })
  })

  it('withholds it once work has started', () => {
    const options = assigneeOptions(
      ticketDetail({ status: 'InProgress', assigneeId: technicianId, assigneeName: 'Mark Reyes' }),
      [mark],
    )

    expect(options.some((option) => option.value === unassignedValue)).toBe(false)
  })

  it('keeps a holder the directory no longer lists, so the control is never blank', () => {
    const options = assigneeOptions(
      ticketDetail({
        status: 'InProgress',
        assigneeId: 'departed-technician',
        assigneeName: 'Ana Cruz',
      }),
      [mark],
    )

    expect(options[0]).toEqual({ value: 'departed-technician', label: 'Ana Cruz' })
  })

  it('keeps a holder whose staff role was taken away, for the same reason', () => {
    // Filtering the list must not erase the person currently on the ticket.
    const options = assigneeOptions(
      ticketDetail({ status: 'InProgress', assigneeId: uma.id, assigneeName: 'Uma User' }),
      [mark, uma],
    )

    expect(options[0]).toEqual({ value: uma.id, label: 'Uma User' })
  })
})
