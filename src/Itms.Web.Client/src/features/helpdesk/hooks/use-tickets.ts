import { useQuery, type UseQueryResult } from '@tanstack/react-query'
import type {
  TicketCounters,
  PagedTickets,
  TicketCategory,
  TicketPriority,
  UserSummary,
} from '@/lib/api/types'
import {
  fetchAssignableUsers,
  fetchTicketCounters,
  fetchTicketCategories,
  fetchTicketPriorities,
  fetchTickets,
} from '../api/tickets-api'
import { serializeTicketQuery, type TicketQuery } from '../lib/ticket-query'

/** The queue's cache keys. The query itself is the key — one address, one cached page. */
export const ticketKeys = {
  all: ['helpdesk', 'tickets'] as const,
  list: (query: TicketQuery) =>
    ['helpdesk', 'tickets', serializeTicketQuery(query).toString()] as const,
}

/** A page of the queue. */
export function useTickets(query: TicketQuery): UseQueryResult<PagedTickets> {
  return useQuery({
    queryKey: ticketKeys.list(query),
    queryFn: ({ signal }) => fetchTickets(query, signal),
    // A queue is read far more often than it changes, and paging back and forth should
    // not refetch what was on screen a moment ago.
    staleTime: 30_000,
    placeholderData: (previous) => previous,
  })
}

/**
 * Reference data for the filter bar.
 *
 * Categories, priorities, and departments change about once a quarter, so they are held
 * for the session rather than refetched per filter interaction.
 */
const referenceDataStaleTime = 10 * 60_000

export function useTicketCategories(): UseQueryResult<TicketCategory[]> {
  return useQuery({
    queryKey: ['helpdesk', 'ticket-categories'],
    queryFn: ({ signal }) => fetchTicketCategories(signal),
    staleTime: referenceDataStaleTime,
  })
}

export function useTicketPriorities(): UseQueryResult<TicketPriority[]> {
  return useQuery({
    queryKey: ['helpdesk', 'ticket-priorities'],
    queryFn: ({ signal }) => fetchTicketPriorities(signal),
    staleTime: referenceDataStaleTime,
  })
}

/**
 * People a ticket can be assigned to.
 *
 * `enabled` is not the enforcement — the endpoint is Technician-guarded server-side. It
 * is here so an end user's screen does not fire a call it knows will be refused.
 */
export function useAssignableUsers(enabled: boolean): UseQueryResult<UserSummary[]> {
  return useQuery({
    queryKey: ['identity', 'assignable-users'],
    queryFn: ({ signal }) => fetchAssignableUsers(signal),
    staleTime: referenceDataStaleTime,
    enabled,
  })
}

/**
 * The queue's headline figures.
 *
 * Scope-wide and independent of the filters, so the key carries only the day boundary the
 * request was counted against — a filter change must not refetch them, which is the point
 * of them being scope-wide in the first place.
 *
 * Held for a minute: the numbers move as tickets are raised and resolved, and a KPI that
 * lags a whole session is worse than one round trip a minute. Every write on the detail
 * screen already invalidates `ticketKeys.all`, which does not reach these — a counter
 * catching up a moment later is the right trade against refetching six counts on every
 * comment.
 */
export function useTicketCounters(dayEnd: string): UseQueryResult<TicketCounters> {
  return useQuery({
    queryKey: ['helpdesk', 'ticket-counters', dayEnd],
    queryFn: ({ signal }) => fetchTicketCounters(dayEnd, signal),
    staleTime: 60_000,
  })
}
