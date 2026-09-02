import { describe, expect, it } from 'vitest'
import {
  clearedFilters,
  defaultUserQuery,
  hasActiveFilters,
  parseUserQuery,
  sameUserQuery,
  serializeUserQuery,
  withFilters,
  type UserQuery,
} from './user-query'

function parse(search: string): UserQuery {
  return parseUserQuery(new URLSearchParams(search))
}

function serialize(query: UserQuery): string {
  return serializeUserQuery(query).toString()
}

describe('parseUserQuery', () => {
  it('reads a bare address as the whole directory, by name', () => {
    expect(parse('')).toEqual(defaultUserQuery)
  })

  it('reads every filter out of the address', () => {
    const query = parse(
      'search=santos&departmentId=dep-1&locationId=loc-1&role=Technician' +
        '&includeInactive=true&sort=Email&direction=Descending&page=3&pageSize=50',
    )

    expect(query).toEqual({
      search: 'santos',
      departmentId: 'dep-1',
      locationId: 'loc-1',
      role: 'Technician',
      includeInactive: true,
      sort: 'Email',
      direction: 'Descending',
      page: 3,
      pageSize: 50,
    })
  })

  it('falls back to the default rather than passing an unrecognised sort through', () => {
    const query = parse('sort=Department&direction=Sideways&pageSize=7&page=0')

    expect(query.sort).toBe(defaultUserQuery.sort)
    expect(query.direction).toBe(defaultUserQuery.direction)
    expect(query.pageSize).toBe(defaultUserQuery.pageSize)
    expect(query.page).toBe(1)
  })

  it('drops a role nothing in the system has', () => {
    // There are three roles and only three (ARCHITECTURE.md §7), so an unrecognised one
    // is a typo rather than a filter — unlike an asset status code, which an
    // administrator may legitimately have added (WP-2.6a).
    expect(parse('role=Superuser').role).toBeNull()
  })

  it('treats anything but true as not including the deactivated', () => {
    expect(parse('includeInactive=false').includeInactive).toBe(false)
    expect(parse('includeInactive=yes').includeInactive).toBe(false)
    expect(parse('includeInactive=true').includeInactive).toBe(true)
  })
})

describe('serializeUserQuery', () => {
  it('writes the ordering out even when it is the default, and leaves page one off', () => {
    expect(serialize(defaultUserQuery)).toBe(
      'sort=DisplayName&direction=Ascending&pageSize=25',
    )
  })

  it('round-trips every filter', () => {
    const query = parse(
      'search=santos&departmentId=dep-1&locationId=loc-1&role=Admin' +
        '&includeInactive=true&sort=CreatedAt&direction=Descending&page=2&pageSize=100',
    )

    expect(parse(serialize(query))).toEqual(query)
  })

  it('trims the search term rather than sending the spaces', () => {
    expect(serialize({ ...defaultUserQuery, search: '  santos  ' })).toContain('search=santos')
  })

  it('omits a blank search entirely', () => {
    expect(serialize({ ...defaultUserQuery, search: '   ' })).not.toContain('search')
  })
})

describe('withFilters', () => {
  it('returns to page one whenever a filter moves', () => {
    const onPageFour = { ...defaultUserQuery, page: 4 }

    expect(withFilters(onPageFour, { role: 'Technician' }).page).toBe(1)
  })

  it('lets a caller change the page by naming it', () => {
    expect(withFilters(defaultUserQuery, { page: 3 }).page).toBe(3)
  })
})

describe('hasActiveFilters', () => {
  it('is false for the query a bare address means', () => {
    expect(hasActiveFilters(defaultUserQuery)).toBe(false)
  })

  it('counts the deactivated toggle, which narrows nothing but changes the rows', () => {
    expect(hasActiveFilters({ ...defaultUserQuery, includeInactive: true })).toBe(true)
  })

  it('ignores the ordering and the page size, which describe how not which', () => {
    expect(
      hasActiveFilters({ ...defaultUserQuery, sort: 'Email', pageSize: 100, page: 5 }),
    ).toBe(false)
  })
})

describe('clearedFilters', () => {
  it('keeps the ordering and the page size somebody chose', () => {
    const cleared = clearedFilters({
      ...defaultUserQuery,
      search: 'santos',
      role: 'Admin',
      includeInactive: true,
      sort: 'CreatedAt',
      direction: 'Descending',
      pageSize: 100,
      page: 6,
    })

    expect(cleared.search).toBe('')
    expect(cleared.role).toBeNull()
    expect(cleared.includeInactive).toBe(false)
    expect(cleared.sort).toBe('CreatedAt')
    expect(cleared.direction).toBe('Descending')
    expect(cleared.pageSize).toBe(100)
    expect(cleared.page).toBe(1)
  })
})

describe('sameUserQuery', () => {
  it('compares what the address says, not the object', () => {
    expect(sameUserQuery(defaultUserQuery, { ...defaultUserQuery, search: '  ' })).toBe(true)
    expect(sameUserQuery(defaultUserQuery, { ...defaultUserQuery, role: 'Admin' })).toBe(false)
  })
})
