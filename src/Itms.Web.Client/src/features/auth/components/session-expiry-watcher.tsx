import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { onUnauthorized } from '@/lib/api/client'
import { currentUserKey } from '@/features/auth/hooks/use-current-user'

/**
 * Turns a 401 on any call into a sign-out.
 *
 * A session can end between requests — revoked by an administrator, expired against its
 * absolute lifetime, or ended by a password change elsewhere (WP-0.5). Whichever screen
 * happens to make the next call, the whole application has to react, and the person has
 * to be told why they are suddenly back at the login page.
 */
export function SessionExpiryWatcher(): null {
  const queryClient = useQueryClient()

  useEffect(
    () =>
      onUnauthorized(() => {
        // Only a session that existed can expire. Without this, the 401 from the very
        // first `/me` call — the normal answer for a visitor who has never signed in —
        // would announce itself as an expiry.
        if (!queryClient.getQueryData(currentUserKey)) {
          return
        }

        queryClient.setQueryData(currentUserKey, null)
        toast.warning('Your session ended. Sign in to continue.')
      }),
    [queryClient],
  )

  return null
}
