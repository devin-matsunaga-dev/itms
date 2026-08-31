import { describe, expect, it } from 'vitest'
import { defaultTicketQuery } from './ticket-query'
import { applyView, isViewActive, ticketViews, viewFilters } from './ticket-views'

const me = '11111111-1111-1111-1111-111111111111'
const technician = { currentUserId: me, worksTheQueue: true }
const endUser = { currentUserId: me, worksTheQueue: false }

describe('the built-in views', () => {
  it('offers exactly the three WP-1.9 names', () => {
    expect(ticketViews.map((view) => view.id)).toEqual(['mine', 'unassigned', 'overdue'])
  })
})

describe('"My tickets"', () => {
  it('means the ones assigned to a technician', () => {
    expect(viewFilters('mine', technician)).toMatchObject({ assigneeId: me, requesterId: null })
  })

  it('means the ones an end user raised, since they hold no assignments', () => {
    // The same words and the same usefulness across roles, rather than a preset that is
    // permanently empty for one of the three.
    expect(viewFilters('mine', endUser)).toMatchObject({ requesterId: me, assigneeId: null })
  })

  it('clears the other side of the filter when the role changes what it means', () => {
    const asTechnician = applyView(defaultTicketQuery, 'mine', technician)
    expect(asTechnician.requesterId).toBeNull()

    const asEndUser = applyView(defaultTicketQuery, 'mine', endUser)
    expect(asEndUser.assigneeId).toBeNull()
  })
})

describe('"Unassigned"', () => {
  it('asks the unassigned question rather than filtering on an absent assignee', () => {
    const query = applyView({ ...defaultTicketQuery, assigneeId: me }, 'unassigned', technician)

    expect(query.unassigned).toBe(true)
    expect(query.assigneeId).toBeNull()
  })
})

describe('"Overdue"', () => {
  it('is the breached resolution SLA the API already filters on', () => {
    expect(applyView(defaultTicketQuery, 'overdue', technician).slaState).toBe('Breached')
  })
})

describe('applyView', () => {
  it('returns to the first page, like any other filter change', () => {
    const query = applyView({ ...defaultTicketQuery, page: 7 }, 'overdue', technician)

    expect(query.page).toBe(1)
  })

  it('keeps the ordering somebody chose', () => {
    const query = applyView(
      { ...defaultTicketQuery, sort: 'Number', direction: 'Descending' },
      'overdue',
      technician,
    )

    expect(query.sort).toBe('Number')
    expect(query.direction).toBe('Descending')
  })
})

describe('isViewActive', () => {
  it('reads the URL rather than any state of its own', () => {
    expect(isViewActive(defaultTicketQuery, 'overdue', technician)).toBe(false)
    expect(
      isViewActive({ ...defaultTicketQuery, slaState: 'Breached' }, 'overdue', technician),
    ).toBe(true)
  })

  it('stays active when the view is narrowed further', () => {
    // Somebody who picks "My tickets" and then filters to Critical is still looking at
    // their tickets; the chip going dark would say otherwise.
    const query = applyView(defaultTicketQuery, 'mine', technician)

    expect(isViewActive({ ...query, priorityId: 'critical' }, 'mine', technician)).toBe(true)
  })

  it('recognises two views at once', () => {
    const query = applyView(applyView(defaultTicketQuery, 'mine', technician), 'overdue', technician)

    expect(isViewActive(query, 'mine', technician)).toBe(true)
    expect(isViewActive(query, 'overdue', technician)).toBe(true)
  })

  it('does not read a technician’s view as active for an end user', () => {
    const query = applyView(defaultTicketQuery, 'mine', technician)

    expect(isViewActive(query, 'mine', endUser)).toBe(false)
  })
})
