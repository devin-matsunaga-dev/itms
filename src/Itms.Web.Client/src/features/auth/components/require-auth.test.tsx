import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useLocation } from 'react-router'
import { RequireAuth } from '@/features/auth/components/require-auth'
import { ApiError } from '@/lib/api/client'
import { Roles } from '@/lib/roles'
import type { AuthenticatedUser } from '@/lib/api/generated-pending'
import { renderWithProviders } from '@/test/render'

const fetchCurrentUser = vi.fn<() => Promise<AuthenticatedUser | null>>()

vi.mock('@/features/auth/api/auth-api', () => ({
  fetchCurrentUser: () => fetchCurrentUser(),
  login: vi.fn(),
  logout: vi.fn(),
}))

const signedIn: AuthenticatedUser = {
  id: '11111111-1111-1111-1111-111111111111',
  userName: 'tech',
  email: 'tech@itms.local',
  displayName: 'Casey Tran',
  roles: [Roles.technician],
  departmentId: null,
  locationId: null,
}

function renderGate(route: string) {
  return renderWithProviders(
    <Routes>
      <Route path="/login" element={<LoginProbe />} />
      <Route element={<RequireAuth />}>
        <Route path="/tickets" element={<p>The ticket queue</p>} />
      </Route>
    </Routes>,
    { route },
  )
}

/** Stands in for the login page and reports the address it was sent from. */
function LoginProbe(): React.JSX.Element {
  const state = useLocation().state as { from?: string } | null
  return (
    <div>
      <p>Sign in</p>
      <p data-testid="from">{state?.from ?? ''}</p>
    </div>
  )
}

beforeEach(() => {
  fetchCurrentUser.mockReset()
})

describe('RequireAuth', () => {
  it('renders the screen for a signed-in account', async () => {
    fetchCurrentUser.mockResolvedValue(signedIn)

    renderGate('/tickets')

    expect(await screen.findByText('The ticket queue')).toBeInTheDocument()
  })

  it('announces the wait while the session is being checked', () => {
    fetchCurrentUser.mockReturnValue(new Promise(() => undefined))

    renderGate('/tickets')

    expect(screen.getByRole('status')).toHaveTextContent('Checking your session')
  })

  it('sends an unauthenticated visitor to the login page', async () => {
    fetchCurrentUser.mockResolvedValue(null)

    renderGate('/tickets')

    expect(await screen.findByText('Sign in')).toBeInTheDocument()
  })

  it('says the server could not be reached rather than blaming the visitor', async () => {
    const person = userEvent.setup()
    fetchCurrentUser.mockRejectedValueOnce(new ApiError(503, null, 'Service Unavailable'))

    renderGate('/tickets')

    expect(await screen.findByText('ITMS could not be reached')).toBeInTheDocument()
    expect(screen.queryByText('Sign in')).not.toBeInTheDocument()

    // And the retry is real: the second answer puts them on the screen they asked for.
    fetchCurrentUser.mockResolvedValue(signedIn)
    await person.click(screen.getByRole('button', { name: /try again/i }))

    expect(await screen.findByText('The ticket queue')).toBeInTheDocument()
  })

  it('carries the address they asked for, so signing in resumes it', async () => {
    fetchCurrentUser.mockResolvedValue(null)

    renderGate('/tickets?status=open')

    expect(await screen.findByTestId('from')).toHaveTextContent('/tickets?status=open')
  })
})
