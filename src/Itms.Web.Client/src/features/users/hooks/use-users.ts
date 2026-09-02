import { useQuery, type UseQueryResult } from '@tanstack/react-query'
import type { AssetSummary, PagedUsers, UserSummary, UserTicketPage } from '@/lib/api/types'
import { fetchUser, fetchUserAssets, fetchUserTickets, fetchUsers } from '../api/users-api'
import { serializeUserQuery, type UserQuery } from '../lib/user-query'

/**
 * How many tickets each panel of the user 360 shows before it says there are more.
 *
 * The panels are a summary of somebody's support history, not a queue: SPEC.md §4 asks the
 * user page to show open tickets and previous tickets, and the queue itself is where a
 * hundred of them are read. Ten is enough to see a pattern; the count beside the heading
 * says what is not on screen, and the "View all" link goes to the queue filtered to them.
 */
export const ticketPanelSize = 10

/** The directory's cache keys. The address is the key — one query, one cached page. */
export const userKeys = {
  all: ['identity', 'users'] as const,
  list: (query: UserQuery) =>
    ['identity', 'users', serializeUserQuery(query).toString()] as const,
  detail: (id: string) => ['identity', 'user', id] as const,
  assets: (id: string) => ['identity', 'user-assets', id] as const,
  tickets: (id: string, state: string) => ['identity', 'user-tickets', id, state] as const,
}

/** A page of the directory. */
export function useUsers(query: UserQuery): UseQueryResult<PagedUsers> {
  return useQuery({
    queryKey: userKeys.list(query),
    queryFn: ({ signal }) => fetchUsers(query, signal),
  })
}

/** One person's profile. */
export function useUser(id: string): UseQueryResult<UserSummary> {
  return useQuery({
    queryKey: userKeys.detail(id),
    queryFn: ({ signal }) => fetchUser(id, signal),
    enabled: id.length > 0,
  })
}

/**
 * The three panels of the user 360, each its own query.
 *
 * WP-2.5's own criterion is a single round trip *per panel*, and this is the client half of
 * it: a screen that refreshes the equipment list does not re-read the tickets, and a panel
 * that fails says so without taking the profile down with it. The same call WP-2.6a made
 * for the asset detail.
 */
export function useUserAssets(id: string): UseQueryResult<AssetSummary[]> {
  return useQuery({
    queryKey: userKeys.assets(id),
    queryFn: ({ signal }) => fetchUserAssets(id, signal),
    enabled: id.length > 0,
  })
}

/** The tickets somebody raised that are still being worked. */
export function useUserOpenTickets(id: string): UseQueryResult<UserTicketPage> {
  return useQuery({
    queryKey: userKeys.tickets(id, 'Open'),
    queryFn: ({ signal }) => fetchUserTickets(id, 'Open', ticketPanelSize, signal),
    enabled: id.length > 0,
  })
}

/** The tickets somebody raised that are finished with — resolved, closed, or cancelled. */
export function useUserPastTickets(id: string): UseQueryResult<UserTicketPage> {
  return useQuery({
    queryKey: userKeys.tickets(id, 'Past'),
    queryFn: ({ signal }) => fetchUserTickets(id, 'Past', ticketPanelSize, signal),
    enabled: id.length > 0,
  })
}
