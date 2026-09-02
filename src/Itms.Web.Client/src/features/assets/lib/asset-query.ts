/**
 * The asset register's state, and its round trip through the URL.
 *
 * The same contract the ticket queue works under (WP-1.9): CONVENTIONS.md requires a list
 * screen to keep filter, sort, and page state in the address, and DESIGN.md §6 repeats it
 * — a view somebody is looking at has to be a view they can send. So the URL is the state,
 * there is no local mirror, and a reload lands on exactly the same rows.
 *
 * Everything here is pure and takes `URLSearchParams`, so it can be asserted without a
 * router and the page decides when to navigate.
 *
 * ## Two things this does differently from the queue
 *
 * 1. **Statuses are addressed by `statusCode`, never by `assetStatusId`.** `ListAssetsQuery`
 *    accepts both, and WP-2.3 recorded why both exist: an id belongs to one database while
 *    a code is the same in every deployment, so a link written against a code survives a
 *    restore. A screen whose whole reason for putting state in the address is that the
 *    address can be sent to somebody should use the half that travels. One addressing
 *    scheme, following WP-1.16.
 * 2. **The warranty filter is one control over two parameters.** `warrantyExpiringInDays`
 *    and `warrantyExpired` are separate server-side facts — "renew this" and "explain
 *    this" — and given together they *union* rather than narrow, alone among the filters
 *    on that query. `warrantyOptions` names every pairing the control can produce, so the
 *    screen never has to reason about the combination.
 */

import type { AssetSort, SortDirection } from '@/lib/api/types'

/** The register, as the screen holds it. */
export interface AssetQuery {
  readonly assetTypeId: string | null
  /**
   * The lifecycle statuses to include, by their immutable code. Repeatable: "in service"
   * is two statuses rather than one, and an empty list is every status.
   */
  readonly statusCode: readonly string[]
  readonly departmentId: string | null
  readonly locationId: string | null
  readonly assignedToUserId: string | null
  /** "Only the equipment nobody holds" — a different question from "no holder filter". */
  readonly unassigned: boolean
  /** Warranties running out within this many days, or null for no window. */
  readonly warrantyExpiringInDays: number | null
  /** True for lapsed warranties, false for those still running, null for both. */
  readonly warrantyExpired: boolean | null
  /** Free text over tag, serial, name, manufacturer, and model. Empty means no search. */
  readonly search: string
  readonly sort: AssetSort
  readonly direction: SortDirection
  readonly page: number
  readonly pageSize: number
}

const sortOptions: readonly AssetSort[] = [
  'AssetTag',
  'CreatedAt',
  'UpdatedAt',
  'WarrantyExpiresAt',
  'Status',
]

const directionOptions: readonly SortDirection[] = ['Ascending', 'Descending']

/** The page sizes the screen offers. The API clamps at 200 regardless. */
export const pageSizeOptions: readonly number[] = [25, 50, 100]

/**
 * The register's own default order: by the tag on the physical label, ascending.
 *
 * It matches the API's default rather than overriding it — an inventory is a register
 * read against the labels on equipment, which is the reasoning WP-2.3 wrote into
 * `AssetSort` — but it is still written into the address, for WP-1.9's reason: a link
 * that says what it is ordered by survives a later change to that default.
 */
export const defaultSort: AssetSort = 'AssetTag'
export const defaultDirection: SortDirection = 'Ascending'
export const defaultPageSize = 25

/** The query a bare `/assets` means. */
export const defaultAssetQuery: AssetQuery = {
  assetTypeId: null,
  statusCode: [],
  departmentId: null,
  locationId: null,
  assignedToUserId: null,
  unassigned: false,
  warrantyExpiringInDays: null,
  warrantyExpired: null,
  search: '',
  sort: defaultSort,
  direction: defaultDirection,
  page: 1,
  pageSize: defaultPageSize,
}

/**
 * Reads the register out of a URL.
 *
 * A closed set — the sort, the direction, the page size — falls back to its default
 * rather than being passed through, so a hand-edited address lands somebody on a sane
 * list instead of on a 400 (WP-1.9). A **status code is not a closed set**: an
 * administrator may add one, and WP-2.3 settled that an unrecognised code is a filter
 * matching nothing rather than an error. So codes travel through as written.
 */
export function parseAssetQuery(params: URLSearchParams): AssetQuery {
  return {
    assetTypeId: params.get('assetTypeId'),
    statusCode: params.getAll('statusCode').filter((code) => code.length > 0),
    departmentId: params.get('departmentId'),
    locationId: params.get('locationId'),
    assignedToUserId: params.get('assignedToUserId'),
    unassigned: params.get('unassigned') === 'true',
    warrantyExpiringInDays: nonNegativeInteger(params.get('warrantyExpiringInDays')),
    warrantyExpired: booleanOrNull(params.get('warrantyExpired')),
    search: params.get('search') ?? '',
    sort: oneOf(params.get('sort'), sortOptions) ?? defaultSort,
    direction: oneOf(params.get('direction'), directionOptions) ?? defaultDirection,
    page: positiveInteger(params.get('page')) ?? 1,
    pageSize: oneOf(positiveInteger(params.get('pageSize')), pageSizeOptions) ?? defaultPageSize,
  }
}

/**
 * Writes the register back into a URL.
 *
 * The ordering and the page size are written out even when they are the defaults, and
 * page one is left off — WP-1.9's call, for the reason above and because a first-page
 * link should still read tidily.
 */
export function serializeAssetQuery(query: AssetQuery): URLSearchParams {
  const params = new URLSearchParams()

  appendIf(params, 'assetTypeId', query.assetTypeId)

  for (const code of query.statusCode) {
    params.append('statusCode', code)
  }

  appendIf(params, 'departmentId', query.departmentId)
  appendIf(params, 'locationId', query.locationId)

  // The holder filter is one question with two shapes, and one null cannot mean both
  // "no filter" and "nobody holds it" — which is why `unassigned` is a flag of its own
  // server-side (WP-2.3). Naming a holder while asking for the unheld would ask the
  // server for two contradictory things, so only one of them is ever written.
  if (query.unassigned) {
    params.append('unassigned', 'true')
  } else {
    appendIf(params, 'assignedToUserId', query.assignedToUserId)
  }

  if (query.warrantyExpiringInDays !== null) {
    params.append('warrantyExpiringInDays', String(query.warrantyExpiringInDays))
  }
  if (query.warrantyExpired !== null) {
    params.append('warrantyExpired', String(query.warrantyExpired))
  }

  if (query.search.trim().length > 0) {
    params.append('search', query.search.trim())
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
export function withFilters(query: AssetQuery, changes: Partial<AssetQuery>): AssetQuery {
  return { ...query, page: 1, ...changes }
}

/** True when two queries describe the same rows in the same order on the same page. */
export function sameAssetQuery(left: AssetQuery, right: AssetQuery): boolean {
  return serializeAssetQuery(left).toString() === serializeAssetQuery(right).toString()
}

/** True when anything is narrowing the register — the "Clear all" control's reason to exist. */
export function hasActiveFilters(query: AssetQuery): boolean {
  return (
    query.assetTypeId !== null ||
    query.statusCode.length > 0 ||
    query.departmentId !== null ||
    query.locationId !== null ||
    query.assignedToUserId !== null ||
    query.unassigned ||
    query.warrantyExpiringInDays !== null ||
    query.warrantyExpired !== null ||
    query.search.trim().length > 0
  )
}

/** Clears every filter, keeping the ordering and the page size somebody chose. */
export function clearedFilters(query: AssetQuery): AssetQuery {
  return {
    ...defaultAssetQuery,
    sort: query.sort,
    direction: query.direction,
    pageSize: query.pageSize,
  }
}

/**
 * How many of the filters behind the "Filters" popover are set.
 *
 * Counts only what that popover contains, so the badge always describes something the
 * reader can see and clear from the panel it sits on. Type, status, and warranty are
 * inline and speak for themselves.
 */
export function advancedFilterCount(query: AssetQuery): number {
  return (
    (query.departmentId === null ? 0 : 1) +
    (query.locationId === null ? 0 : 1) +
    (query.assignedToUserId === null && !query.unassigned ? 0 : 1)
  )
}

/** One option of the warranty filter, and the pair of parameters it writes. */
export interface WarrantyOption {
  /** The select's own value. Not sent anywhere — the two fields below are. */
  readonly value: string
  readonly label: string
  readonly expiringInDays: number | null
  readonly expired: boolean | null
}

/**
 * Every warranty window the filter offers.
 *
 * The last two are the pairing WP-2.3 built deliberately: given together the two
 * parameters union rather than narrow, so "expired or expiring within 30 days" is one
 * list — the one somebody chasing renewals actually wants — where intersecting them
 * could only ever return nothing.
 */
export const warrantyOptions: readonly WarrantyOption[] = [
  { value: 'any', label: 'Any warranty', expiringInDays: null, expired: null },
  { value: '30', label: 'Expiring in 30 days', expiringInDays: 30, expired: null },
  { value: '60', label: 'Expiring in 60 days', expiringInDays: 60, expired: null },
  { value: '90', label: 'Expiring in 90 days', expiringInDays: 90, expired: null },
  { value: 'expired', label: 'Already expired', expiringInDays: null, expired: true },
  {
    value: 'attention',
    label: 'Expired or expiring in 30 days',
    expiringInDays: 30,
    expired: true,
  },
  { value: 'covered', label: 'Still under warranty', expiringInDays: null, expired: false },
]

/**
 * Which warranty option the address is currently describing, or null for a combination
 * no option names.
 *
 * Null is a real answer rather than a fallback to "any": a hand-written
 * `?warrantyExpiringInDays=45` is a legitimate filter the server honours, and the select
 * showing "Any warranty" over a list that is filtered would be lying about the rows. The
 * trigger renders its placeholder instead, exactly as the toolbar's sort does for an
 * ordering it does not name.
 */
export function activeWarrantyOption(query: AssetQuery): WarrantyOption | null {
  return (
    warrantyOptions.find(
      (option) =>
        option.expiringInDays === query.warrantyExpiringInDays &&
        option.expired === query.warrantyExpired,
    ) ?? null
  )
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
  const parsed = integer(value)
  return parsed !== null && parsed > 0 ? parsed : null
}

/** Zero is a real window — the warranties running out today. */
function nonNegativeInteger(value: string | null): number | null {
  const parsed = integer(value)
  return parsed !== null && parsed >= 0 ? parsed : null
}

function integer(value: string | null): number | null {
  if (value === null || value.trim().length === 0) {
    return null
  }

  const parsed = Number(value)
  return Number.isInteger(parsed) ? parsed : null
}

function booleanOrNull(value: string | null): boolean | null {
  return value === 'true' ? true : value === 'false' ? false : null
}
