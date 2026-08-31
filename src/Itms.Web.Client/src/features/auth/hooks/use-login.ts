import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query'
import { login } from '@/features/auth/api/auth-api'
import { currentUserKey } from '@/features/auth/hooks/use-current-user'
import type { ApiError } from '@/lib/api/client'
import type { AuthenticatedUser, LoginRequest } from '@/lib/api/generated-pending'

/**
 * Signs in and seeds the current-user cache from the response, so the shell renders
 * the right nav immediately instead of waiting on a second round trip to `/me`.
 */
export function useLogin(): UseMutationResult<AuthenticatedUser, ApiError, LoginRequest> {
  const queryClient = useQueryClient()

  return useMutation<AuthenticatedUser, ApiError, LoginRequest>({
    mutationFn: login,
    onSuccess: (user) => {
      queryClient.setQueryData(currentUserKey, user)
    },
  })
}
