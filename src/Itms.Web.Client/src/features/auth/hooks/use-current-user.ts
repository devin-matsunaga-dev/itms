import { useQuery, type UseQueryResult } from '@tanstack/react-query'
import { fetchCurrentUser } from '@/features/auth/api/auth-api'
import type { AuthenticatedUser } from '@/lib/api/generated-pending'

/** The cache key for "who am I". Exported so sign-in and sign-out can write it. */
export const currentUserKey = ['auth', 'me'] as const

/**
 * The signed-in account, or null when nobody is. `null` is a settled answer — the
 * query does not retry it, because a 401 is the server's decision, not a hiccup.
 */
export function useCurrentUser(): UseQueryResult<AuthenticatedUser | null> {
  return useQuery({
    queryKey: currentUserKey,
    queryFn: ({ signal }) => fetchCurrentUser(signal),
    retry: false,
    staleTime: 5 * 60_000,
  })
}
