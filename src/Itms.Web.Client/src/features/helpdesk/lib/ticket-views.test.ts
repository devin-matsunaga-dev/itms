import { describe, expect, it } from 'vitest'
import { applyMyTickets, clearMyTickets, isMyTickets, myTicketsFilters } from './ticket-views'
import { defaultTicketQuery, type TicketQuery } from './ticket-query'

const me = '11111111-1111-1111-1111-111111111111'
const staff = { currentUserId: me, worksTheQueue: true }
const endUser = { currentUserId: me, worksTheQueue: false }

const query = (overrides: Partial<TicketQuery> = {}): TicketQuery => ({
  ...defaultTicketQuery,
  ...overrides,
})

describe('myTicketsFilters', () => {
  it('means "assigned to me" for somebody who works the queue', () => {
    expect(myTicketsFilters(staff)).toMatchObject({ assigneeId: me, requesterId: null })
  })

  it('means "raised by me" for an end user, who holds nothing', () => {
    // Same words, same usefulness — rather than a preset permanently empty for one role.
    expect(myTicketsFilters(endUser)).toMatchObject({ requesterId: me, assigneeId: null })
  })

  it('clears the unassigned question either way, because one null cannot mean both', () => {
    expect(myTicketsFilters(staff).unassigned).toBe(false)
    expect(myTicketsFilters(endUser).unassigned).toBe(false)
  })
})

describe('applyMyTickets', () => {
  it('returns to the first page, because page four of a different question may not exist', () => {
    expect(applyMyTickets(query({ page: 4 }), staff).page).toBe(1)
  })

  it('leaves the ordering and the page size alone', () => {
    const narrowed = applyMyTickets(query({ sort: 'DueAt', pageSize: 100 }), staff)

    expect(narrowed.sort).toBe('DueAt')
    expect(narrowed.pageSize).toBe(100)
  })

  it('keeps the other filters, so it narrows rather than replaces', () => {
    expect(applyMyTickets(query({ priorityId: 'pri-high' }), staff).priorityId).toBe('pri-high')
  })
})

describe('clearMyTickets', () => {
  it('takes the view off and leaves everything else standing', () => {
    const on = applyMyTickets(query({ priorityId: 'pri-high' }), staff)
    const off = clearMyTickets(on, staff)

    expect(off.assigneeId).toBeNull()
    expect(off.priorityId).toBe('pri-high')
  })

  it('clears the field that role actually filters on', () => {
    const on = applyMyTickets(query(), endUser)

    expect(clearMyTickets(on, endUser).requesterId).toBeNull()
  })
})

describe('isMyTickets', () => {
  it('is false for an untouched queue', () => {
    expect(isMyTickets(query(), staff)).toBe(false)
  })

  it('is true once the address says what the view would say', () => {
    expect(isMyTickets(applyMyTickets(query(), staff), staff)).toBe(true)
  })

  it('stays true when the view is narrowed further', () => {
    // A subset test, not an equality one: somebody who picked their own tickets and then
    // narrowed to Critical is still looking at their tickets.
    const narrowed = { ...applyMyTickets(query(), staff), priorityId: 'pri-critical' }

    expect(isMyTickets(narrowed, staff)).toBe(true)
  })

  it('is false when somebody else’s tickets are being shown', () => {
    expect(isMyTickets(query({ assigneeId: 'somebody-else' }), staff)).toBe(false)
  })

  it('reads the field that role filters on, not the other', () => {
    // A technician's own queue is an assignee filter; an end user's is a requester one.
    const theirs = applyMyTickets(query(), endUser)

    expect(isMyTickets(theirs, endUser)).toBe(true)
    expect(isMyTickets(theirs, staff)).toBe(false)
  })
})
