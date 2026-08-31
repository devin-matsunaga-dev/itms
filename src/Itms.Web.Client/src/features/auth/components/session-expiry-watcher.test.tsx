import { describe, expect, it } from 'vitest'
import { QueryClient } from '@tanstack/react-query'
import { screen, waitFor } from '@testing-library/react'
import { Toaster } from '@/components/ui/sonner'
import { SessionExpiryWatcher } from '@/features/auth/components/session-expiry-watcher'
import { currentUserKey } from '@/features/auth/hooks/use-current-user'
import { ApiError, apiFetch } from '@/lib/api/client'
import { Roles } from '@/lib/roles'
import type { AuthenticatedUser } from '@/lib/api/generated-pending'
import { renderWithProviders } from '@/test/render'

const account: AuthenticatedUser = {
  id: '11111111-1111-1111-1111-111111111111',
  userName: 'tech',
  email: 'tech@itms.local',
  displayName: 'Casey Tran',
  roles: [Roles.technician],
  departmentId: null,
  locationId: null,
}

/** A 401 from any endpoint, the way a revoked session arrives mid-session. */
async function provokeUnauthorized(): Promise<void> {
  const originalFetch = globalThis.fetch
  globalThis.fetch = () =>
    Promise.resolve(
      new Response(JSON.stringify({ status: 401, code: 'auth.not_signed_in' }), {
        status: 401,
        headers: { 'content-type': 'application/problem+json' },
      }),
    )

  try {
    await apiFetch('/tickets')
  } catch (error) {
    if (!(error instanceof ApiError)) {
      throw error
    }
  } finally {
    globalThis.fetch = originalFetch
  }
}

describe('SessionExpiryWatcher', () => {
  it('signs the account out and says why when the session ends mid-use', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    queryClient.setQueryData(currentUserKey, account)

    renderWithProviders(
      <>
        <SessionExpiryWatcher />
        <Toaster />
      </>,
      { queryClient },
    )

    await provokeUnauthorized()

    await waitFor(() => {
      expect(queryClient.getQueryData(currentUserKey)).toBeNull()
    })
    expect(await screen.findByText('Your session ended. Sign in to continue.')).toBeInTheDocument()
  })

  it('says nothing when nobody was signed in to begin with', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })

    renderWithProviders(
      <>
        <SessionExpiryWatcher />
        <Toaster />
      </>,
      { queryClient },
    )

    await provokeUnauthorized()

    // The 401 from the very first /me call is the normal answer for a visitor who has
    // never signed in. Announcing it as an expiry would be a lie.
    expect(screen.queryByText('Your session ended. Sign in to continue.')).not.toBeInTheDocument()
  })
})
