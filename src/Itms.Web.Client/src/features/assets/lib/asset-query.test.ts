import { describe, expect, it } from 'vitest'
import {
  activeWarrantyOption,
  advancedFilterCount,
  clearedFilters,
  defaultAssetQuery,
  hasActiveFilters,
  parseAssetQuery,
  sameAssetQuery,
  serializeAssetQuery,
  warrantyOptions,
  withFilters,
  type AssetQuery,
} from './asset-query'

function parse(search: string): AssetQuery {
  return parseAssetQuery(new URLSearchParams(search))
}

function serialize(query: AssetQuery): string {
  return serializeAssetQuery(query).toString()
}

describe('parseAssetQuery', () => {
  it('reads a bare address as the whole register, by tag', () => {
    expect(parse('')).toEqual(defaultAssetQuery)
  })

  it('reads every filter out of the address', () => {
    const query = parse(
      'assetTypeId=type-1&statusCode=deployed&statusCode=repair&departmentId=dep-1' +
        '&locationId=loc-1&assignedToUserId=user-1&warrantyExpiringInDays=30' +
        '&warrantyExpired=true&search=LAP&sort=Status&direction=Descending&page=3&pageSize=50',
    )

    expect(query).toEqual({
      assetTypeId: 'type-1',
      statusCode: ['deployed', 'repair'],
      departmentId: 'dep-1',
      locationId: 'loc-1',
      assignedToUserId: 'user-1',
      unassigned: false,
      warrantyExpiringInDays: 30,
      warrantyExpired: true,
      search: 'LAP',
      sort: 'Status',
      direction: 'Descending',
      page: 3,
      pageSize: 50,
    })
  })

  it('falls back to the default rather than passing an unrecognised sort through', () => {
    // WP-1.9: a hand-edited address should land somebody on a sane list, not on a 400.
    const query = parse('sort=Cost&direction=Sideways&pageSize=7&page=0')

    expect(query.sort).toBe(defaultAssetQuery.sort)
    expect(query.direction).toBe(defaultAssetQuery.direction)
    expect(query.pageSize).toBe(defaultAssetQuery.pageSize)
    expect(query.page).toBe(1)
  })

  it('lets an unrecognised status code through, because a status is configurable', () => {
    // WP-2.1 lets an administrator add a status; WP-2.3 settled that an unrecognised code
    // is a filter matching nothing rather than an error. Narrowing it to the seeded six
    // would break the deployment that added a seventh.
    expect(parse('statusCode=on-loan').statusCode).toEqual(['on-loan'])
  })

  it('reads a zero-day warranty window, which is the warranties running out today', () => {
    expect(parse('warrantyExpiringInDays=0').warrantyExpiringInDays).toBe(0)
    expect(parse('warrantyExpiringInDays=-5').warrantyExpiringInDays).toBeNull()
    expect(parse('warrantyExpiringInDays=soon').warrantyExpiringInDays).toBeNull()
  })

  it('tells the three warranty-expired readings apart', () => {
    expect(parse('warrantyExpired=true').warrantyExpired).toBe(true)
    expect(parse('warrantyExpired=false').warrantyExpired).toBe(false)
    expect(parse('').warrantyExpired).toBeNull()
  })
})

describe('serializeAssetQuery', () => {
  it('round-trips every filter through the address', () => {
    const query: AssetQuery = {
      ...defaultAssetQuery,
      assetTypeId: 'type-1',
      statusCode: ['deployed', 'in-stock'],
      departmentId: 'dep-1',
      locationId: 'loc-1',
      assignedToUserId: 'user-1',
      warrantyExpiringInDays: 60,
      warrantyExpired: false,
      search: 'LAP-0042',
      sort: 'WarrantyExpiresAt',
      direction: 'Descending',
      page: 4,
      pageSize: 100,
    }

    expect(parse(serialize(query))).toEqual(query)
  })

  it('states the ordering and the page size even when they are the defaults', () => {
    // A link that says what it is sorted by survives a later change to that default.
    const params = serializeAssetQuery(defaultAssetQuery)

    expect(params.get('sort')).toBe('AssetTag')
    expect(params.get('direction')).toBe('Ascending')
    expect(params.get('pageSize')).toBe('25')
  })

  it('leaves page one out, so a first-page link reads tidily', () => {
    expect(serializeAssetQuery(defaultAssetQuery).has('page')).toBe(false)
    expect(serializeAssetQuery({ ...defaultAssetQuery, page: 2 }).get('page')).toBe('2')
  })

  it('trims the search term and drops an empty one', () => {
    expect(serializeAssetQuery({ ...defaultAssetQuery, search: '  LAP  ' }).get('search')).toBe('LAP')
    expect(serializeAssetQuery({ ...defaultAssetQuery, search: '   ' }).has('search')).toBe(false)
  })

  it('never asks for a named holder and the unheld at once', () => {
    // One null cannot mean both "no filter" and "nobody holds it", which is why the server
    // has a flag of its own (WP-2.3) — and asking for both would be two contradictory
    // things in one address.
    const params = serializeAssetQuery({
      ...defaultAssetQuery,
      assignedToUserId: 'user-1',
      unassigned: true,
    })

    expect(params.get('unassigned')).toBe('true')
    expect(params.has('assignedToUserId')).toBe(false)
  })
})

describe('withFilters', () => {
  it('returns to page one on a filter change', () => {
    const query = { ...defaultAssetQuery, page: 4 }

    // Page four of a different question is a page that may not exist.
    expect(withFilters(query, { assetTypeId: 'type-1' }).page).toBe(1)
  })

  it('honours a page the caller names itself', () => {
    expect(withFilters(defaultAssetQuery, { page: 3 }).page).toBe(3)
  })
})

describe('hasActiveFilters and clearedFilters', () => {
  it('is false for the whole register and true for anything narrower', () => {
    expect(hasActiveFilters(defaultAssetQuery)).toBe(false)
    expect(hasActiveFilters({ ...defaultAssetQuery, statusCode: ['deployed'] })).toBe(true)
    expect(hasActiveFilters({ ...defaultAssetQuery, unassigned: true })).toBe(true)
    expect(hasActiveFilters({ ...defaultAssetQuery, warrantyExpired: false })).toBe(true)
    expect(hasActiveFilters({ ...defaultAssetQuery, search: '  ' })).toBe(false)
  })

  it('keeps the ordering and the page size somebody chose', () => {
    const query: AssetQuery = {
      ...defaultAssetQuery,
      statusCode: ['repair'],
      sort: 'UpdatedAt',
      direction: 'Descending',
      pageSize: 100,
      page: 5,
    }

    const cleared = clearedFilters(query)

    expect(hasActiveFilters(cleared)).toBe(false)
    expect(cleared.sort).toBe('UpdatedAt')
    expect(cleared.direction).toBe('Descending')
    expect(cleared.pageSize).toBe(100)
    expect(cleared.page).toBe(1)
  })
})

describe('advancedFilterCount', () => {
  it('counts only what the popover contains', () => {
    // The badge has to describe something the reader can open and clear from that panel.
    expect(
      advancedFilterCount({
        ...defaultAssetQuery,
        assetTypeId: 'type-1',
        statusCode: ['deployed'],
        warrantyExpiringInDays: 30,
      }),
    ).toBe(0)

    expect(
      advancedFilterCount({
        ...defaultAssetQuery,
        departmentId: 'dep-1',
        locationId: 'loc-1',
        unassigned: true,
      }),
    ).toBe(3)
  })

  it('counts the holder once, however it is expressed', () => {
    expect(advancedFilterCount({ ...defaultAssetQuery, assignedToUserId: 'user-1' })).toBe(1)
    expect(advancedFilterCount({ ...defaultAssetQuery, unassigned: true })).toBe(1)
  })
})

describe('activeWarrantyOption', () => {
  it('names every pairing its own options produce', () => {
    for (const option of warrantyOptions) {
      const query: AssetQuery = {
        ...defaultAssetQuery,
        warrantyExpiringInDays: option.expiringInDays,
        warrantyExpired: option.expired,
      }

      expect(activeWarrantyOption(query)?.value).toBe(option.value)
    }
  })

  it('offers the union the server built deliberately', () => {
    // WP-2.3: given together the two parameters union rather than narrow, because the two
    // windows are disjoint and intersecting them could only ever return nothing.
    const attention = warrantyOptions.find((option) => option.value === 'attention')

    expect(attention?.expiringInDays).toBe(30)
    expect(attention?.expired).toBe(true)
  })

  it('answers null for a window no option names, rather than pretending it is unfiltered', () => {
    // `?warrantyExpiringInDays=45` is a filter the server honours. The select showing
    // "Any warranty" over a filtered list would be lying about the rows.
    expect(activeWarrantyOption({ ...defaultAssetQuery, warrantyExpiringInDays: 45 })).toBeNull()
  })
})

describe('sameAssetQuery', () => {
  it('compares the rows, not the object', () => {
    expect(sameAssetQuery(defaultAssetQuery, { ...defaultAssetQuery })).toBe(true)
    expect(sameAssetQuery(defaultAssetQuery, { ...defaultAssetQuery, search: 'LAP' })).toBe(false)
    // Page one and an unstated page are the same page.
    expect(sameAssetQuery(defaultAssetQuery, { ...defaultAssetQuery, page: 1 })).toBe(true)
  })
})
