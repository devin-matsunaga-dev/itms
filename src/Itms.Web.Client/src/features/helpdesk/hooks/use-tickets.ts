import { useQuery, type UseQueryResult } from '@tanstack/react-query'
import type {
  Department,
  PagedTickets,
  TicketCategory,
  TicketPriority,
  UserSummary,
} from '@/lib/api/types'
import {
  fetchAssignableUsers,
  fetchDepartments,
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

export function useDepartments(): UseQueryResult<Department[]> {
  return useQuery({
    queryKey: ['directory', 'departments'],
    queryFn: ({ signal }) => fetchDepartments(signal),
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
