/**
 * The queue's state, and its round trip through the URL.
 *
 * CONVENTIONS.md requires every list screen to keep filter, sort, and page state in the
 * address, and DESIGN.md §6 repeats it: a view somebody is looking at has to be a view
 * they can send to somebody else. So the URL is the state — there is no local mirror of
 * it, nothing to keep in step, and a reload lands on exactly the same rows.
 *
 * Everything here is pure and takes `URLSearchParams`, which is what makes it testable
 * without a router and what lets the page decide when to navigate.
 */

import type { SlaState, SortDirection, TicketSort, TicketStatus } from '@/lib/api/types'
import { statusOrder, slaStateOrder } from './ticket-display'

/** The queue, as the screen holds it. */
export interface TicketQuery {
  /** Repeatable: "open" is four statuses rather than one (WP-1.5). */
  readonly status: readonly TicketStatus[]
  readonly priorityId: string | null
  readonly categoryId: string | null
  readonly assigneeId: string | null
  /** "Only the tickets nobody holds" — a different question from "no assignee filter". */
  readonly unassigned: boolean
  readonly departmentId: string | null
  readonly requesterId: string | null
  /** An ISO instant, or null. */
  readonly createdFrom: string | null
  readonly createdTo: string | null
  readonly slaState: SlaState | null
  readonly sort: TicketSort
  readonly direction: SortDirection
  readonly page: number
  readonly pageSize: number
}

const sortOptions: readonly TicketSort[] = ['CreatedAt', 'UpdatedAt', 'Priority', 'Number', 'DueAt']
const directionOptions: readonly SortDirection[] = ['Ascending', 'Descending']

/** The page sizes the screen offers. The API clamps at 200 regardless. */
export const pageSizeOptions: readonly number[] = [25, 50, 100]

/**
 * The queue's own default order: most urgent first, ties broken by age, oldest first.
 *
 * WP-1.5 deliberately left the API's default at newest-created-first and left this
 * screen to ask for the queue ordering explicitly. It is written into the URL rather
 * than left implicit, so what somebody is looking at is what they can send on — and so a
 * later change to the API's default cannot silently reorder this screen.
 */
export const defaultSort: TicketSort = 'Priority'
export const defaultDirection: SortDirection = 'Ascending'
export const defaultPageSize = 25

/** The query a bare `/tickets` means. */
export const defaultTicketQuery: TicketQuery = {
  status: [],
  priorityId: null,
  categoryId: null,
  assigneeId: null,
  unassigned: false,
  departmentId: null,
  requesterId: null,
  createdFrom: null,
  createdTo: null,
  slaState: null,
  sort: defaultSort,
  direction: defaultDirection,
  page: 1,
  pageSize: defaultPageSize,
}

/**
 * Reads the queue out of a URL.
 *
 * Anything unrecognised falls back to the default rather than being passed through: a
 * hand-edited address should land somebody on a sane queue, not on a 400 from the API.
 */
export function parseTicketQuery(params: URLSearchParams): TicketQuery {
  return {
    status: params.getAll('status').filter(isStatus),
    priorityId: params.get('priorityId'),
    categoryId: params.get('categoryId'),
    assigneeId: params.get('assigneeId'),
    unassigned: params.get('unassigned') === 'true',
    departmentId: params.get('departmentId'),
    requesterId: params.get('requesterId'),
    createdFrom: params.get('createdFrom'),
    createdTo: params.get('createdTo'),
    slaState: oneOf(params.get('slaState'), slaStateOrder),
    sort: oneOf(params.get('sort'), sortOptions) ?? defaultSort,
    direction: oneOf(params.get('direction'), directionOptions) ?? defaultDirection,
    page: positiveInteger(params.get('page')) ?? 1,
    pageSize: oneOf(positiveInteger(params.get('pageSize')), pageSizeOptions) ?? defaultPageSize,
  }
}

/**
 * Writes the queue back into a URL.
 *
 * Defaults are written out too, not omitted. An address that says what it is sorted by
 * survives being pasted somewhere after the default has changed, which is the whole
 * point of the sort being in the URL at all.
 */
export function serializeTicketQuery(query: TicketQuery): URLSearchParams {
  const params = new URLSearchParams()

  for (const status of query.status) {
    params.append('status', status)
  }

  appendIf(params, 'priorityId', query.priorityId)
  appendIf(params, 'categoryId', query.categoryId)
  appendIf(params, 'assigneeId', query.assigneeId)
  if (query.unassigned) {
    params.append('unassigned', 'true')
  }
  appendIf(params, 'departmentId', query.departmentId)
  appendIf(params, 'requesterId', query.requesterId)
  appendIf(params, 'createdFrom', query.createdFrom)
  appendIf(params, 'createdTo', query.createdTo)
  appendIf(params, 'slaState', query.slaState)

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
 * Every filter change resets the page, because page four of a different question is a
 * page that may not exist — and an empty screen is a worse answer than the first page.
 * A caller changing the page says so by naming `page` itself.
 */
export function withFilters(query: TicketQuery, changes: Partial<TicketQuery>): TicketQuery {
  return { ...query, page: 1, ...changes }
}

/** True when two queries describe the same rows in the same order on the same page. */
export function sameTicketQuery(left: TicketQuery, right: TicketQuery): boolean {
  return serializeTicketQuery(left).toString() === serializeTicketQuery(right).toString()
}

/** True when anything is filtering the queue — the "clear filters" control's reason to exist. */
export function hasActiveFilters(query: TicketQuery): boolean {
  return (
    query.status.length > 0 ||
    query.priorityId !== null ||
    query.categoryId !== null ||
    query.assigneeId !== null ||
    query.unassigned ||
    query.departmentId !== null ||
    query.requesterId !== null ||
    query.createdFrom !== null ||
    query.createdTo !== null ||
    query.slaState !== null
  )
}

/** Clears every filter, keeping the ordering and the page size somebody chose. */
export function clearedFilters(query: TicketQuery): TicketQuery {
  return {
    ...defaultTicketQuery,
    sort: query.sort,
    direction: query.direction,
    pageSize: query.pageSize,
  }
}

/**
 * A `<input type="date">` value turned into the instant that day begins, locally.
 *
 * The filter is a range of instants on the wire (ARCHITECTURE.md §11 stores everything
 * UTC) but a person picks a calendar day, and the day they mean is the one on their own
 * clock — "since the 3rd" in Saipan is not "since the 3rd" in UTC.
 */
export function dayStart(value: string): string | null {
  const parts = splitDate(value)
  if (parts === null) {
    return null
  }

  return new Date(parts[0], parts[1] - 1, parts[2], 0, 0, 0, 0).toISOString()
}

/** The same, for the last instant of the chosen day — a range's end is inclusive. */
export function dayEnd(value: string): string | null {
  const parts = splitDate(value)
  if (parts === null) {
    return null
  }

  return new Date(parts[0], parts[1] - 1, parts[2], 23, 59, 59, 999).toISOString()
}

/** An instant turned back into the local calendar day a date input can show. */
export function toDateInput(value: string | null): string {
  if (value === null) {
    return ''
  }

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return ''
  }

  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${String(date.getFullYear())}-${month}-${day}`
}

function splitDate(value: string): [number, number, number] | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (match === null) {
    return null
  }

  return [Number(match[1]), Number(match[2]), Number(match[3])]
}

function appendIf(params: URLSearchParams, name: string, value: string | null): void {
  if (value !== null && value !== '') {
    params.append(name, value)
  }
}

function isStatus(value: string): value is TicketStatus {
  return (statusOrder as readonly string[]).includes(value)
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
  if (value === null) {
    return null
  }

  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null
}
