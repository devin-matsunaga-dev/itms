import { apiFetch } from '@/lib/api/client'
import type {
  AssetSummary,
  PagedUsers,
  TicketActivity,
  UserSummary,
  UserTicketPage,
} from '@/lib/api/types'
import { serializeUserQuery, type UserQuery } from '../lib/user-query'

/**
 * The user directory and the three reads behind the user 360 page (WP-0.5, WP-2.5, WP-2.7).
 *
 * `GET /api/v1/users` is Technician-or-Admin: an end user's business is their own tickets
 * (SPEC.md §14) and they have no reason to enumerate the staff directory. The two panel
 * reads are the exception WP-2.5 built deliberately — anybody may ask them about
 * themselves — but this screen is the staff one, and nothing here is the enforcement.
 */

/**
 * A page of the directory.
 *
 * The query is serialized by the same function that writes the address bar, so what the
 * screen asks the server for and what the URL says are the same string by construction —
 * the property WP-1.9 built `serializeTicketQuery` for.
 */
export function fetchUsers(query: UserQuery, signal?: AbortSignal): Promise<PagedUsers> {
  return apiFetch<PagedUsers>(
    `/users?${serializeUserQuery(query).toString()}`,
    signal ? { signal } : {},
  )
}

/** One person's public summary. It carries no credential state of any kind. */
export function fetchUser(id: string, signal?: AbortSignal): Promise<UserSummary> {
  return apiFetch<UserSummary>(`/users/${id}`, signal ? { signal } : {})
}

/**
 * The equipment somebody is holding right now.
 *
 * Unpaged, because it answers what one person is holding — a handful of things rather than
 * a queue (WP-2.5).
 */
export function fetchUserAssets(id: string, signal?: AbortSignal): Promise<AssetSummary[]> {
  return apiFetch<AssetSummary[]>(`/users/${id}/assets`, signal ? { signal } : {})
}

/**
 * The tickets somebody raised, newest first.
 *
 * `state=Open` is what is still being worked and `state=Past` is what is finished with.
 * The two are complementary, so the pair is the whole history and nothing appears in both
 * — which is what lets the screen render SPEC.md §4's two panels from two calls without
 * either of them having to know the status set.
 */
export function fetchUserTickets(
  id: string,
  state: TicketActivity,
  pageSize: number,
  signal?: AbortSignal,
): Promise<UserTicketPage> {
  return apiFetch<UserTicketPage>(
    `/users/${id}/tickets?state=${state}&pageSize=${String(pageSize)}`,
    signal ? { signal } : {},
  )
}
