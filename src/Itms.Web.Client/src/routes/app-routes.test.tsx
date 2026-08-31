import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AppRoutes } from '@/routes/app-routes'
import { navItems } from '@/routes/navigation'
import { Roles } from '@/lib/roles'
import type { AuthenticatedUser } from '@/lib/api/generated-pending'
import { renderWithProviders } from '@/test/render'

const fetchCurrentUser = vi.fn<() => Promise<AuthenticatedUser | null>>()
const logout = vi.fn<() => Promise<void>>()

vi.mock('@/features/auth/api/auth-api', () => ({
  fetchCurrentUser: () => fetchCurrentUser(),
  logout: () => logout(),
  login: vi.fn(),
}))

function account(roles: string[]): AuthenticatedUser {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    userName: 'someone',
    email: 'someone@itms.local',
    displayName: 'Casey Tran',
    roles,
    departmentId: null,
    locationId: null,
  }
}

beforeEach(() => {
  fetchCurrentUser.mockReset()
  logout.mockReset()
})

describe('routing and the role gate', () => {
  it('renders every listed destination for an Admin', async () => {
    fetchCurrentUser.mockResolvedValue(account([Roles.admin]))

    for (const item of navItems) {
      const { unmount } = renderWithProviders(<AppRoutes />, { route: item.path })
      // Every nav item has a route: none of them falls through to the 404.
      await waitFor(() => {
        expect(screen.queryByText('Page not found')).not.toBeInTheDocument()
      })
      expect(await screen.findByRole('heading', { level: 1 })).toBeInTheDocument()
      unmount()
    }
  })

  it('refuses a typed address the role is not offered', async () => {
    fetchCurrentUser.mockResolvedValue(account([Roles.user]))

    renderWithProviders(<AppRoutes />, { route: '/administration' })

    expect(await screen.findByText('You do not have access to this screen')).toBeInTheDocument()
    expect(screen.queryByText('Nothing to administer yet')).not.toBeInTheDocument()
  })

  it('lets a Technician reach the operational screens', async () => {
    fetchCurrentUser.mockResolvedValue(account([Roles.technician]))

    renderWithProviders(<AppRoutes />, { route: '/assets' })

    expect(await screen.findByRole('heading', { level: 1, name: 'Assets' })).toBeInTheDocument()
  })

  it('refuses administration to a Technician', async () => {
    fetchCurrentUser.mockResolvedValue(account([Roles.technician]))

    renderWithProviders(<AppRoutes />, { route: '/administration' })

    expect(await screen.findByText('You do not have access to this screen')).toBeInTheDocument()
  })

  it('renders a real not-found page inside the shell for an unknown address', async () => {
    fetchCurrentUser.mockResolvedValue(account([Roles.admin]))

    renderWithProviders(<AppRoutes />, { route: '/nowhere' })

    expect(await screen.findByText('Nothing lives at this address')).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument()
  })

  it('sends an unauthenticated visitor to the login page', async () => {
    fetchCurrentUser.mockResolvedValue(null)

    renderWithProviders(<AppRoutes />, { route: '/tickets' })

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument()
  })

  it('signs out and returns to the login page', async () => {
    const person = userEvent.setup()
    fetchCurrentUser.mockResolvedValue(account([Roles.admin]))
    logout.mockResolvedValue(undefined)

    renderWithProviders(<AppRoutes />, { route: '/' })

    await person.click(await screen.findByRole('button', { name: /casey tran/i }))

    // From here the session no longer exists, which is what the server would say to
    // the refetch the cleared cache triggers.
    fetchCurrentUser.mockResolvedValue(null)
    await person.click(await screen.findByRole('menuitem', { name: /sign out/i }))

    expect(logout).toHaveBeenCalledOnce()
    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeInTheDocument()
  })
})
