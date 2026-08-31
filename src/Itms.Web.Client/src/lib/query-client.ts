import { QueryClient } from '@tanstack/react-query'
import { ApiError } from '@/lib/api/client'

/**
 * The server-state store. CONVENTIONS.md makes TanStack Query the only place server
 * data lives — no `useEffect` fetching, no manual `fetch` in a component.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // A 4xx is an answer, not a hiccup: retrying a 401, a 403, or a validation
        // failure only delays showing the user what happened.
        retry: (failureCount, error) => {
          if (error instanceof ApiError && error.status < 500) {
            return false
          }
          return failureCount < 2
        },
        staleTime: 30_000,
        refetchOnWindowFocus: false,
      },
      mutations: {
        retry: false,
      },
    },
  })
}
