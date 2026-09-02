import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useLocation } from 'react-router'
import { UserDetailPage } from '@/features/users/routes/user-detail-page'
import { ApiError } from '@/lib/api/client'
import type {
  AssetStatus,
  AssetSummary,
  Department,
  Location,
  TicketActivity,
  UserSummary,
  UserTicketPage,
} from '@/lib/api/types'
import {
  department,
  heldAsset,
  room,
  seededStatuses,
  site,
  ticket,
  user,
} from '@/features/users/test/user-fixtures'
import { renderWithProviders } from '@/test/render'

const fetchUser = vi.fn<(id: string) => Promise<UserSummary>>()
const fetchUserAssets = vi.fn<(id: string) => Promise<AssetSummary[]>>()
const fetchUserTickets =
  vi.fn<(id: string, state: TicketActivity, pageSize: number) => Promise<UserTicketPage>>()

vi.mock('@/features/users/api/users-api', () => ({
  fetchUsers: vi.fn(),
  fetchUser: (id: string) => fetchUser(id),
  fetchUserAssets: (id: string) => fetchUserAssets(id),
  fetchUserTickets: (id: string, state: TicketActivity, pageSize: number) =>
    fetchUserTickets(id, state, pageSize),
}))

vi.mock('@/features/assets/api/assets-api', () => ({
  fetchAssetStatuses: (): Promise<AssetStatus[]> => Promise.resolve(seededStatuses),
  fetchAssets: vi.fn(),
  fetchAssetTypes: vi.fn(),
  fetchAssetHolders: vi.fn(),
  fetchAsset: vi.fn(),
  fetchAssetHistory: vi.fn(),
  fetchAssetTickets: vi.fn(),
}))

vi.mock('@/features/directory/api/directory-api', () => ({
  fetchDepartments: (): Promise<Department[]> => Promise.resolve([department]),
  fetchLocations: (): Promise<Location[]> => Promise.resolve([room]),
  fetchLocationRoots: (): Promise<Location[]> => Promise.resolve([site]),
  fetchLocationChildren: (): Promise<Location[]> => Promise.resolve([room]),
  searchLocations: (): Promise<Location[]> => Promise.resolve([room]),
  fetchLocationAncestors: (): Promise<Location[]> => Promise.resolve([site, room]),
}))

function ticketPage(items: ReturnType<typeof ticket>[], total = items.length): UserTicketPage {
  return { items, total, page: 1, pageSize: 10, totalPages: 1, hasNextPage: false }
}

function AddressProbe(): React.JSX.Element {
  const { pathname } = useLocation()
  return <output data-testid="path">{pathname}</output>
}

function renderUser(route = '/users/user-1') {
  return renderWithProviders(
    <Routes>
      <Route
        path="/users/:id"
        element={
          <>
            <UserDetailPage />
            <AddressProbe />
          </>
        }
      />
      <Route path="/users" element={<AddressProbe />} />
      <Route path="/assets/:id" element={<AddressProbe />} />
      <Route path="/tickets/:id" element={<AddressProbe />} />
    </Routes>,
    { route },
  )
}

beforeEach(() => {
  fetchUser.mockReset()
  fetchUserAssets.mockReset()
  fetchUserTickets.mockReset()

  fetchUser.mockResolvedValue(user())
  fetchUserAssets.mockResolvedValue([heldAsset()])
  fetchUserTickets.mockImplementation((_id, state) =>
    Promise.resolve(
      state === 'Open'
        ? ticketPage([ticket()])
        : ticketPage([ticket({ id: 'ticket-2', number: 'TKT-0002', subject: 'Old monitor swap', status: 'Closed', isOpen: false })]),
    ),
  )
})

describe('UserDetailPage', () => {
  it('answers the spec’s acceptance shape: the person, their equipment, their support history', async () => {
    // SPEC.md §4: "a technician searches a user and immediately sees their equipment and
    // support history".
    renderUser()

    expect(await screen.findByRole('heading', { name: 'Jane Santos', level: 1 })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'LAP-0042' })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'TKT-0007' })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'TKT-0002' })).toBeInTheDocument()
  })

  it('reads the two ticket panels as the complementary pair the server offers', async () => {
    renderUser()
    await screen.findByRole('link', { name: 'TKT-0007' })

    expect(fetchUserTickets).toHaveBeenCalledWith('user-1', 'Open', 10)
    expect(fetchUserTickets).toHaveBeenCalledWith('user-1', 'Past', 10)
  })

  it('names the room from the ancestor chain rather than from the flat list', async () => {
    // The chain is one request that is always right; the flat read is one page of two
    // hundred and can honestly not contain the room.
    renderUser()

    expect(await screen.findByText('CUC → Saipan Plant → Server Room')).toBeInTheDocument()
  })

  it('names the department the profile carries as an id', async () => {
    renderUser()

    expect(await screen.findByText('Information Technology')).toBeInTheDocument()
  })

  it('renders the equipment status as the word an administrator gave it, not the code', async () => {
    renderUser()

    expect(await screen.findByText('Deployed')).toBeInTheDocument()
    expect(screen.queryByText('deployed')).not.toBeInTheDocument()
  })

  it('says a deactivated account keeps its history', async () => {
    // Invariant 9 is the reason this screen still renders every panel for somebody who
    // has left.
    fetchUser.mockResolvedValue(user({ isActive: false }))
    renderUser()

    expect(await screen.findByText('Deactivated')).toBeInTheDocument()
    expect(
      screen.getByText(/tickets, comments, and equipment history are kept/i),
    ).toBeInTheDocument()
  })

  it('offers no way to change anything, because user administration is WP-5.8', async () => {
    renderUser()
    await screen.findByRole('heading', { name: 'Jane Santos', level: 1 })

    expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /deactivate/i })).not.toBeInTheDocument()
  })

  it('keeps one panel’s failure out of the others', async () => {
    fetchUserAssets.mockRejectedValue(new Error('network'))
    renderUser()

    expect(await screen.findByText('The equipment list could not be loaded.')).toBeInTheDocument()
    // The profile and the tickets are still there.
    expect(screen.getByRole('heading', { name: 'Jane Santos', level: 1 })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'TKT-0007' })).toBeInTheDocument()
  })

  it('says so plainly when there is no such person', async () => {
    fetchUser.mockRejectedValue(new ApiError(404, null, 'No such user.'))
    renderUser()

    expect(await screen.findByText('No such person')).toBeInTheDocument()
  })

  it('offers a retry when the profile could not be read for another reason', async () => {
    fetchUser.mockRejectedValue(new ApiError(500, null, 'boom'))
    renderUser()

    expect(await screen.findByText('This person could not be loaded.')).toBeInTheDocument()

    fetchUser.mockResolvedValue(user())
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('heading', { name: 'Jane Santos', level: 1 })).toBeInTheDocument()
  })

  it('carries the way back to the directory above the title', async () => {
    renderUser()

    await userEvent.click(await screen.findByRole('link', { name: 'Back to users' }))

    await waitFor(() => {
      expect(screen.getByTestId('path').textContent).toBe('/users')
    })
  })

  it('links each panel on to the screen built to work on those rows', async () => {
    renderUser()

    const viewAll = await screen.findAllByRole('link', { name: 'View all' })
    expect(viewAll.length).toBeGreaterThan(0)
    expect(viewAll.some((link) => link.getAttribute('href')?.includes('assignedToUserId=user-1'))).toBe(true)
    expect(viewAll.some((link) => link.getAttribute('href')?.includes('requesterId=user-1'))).toBe(true)
  })

  it('says what it is not showing when a panel has more than fits', async () => {
    fetchUserTickets.mockImplementation((_id, state) =>
      Promise.resolve(state === 'Open' ? ticketPage([ticket()], 42) : ticketPage([])),
    )
    renderUser()

    expect(await screen.findByText('Showing the 1 most recent of 42.')).toBeInTheDocument()
  })

  it('says plainly when somebody holds nothing and has raised nothing', async () => {
    fetchUserAssets.mockResolvedValue([])
    fetchUserTickets.mockResolvedValue(ticketPage([]))
    renderUser()

    expect(await screen.findByText('No equipment is issued to this person.')).toBeInTheDocument()
    expect(
      screen.getByText('This person has nothing open with the helpdesk.'),
    ).toBeInTheDocument()
    expect(screen.getByText('This person has no finished tickets.')).toBeInTheDocument()
  })
})
