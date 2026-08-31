import { describe, expect, it } from 'vitest'
import {
  clearedFilters,
  dayEnd,
  dayStart,
  defaultDirection,
  defaultSort,
  defaultTicketQuery,
  hasActiveFilters,
  parseTicketQuery,
  sameTicketQuery,
  serializeTicketQuery,
  toDateInput,
  withFilters,
} from './ticket-query'

describe('parseTicketQuery', () => {
  it('gives a bare address the queue ordering, not the API default', () => {
    const query = parseTicketQuery(new URLSearchParams())

    expect(query.sort).toBe(defaultSort)
    expect(query.direction).toBe(defaultDirection)
    expect(query.page).toBe(1)
  })

  it('reads every filter the endpoint accepts', () => {
    const query = parseTicketQuery(
      new URLSearchParams(
        'status=New&status=InProgress&priorityId=p1&categoryId=c1&assigneeId=a1' +
          '&departmentId=d1&requesterId=r1&createdFrom=2026-01-01T00:00:00.000Z' +
          '&createdTo=2026-02-01T00:00:00.000Z&slaState=Breached&sort=DueAt' +
          '&direction=Descending&page=3&pageSize=50',
      ),
    )

    expect(query.status).toEqual(['New', 'InProgress'])
    expect(query.priorityId).toBe('p1')
    expect(query.categoryId).toBe('c1')
    expect(query.assigneeId).toBe('a1')
    expect(query.departmentId).toBe('d1')
    expect(query.requesterId).toBe('r1')
    expect(query.createdFrom).toBe('2026-01-01T00:00:00.000Z')
    expect(query.createdTo).toBe('2026-02-01T00:00:00.000Z')
    expect(query.slaState).toBe('Breached')
    expect(query.sort).toBe('DueAt')
    expect(query.direction).toBe('Descending')
    expect(query.page).toBe(3)
    expect(query.pageSize).toBe(50)
  })

  it('keeps "unassigned" distinct from "no assignee filter"', () => {
    expect(parseTicketQuery(new URLSearchParams('unassigned=true')).unassigned).toBe(true)
    expect(parseTicketQuery(new URLSearchParams()).unassigned).toBe(false)
  })

  it('falls back rather than passing a hand-edited value to the API', () => {
    // A bad address should land somebody on a sane queue, not on a 400.
    const query = parseTicketQuery(
      new URLSearchParams(
        'status=Nonsense&status=New&slaState=Nonsense&sort=Nonsense' +
          '&direction=sideways&page=-4&pageSize=7',
      ),
    )

    expect(query.status).toEqual(['New'])
    expect(query.slaState).toBeNull()
    expect(query.sort).toBe(defaultSort)
    expect(query.direction).toBe(defaultDirection)
    expect(query.page).toBe(1)
    expect(query.pageSize).toBe(defaultTicketQuery.pageSize)
  })
})

describe('serializeTicketQuery', () => {
  it('round-trips every field, so a link reopens the queue it came from', () => {
    const original = {
      ...defaultTicketQuery,
      status: ['New', 'Waiting'] as const,
      priorityId: 'p1',
      categoryId: 'c1',
      assigneeId: 'a1',
      departmentId: 'd1',
      requesterId: 'r1',
      createdFrom: '2026-01-01T00:00:00.000Z',
      createdTo: '2026-02-01T00:00:00.000Z',
      slaState: 'Approaching' as const,
      sort: 'UpdatedAt' as const,
      direction: 'Descending' as const,
      page: 4,
      pageSize: 100,
    }

    expect(parseTicketQuery(serializeTicketQuery(original))).toEqual(original)
  })

  it('always states the ordering, so the address survives a change of API default', () => {
    const params = serializeTicketQuery(defaultTicketQuery)

    expect(params.get('sort')).toBe(defaultSort)
    expect(params.get('direction')).toBe(defaultDirection)
  })

  it('leaves page one out of the address and keeps it out of the way', () => {
    expect(serializeTicketQuery(defaultTicketQuery).has('page')).toBe(false)
    expect(serializeTicketQuery({ ...defaultTicketQuery, page: 2 }).get('page')).toBe('2')
  })

  it('omits an unset filter entirely rather than sending an empty one', () => {
    const params = serializeTicketQuery(defaultTicketQuery)

    expect(params.has('priorityId')).toBe(false)
    expect(params.has('unassigned')).toBe(false)
    expect(params.has('slaState')).toBe(false)
  })
})

describe('withFilters', () => {
  it('returns to the first page, because page four of a different question may not exist', () => {
    const query = withFilters({ ...defaultTicketQuery, page: 4 }, { priorityId: 'p1' })

    expect(query.page).toBe(1)
    expect(query.priorityId).toBe('p1')
  })

  it('lets a caller name the page it wants', () => {
    expect(withFilters(defaultTicketQuery, { page: 3 }).page).toBe(3)
  })
})

describe('clearedFilters', () => {
  it('drops the filters and keeps the ordering and page size somebody chose', () => {
    const query = clearedFilters({
      ...defaultTicketQuery,
      status: ['New'],
      priorityId: 'p1',
      unassigned: true,
      sort: 'DueAt',
      direction: 'Descending',
      pageSize: 100,
      page: 5,
    })

    expect(hasActiveFilters(query)).toBe(false)
    expect(query.sort).toBe('DueAt')
    expect(query.direction).toBe('Descending')
    expect(query.pageSize).toBe(100)
    expect(query.page).toBe(1)
  })
})

describe('hasActiveFilters', () => {
  it('is false for the queue as it opens', () => {
    expect(hasActiveFilters(defaultTicketQuery)).toBe(false)
  })

  it('is true for each filter on its own', () => {
    expect(hasActiveFilters({ ...defaultTicketQuery, status: ['New'] })).toBe(true)
    expect(hasActiveFilters({ ...defaultTicketQuery, unassigned: true })).toBe(true)
    expect(hasActiveFilters({ ...defaultTicketQuery, slaState: 'Breached' })).toBe(true)
    expect(hasActiveFilters({ ...defaultTicketQuery, requesterId: 'r1' })).toBe(true)
  })

  it('does not count sorting or paging as a filter', () => {
    expect(
      hasActiveFilters({ ...defaultTicketQuery, sort: 'Number', page: 6, pageSize: 100 }),
    ).toBe(false)
  })
})

describe('sameTicketQuery', () => {
  it('compares the rows asked for, not the object identity', () => {
    expect(sameTicketQuery(defaultTicketQuery, { ...defaultTicketQuery })).toBe(true)
    expect(sameTicketQuery(defaultTicketQuery, { ...defaultTicketQuery, page: 2 })).toBe(false)
  })
})

describe('the date range', () => {
  it('takes the calendar day on the viewer’s own clock, not on UTC', () => {
    const start = dayStart('2026-03-04')
    const end = dayEnd('2026-03-04')

    expect(start).not.toBeNull()
    expect(end).not.toBeNull()

    const startLocal = new Date(start as string)
    const endLocal = new Date(end as string)

    expect(startLocal.getHours()).toBe(0)
    expect(startLocal.getDate()).toBe(4)
    expect(endLocal.getHours()).toBe(23)
    expect(endLocal.getMinutes()).toBe(59)
    expect(endLocal.getDate()).toBe(4)
  })

  it('round-trips back into what a date input can show', () => {
    expect(toDateInput(dayStart('2026-03-04'))).toBe('2026-03-04')
    expect(toDateInput(dayEnd('2026-12-31'))).toBe('2026-12-31')
  })

  it('reads a cleared input and an unparseable instant as no filter', () => {
    expect(dayStart('')).toBeNull()
    expect(dayEnd('not-a-date')).toBeNull()
    expect(toDateInput(null)).toBe('')
    expect(toDateInput('not-an-instant')).toBe('')
  })
})
