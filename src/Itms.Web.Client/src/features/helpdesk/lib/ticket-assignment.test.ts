import { describe, expect, it } from 'vitest'
import type { UserSummary } from '@/lib/api/types'
import { assigneeOptions, canChangeAssignee, canUnassign, unassignedValue } from './ticket-assignment'
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
})
