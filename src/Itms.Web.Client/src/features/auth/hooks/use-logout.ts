import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query'
import { logout } from '@/features/auth/api/auth-api'
import { currentUserKey } from '@/features/auth/hooks/use-current-user'
import type { ApiError } from '@/lib/api/client'

/**
 * Signs out. The cache is cleared whether or not the call succeeded: the session is
 * gone from this browser's point of view either way, and leaving another account's
 * data in the cache would be worse than an extra fetch.
 */
export function useLogout(): UseMutationResult<void, ApiError, void> {
  const queryClient = useQueryClient()

  return useMutation<void, ApiError, void>({
    mutationFn: logout,
    onSettled: () => {
      queryClient.setQueryData(currentUserKey, null)
      queryClient.clear()
    },
  })
}
