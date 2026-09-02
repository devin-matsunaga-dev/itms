import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useLocation } from 'react-router'
import { UsersPage } from '@/features/users/routes/users-page'
import type { Department, Location, PagedUsers } from '@/lib/api/types'
import type { UserQuery } from '@/features/users/lib/user-query'
import { department, room, site, user, usersPage } from '@/features/users/test/user-fixtures'
import { renderWithProviders } from '@/test/render'

const fetchUsers = vi.fn<(query: UserQuery) => Promise<PagedUsers>>()

vi.mock('@/features/users/api/users-api', () => ({
  fetchUsers: (query: UserQuery) => fetchUsers(query),
  fetchUser: vi.fn(),
  fetchUserAssets: vi.fn(),
  fetchUserTickets: vi.fn(),
}))

vi.mock('@/features/directory/api/directory-api', () => ({
  fetchDepartments: (): Promise<Department[]> => Promise.resolve([department]),
  fetchLocations: (): Promise<Location[]> => Promise.resolve([room]),
  fetchLocationRoots: (): Promise<Location[]> => Promise.resolve([site]),
  fetchLocationChildren: (): Promise<Location[]> => Promise.resolve([room]),
  searchLocations: (): Promise<Location[]> => Promise.resolve([room]),
  fetchLocationAncestors: (): Promise<Location[]> => Promise.resolve([site, room]),
}))

/** Reports the address the screen has navigated to, so the URL can be asserted on. */
function AddressProbe(): React.JSX.Element {
  const { pathname, search } = useLocation()
  return (
    <>
      <output data-testid="address">{search}</output>
      <output data-testid="path">{pathname}</output>
    </>
  )
}

function renderDirectory(route = '/users') {
  return renderWithProviders(
    <Routes>
      <Route
        path="/users"
        element={
          <>
            <UsersPage />
            <AddressProbe />
          </>
        }
      />
      {/* The 360 is `user-detail-page.test.tsx`'s; here it only has to be somewhere for a
          row to land. */}
      <Route path="/users/:id" element={<AddressProbe />} />
    </Routes>,
    { route },
  )
}

function path(): string {
  return screen.getByTestId('path').textContent ?? ''
}

function address(): string {
  return screen.getByTestId('address').textContent ?? ''
}

beforeEach(() => {
  fetchUsers.mockReset()
  fetchUsers.mockResolvedValue(usersPage([user()]))
})

describe('UsersPage', () => {
  it('writes the ordering into the address on arrival, so a bare /users is linkable', async () => {
    renderDirectory()

    await waitFor(() => {
      expect(address()).toContain('sort=DisplayName')
    })
    expect(address()).toContain('direction=Ascending')
    expect(address()).toContain('pageSize=25')
    expect(address()).not.toContain('page=1')
  })

  it('renders a person as a name over their address', async () => {
    renderDirectory()

    expect(await screen.findByRole('button', { name: 'Jane Santos' })).toBeInTheDocument()
    expect(screen.getByText('jane.santos@itms.local')).toBeInTheDocument()
  })

  it('resolves the department and the room the row names by id', async () => {
    renderDirectory()

    expect(await screen.findByText('Information Technology')).toBeInTheDocument()
    expect(screen.getByText('CUC → Saipan Plant → Server Room')).toBeInTheDocument()
  })

  it('says "not listed" for a room the flat read did not contain', async () => {
    // The flat location read is one page of two hundred. An em dash would claim the
    // person has no location, which is a different and false statement.
    fetchUsers.mockResolvedValue(usersPage([user({ locationId: 'loc-elsewhere' })]))
    renderDirectory()

    expect(await screen.findByText('Not listed')).toBeInTheDocument()
  })

  it('shows whether the account can still sign in', async () => {
    fetchUsers.mockResolvedValue(usersPage([user(), user({ id: 'user-2', displayName: 'Ex Employee', isActive: false })]))
    renderDirectory()

    expect(await screen.findByText('Active')).toBeInTheDocument()
    expect(screen.getByText('Deactivated')).toBeInTheDocument()
  })

  it('asks the server for the filters in the address', async () => {
    renderDirectory('/users?role=Technician&includeInactive=true&search=santos')

    await waitFor(() => {
      expect(fetchUsers).toHaveBeenCalled()
    })

    const asked = fetchUsers.mock.calls.at(-1)?.[0]
    expect(asked?.role).toBe('Technician')
    expect(asked?.includeInactive).toBe(true)
    expect(asked?.search).toBe('santos')
  })

  it('puts a role filter into the address rather than filtering in the browser', async () => {
    renderDirectory()
    await screen.findByRole('button', { name: 'Jane Santos' })

    await userEvent.click(screen.getByRole('combobox', { name: 'Role' }))
    await userEvent.click(await screen.findByRole('option', { name: 'Technicians' }))

    await waitFor(() => {
      expect(address()).toContain('role=Technician')
    })
  })

  it('returns to page one when a filter moves', async () => {
    renderDirectory('/users?page=4&sort=DisplayName&direction=Ascending&pageSize=25')
    await screen.findByRole('button', { name: 'Jane Santos' })

    await userEvent.click(screen.getByRole('combobox', { name: 'Role' }))
    await userEvent.click(await screen.findByRole('option', { name: 'End users' }))

    await waitFor(() => {
      expect(address()).not.toContain('page=4')
    })
  })

  it('opens the person the row names', async () => {
    renderDirectory()

    await userEvent.click(await screen.findByRole('button', { name: 'Jane Santos' }))

    await waitFor(() => {
      expect(path()).toBe('/users/user-1')
    })
  })

  it('reverses the ordering when the sorted column is clicked again', async () => {
    renderDirectory()
    await screen.findByRole('button', { name: 'Jane Santos' })

    await userEvent.click(screen.getByRole('button', { name: /person/i }))

    await waitFor(() => {
      expect(address()).toContain('direction=Descending')
    })
  })

  it('offers no create action, because user administration does not exist yet', async () => {
    // WP-5.8 owns creating an account, and WP-1.11 settled that a control which silently
    // does nothing is worse than one that is absent.
    renderDirectory()
    await screen.findByRole('button', { name: 'Jane Santos' })

    expect(screen.queryByRole('button', { name: /new user/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /new user/i })).not.toBeInTheDocument()
  })

  it('says the filters are what emptied the list, and offers to clear them', async () => {
    fetchUsers.mockResolvedValue(usersPage([]))
    renderDirectory('/users?role=Admin&sort=DisplayName&direction=Ascending&pageSize=25')

    expect(await screen.findByText('Nobody matches these filters')).toBeInTheDocument()

    // Two controls carry these words: the filter bar's, and the empty state's own — which
    // DESIGN.md §4 asks for deliberately ("Clear all sits at the end of the bar and is the
    // same words the empty state uses"). The empty state's is the last one rendered.
    const clears = screen.getAllByRole('button', { name: 'Clear all' })
    await userEvent.click(clears[clears.length - 1] as HTMLElement)

    await waitFor(() => {
      expect(address()).not.toContain('role=Admin')
    })
  })

  it('distinguishes an empty directory from a filtered one', async () => {
    fetchUsers.mockResolvedValue(usersPage([]))
    renderDirectory()

    expect(await screen.findByText('No people yet')).toBeInTheDocument()
  })

  it('offers a retry when the directory cannot be read', async () => {
    fetchUsers.mockRejectedValue(new Error('network'))
    renderDirectory()

    expect(await screen.findByText('The user directory could not be loaded.')).toBeInTheDocument()

    fetchUsers.mockResolvedValue(usersPage([user()]))
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('button', { name: 'Jane Santos' })).toBeInTheDocument()
  })

  it('states the total the server reports, not the size of the page', async () => {
    fetchUsers.mockResolvedValue(usersPage([user()], 214))
    renderDirectory()

    expect(await screen.findByText('214 people')).toBeInTheDocument()
    // The footer states the page's own range against the total, which is what makes the
    // arrows mean something.
    expect(screen.getByText('1–25 of 214 people')).toBeInTheDocument()
  })
})
