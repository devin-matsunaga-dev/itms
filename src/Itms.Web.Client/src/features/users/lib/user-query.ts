/**
 * The user directory's state, and its round trip through the URL.
 *
 * The same contract the ticket queue and the asset register work under (WP-1.9, WP-2.6a):
 * CONVENTIONS.md requires a list screen to keep filter, sort, and page state in the
 * address, and DESIGN.md §6 repeats it — a view somebody is looking at has to be a view
 * they can send. So the URL is the state, there is no local mirror, and a reload lands on
 * exactly the same rows.
 *
 * Everything here is pure and takes `URLSearchParams`, so it can be asserted without a
 * router and the screen decides when to navigate.
 *
 * **The role travels as its name rather than as an id**, unlike the department and the
 * location beside it. There are three roles, they are seeded in every environment, and
 * `ItmsRoles` fixes their spelling product-wide — so `?role=Technician` means the same
 * thing in every deployment where an id would not, which is the reasoning WP-2.6a wrote
 * into the asset register's status code.
 */

import type { SortDirection, UserSort } from '@/lib/api/types'
import { Roles, type Role } from '@/lib/roles'

/** The directory, as the screen holds it. */
export interface UserQuery {
  /** Free text over the display name and the email address. Empty means no search. */
  readonly search: string
  readonly departmentId: string | null
  readonly locationId: string | null
  /** One of the three roles, or null for everybody. */
  readonly role: Role | null
  /**
   * Whether deactivated accounts are shown. False by default, matching the server: a
   * directory is mostly read to find somebody to contact, and invariant 9 means the
   * deactivated never disappear — they are asked for.
   */
  readonly includeInactive: boolean
  readonly sort: UserSort
  readonly direction: SortDirection
  readonly page: number
  readonly pageSize: number
}

const sortOptions: readonly UserSort[] = ['DisplayName', 'Email', 'CreatedAt']
const directionOptions: readonly SortDirection[] = ['Ascending', 'Descending']
const roleOptions: readonly Role[] = [Roles.admin, Roles.technician, Roles.user]

/** The page sizes the screen offers. The API clamps at 200 regardless. */
export const pageSizeOptions: readonly number[] = [25, 50, 100]

/** A directory is read alphabetically, which is also how every picker lists people. */
export const defaultSort: UserSort = 'DisplayName'
export const defaultDirection: SortDirection = 'Ascending'
export const defaultPageSize = 25

/** The query a bare `/users` means. */
export const defaultUserQuery: UserQuery = {
  search: '',
  departmentId: null,
  locationId: null,
  role: null,
  includeInactive: false,
  sort: defaultSort,
  direction: defaultDirection,
  page: 1,
  pageSize: defaultPageSize,
}

/**
 * Reads the directory out of a URL.
 *
 * Every closed set — the role, the sort, the direction, the page size — falls back to its
 * default rather than being passed through, so a hand-edited address lands somebody on a
 * sane list instead of on a 400. The role is a closed set here where the asset register's
 * status code is not, and the difference is real: an administrator may add a status
 * (WP-2.1) but ARCHITECTURE.md §7 says there are three roles and only three.
 */
export function parseUserQuery(params: URLSearchParams): UserQuery {
  return {
    search: params.get('search') ?? '',
    departmentId: params.get('departmentId'),
    locationId: params.get('locationId'),
    role: oneOf(params.get('role'), roleOptions),
    includeInactive: params.get('includeInactive') === 'true',
    sort: oneOf(params.get('sort'), sortOptions) ?? defaultSort,
    direction: oneOf(params.get('direction'), directionOptions) ?? defaultDirection,
    page: positiveInteger(params.get('page')) ?? 1,
    pageSize: oneOf(positiveInteger(params.get('pageSize')), pageSizeOptions) ?? defaultPageSize,
  }
}

/**
 * Writes the directory back into a URL.
 *
 * The ordering and the page size are written out even when they are the defaults, and page
 * one is left off — WP-1.9's call: a link that says what it is ordered by survives a later
 * change to that default, and a first-page link should still read tidily.
 */
export function serializeUserQuery(query: UserQuery): URLSearchParams {
  const params = new URLSearchParams()

  if (query.search.trim().length > 0) {
    params.append('search', query.search.trim())
  }

  appendIf(params, 'departmentId', query.departmentId)
  appendIf(params, 'locationId', query.locationId)
  appendIf(params, 'role', query.role)

  if (query.includeInactive) {
    params.append('includeInactive', 'true')
  }

  params.append('sort', query.sort)
  params.append('direction', query.direction)
  if (query.page > 1) {
    params.append('page', String(query.page))
  }
  params.append('pageSize', String(query.pageSize))

  return params
}

/**
 * A copy of `query` with `changes` applied, returned to page one.
 *
 * Every filter change resets the page: page four of a different question is a page that
 * may not exist, and an empty screen is a worse answer than the first one. A caller
 * changing the page says so by naming `page` itself.
 */
export function withFilters(query: UserQuery, changes: Partial<UserQuery>): UserQuery {
  return { ...query, page: 1, ...changes }
}

/** True when two queries describe the same people in the same order on the same page. */
export function sameUserQuery(left: UserQuery, right: UserQuery): boolean {
  return serializeUserQuery(left).toString() === serializeUserQuery(right).toString()
}

/** True when anything is narrowing the directory — the "Clear all" control's reason to exist. */
export function hasActiveFilters(query: UserQuery): boolean {
  return (
    query.search.trim().length > 0 ||
    query.departmentId !== null ||
    query.locationId !== null ||
    query.role !== null ||
    query.includeInactive
  )
}

/** Clears every filter, keeping the ordering and the page size somebody chose. */
export function clearedFilters(query: UserQuery): UserQuery {
  return {
    ...defaultUserQuery,
    sort: query.sort,
    direction: query.direction,
    pageSize: query.pageSize,
  }
}

function appendIf(params: URLSearchParams, name: string, value: string | null): void {
  if (value !== null && value !== '') {
    params.append(name, value)
  }
}

function oneOf<T extends string | number>(
  value: string | number | null,
  allowed: readonly T[],
): T | null {
  return value !== null && (allowed as readonly (string | number)[]).includes(value)
    ? (value as T)
    : null
}

function positiveInteger(value: string | null): number | null {
  if (value === null || value.trim().length === 0) {
    return null
  }

  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null
}
