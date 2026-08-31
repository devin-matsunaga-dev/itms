import { apiFetch } from '@/lib/api/client'
import type {
  Department,
  PagedTickets,
  TicketCategory,
  TicketPriority,
  UserSummary,
} from '@/lib/api/types'
import { serializeTicketQuery, type TicketQuery } from '../lib/ticket-query'

/** The helpdesk endpoints the queue screen reads (WP-1.5's `/api/v1/tickets` group). */

/**
 * A page of the ticket queue.
 *
 * The query is serialized by the same function that writes the address bar, so what the
 * screen asks the server for and what the URL claims it is showing cannot drift.
 */
export function fetchTickets(query: TicketQuery, signal?: AbortSignal): Promise<PagedTickets> {
  const params = serializeTicketQuery(query)
  // The URL omits page 1 to keep a first-page link tidy; the API is told explicitly.
  params.set('page', String(query.page))

  return apiFetch<PagedTickets>(`/tickets?${params.toString()}`, signal ? { signal } : {})
}

/** Active ticket categories, for the category filter. */
export async function fetchTicketCategories(signal?: AbortSignal): Promise<TicketCategory[]> {
  const page = await apiFetch<{ items: TicketCategory[] }>(
    '/ticket-categories?pageSize=200',
    signal ? { signal } : {},
  )
  return page.items
}

/** Active ticket priorities, rank first, for the priority filter. */
export async function fetchTicketPriorities(signal?: AbortSignal): Promise<TicketPriority[]> {
  const page = await apiFetch<{ items: TicketPriority[] }>(
    '/ticket-priorities?pageSize=200',
    signal ? { signal } : {},
  )
  return page.items
}

/** Active departments, for the department filter. */
export async function fetchDepartments(signal?: AbortSignal): Promise<Department[]> {
  const page = await apiFetch<{ items: Department[] }>(
    '/departments?pageSize=200',
    signal ? { signal } : {},
  )
  return page.items
}

/**
 * People who can hold a ticket, for the assignee filter.
 *
 * The endpoint is Technician-guarded, so this is never called for an end user — their
 * queue is their own tickets and an assignee filter would answer nothing they can ask.
 */
export function fetchAssignableUsers(signal?: AbortSignal): Promise<UserSummary[]> {
  return apiFetch<UserSummary[]>('/users?limit=200', signal ? { signal } : {})
}
