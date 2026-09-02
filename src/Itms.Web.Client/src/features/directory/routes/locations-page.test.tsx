import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useLocation } from 'react-router'
import { LocationsPage } from '@/features/directory/routes/locations-page'
import { ApiError } from '@/lib/api/client'
import type {
  CreateLocationRequest,
  Location,
  LocationUsage,
  MoveLocationRequest,
} from '@/lib/api/types'
import {
  location,
  locationUsage,
  organisation,
  room,
} from '@/features/directory/test/directory-fixtures'
import { renderWithProviders } from '@/test/render'

const fetchLocationRoots = vi.fn<() => Promise<Location[]>>()
const fetchLocationChildren = vi.fn<(parentId: string) => Promise<Location[]>>()
const fetchLocationAncestors = vi.fn<(id: string) => Promise<Location[]>>()
const searchLocations = vi.fn<(term: string) => Promise<Location[]>>()
const fetchLocationSubtree = vi.fn<(id: string) => Promise<Location[]>>()
const fetchLocationUsage = vi.fn<(id: string) => Promise<LocationUsage>>()
const createLocation = vi.fn<(request: CreateLocationRequest) => Promise<Location>>()
const moveLocation = vi.fn<(id: string, request: MoveLocationRequest) => Promise<Location>>()
const deleteLocation = vi.fn<(id: string) => Promise<void>>()

vi.mock('@/features/directory/api/directory-api', () => ({
  fetchDepartments: vi.fn(),
  fetchDepartmentPage: vi.fn(),
  fetchDepartmentUsage: vi.fn(),
  createDepartment: vi.fn(),
  updateDepartment: vi.fn(),
  setDepartmentActive: vi.fn(),
  fetchLocations: vi.fn(),
  fetchLocationRoots: () => fetchLocationRoots(),
  fetchLocationChildren: (parentId: string) => fetchLocationChildren(parentId),
  searchLocations: (term: string) => searchLocations(term),
  fetchLocationAncestors: (id: string) => fetchLocationAncestors(id),
  fetchLocationSubtree: (id: string) => fetchLocationSubtree(id),
  fetchLocationUsage: (id: string) => fetchLocationUsage(id),
  createLocation: (request: CreateLocationRequest) => createLocation(request),
  updateLocation: vi.fn(),
  moveLocation: (id: string, request: MoveLocationRequest) => moveLocation(id, request),
  deleteLocation: (id: string) => deleteLocation(id),
}))

function AddressProbe(): React.JSX.Element {
  const { search } = useLocation()
  return <output data-testid="address">{search}</output>
}

function renderLocations(route = '/administration/locations') {
  return renderWithProviders(
    <Routes>
      <Route
        path="/administration/locations"
        element={
          <>
            <LocationsPage />
            <AddressProbe />
          </>
        }
      />
      <Route path="/administration" element={<AddressProbe />} />
    </Routes>,
    { route },
  )
}

function address(): string {
  return screen.getByTestId('address').textContent ?? ''
}

beforeEach(() => {
  fetchLocationRoots.mockReset()
  fetchLocationChildren.mockReset()
  fetchLocationAncestors.mockReset()
  searchLocations.mockReset()
  fetchLocationSubtree.mockReset()
  fetchLocationUsage.mockReset()
  createLocation.mockReset()
  moveLocation.mockReset()
  deleteLocation.mockReset()

  fetchLocationRoots.mockResolvedValue([organisation])
  fetchLocationChildren.mockResolvedValue([location()])
  fetchLocationAncestors.mockResolvedValue([organisation])
  searchLocations.mockResolvedValue([room])
  fetchLocationSubtree.mockResolvedValue([location()])
  fetchLocationUsage.mockResolvedValue(locationUsage())
  createLocation.mockResolvedValue(room)
  moveLocation.mockResolvedValue(location({ parentId: organisation.id }))
  deleteLocation.mockResolvedValue(undefined)
})

describe('LocationsPage', () => {
  it('opens on the top of the tree, one level at a time', async () => {
    renderLocations()

    expect(await screen.findByText('CUC')).toBeInTheDocument()
    expect(fetchLocationRoots).toHaveBeenCalled()
    // The whole tree is never asked for.
    expect(fetchLocationChildren).not.toHaveBeenCalled()
  })

  it('walks into a node and says so in the address', async () => {
    renderLocations()
    await screen.findByText('CUC')

    await userEvent.click(screen.getByRole('button', { name: /open/i }))

    await waitFor(() => {
      expect(address()).toContain('parent=loc-cuc')
    })
    expect(await screen.findByText('Saipan Plant')).toBeInTheDocument()
  })

  it('reads the level the address names, so a link into the tree lands there', async () => {
    renderLocations('/administration/locations?parent=loc-cuc')

    expect(await screen.findByText('Saipan Plant')).toBeInTheDocument()
    expect(fetchLocationChildren).toHaveBeenCalledWith('loc-cuc')
  })

  it('searches the whole tree rather than the level on screen', async () => {
    renderLocations()
    await screen.findByText('CUC')

    await userEvent.type(screen.getByRole('searchbox'), 'server')

    expect(await screen.findByText('Server Room')).toBeInTheDocument()
    await waitFor(() => {
      expect(searchLocations).toHaveBeenCalledWith('server')
    })
    // The full path is what a flat result is identified by.
    expect(screen.getByText('CUC → Saipan Plant → Server Room')).toBeInTheDocument()
  })

  it('creates a child under the node that is open', async () => {
    renderLocations('/administration/locations?parent=loc-cuc')
    await screen.findByText('Saipan Plant')

    await userEvent.click(screen.getByRole('button', { name: /new location in CUC/i }))
    await userEvent.type(await screen.findByLabelText(/^name/i), 'Rota Plant')
    await userEvent.click(screen.getByRole('button', { name: 'Create location' }))

    await waitFor(() => {
      expect(createLocation).toHaveBeenCalledWith({
        name: 'Rota Plant',
        kind: 'Room',
        parentId: 'loc-cuc',
        description: null,
      })
    })
  })

  it('puts the server’s hierarchy refusal on the level field', async () => {
    // The rule lives on the server (WP-2.4), and its message names the whole hierarchy.
    createLocation.mockRejectedValue(
      new ApiError(
        409,
        {
          code: 'directory.illegal_placement',
          detail:
            'A Building cannot sit under a Room. The hierarchy runs Organization, Site, Building, Floor or Area, Room.',
        },
        'A Building cannot sit under a Room.',
      ),
    )
    renderLocations('/administration/locations?parent=loc-cuc')
    await screen.findByText('Saipan Plant')

    await userEvent.click(screen.getByRole('button', { name: /new location in CUC/i }))
    await userEvent.type(await screen.findByLabelText(/^name/i), 'Nowhere')
    await userEvent.click(screen.getByRole('button', { name: 'Create location' }))

    expect(
      await screen.findByText(/The hierarchy runs Organization, Site, Building/),
    ).toBeInTheDocument()
  })

  it('shows a node’s level read-only when renaming, with the reason', async () => {
    // `UpdateLocationRequest` carries no kind, so a Room cannot quietly become a Building.
    renderLocations()
    await screen.findByText('CUC')

    await userEvent.click(screen.getByRole('button', { name: 'Rename' }))

    const level = await screen.findByLabelText(/^level/i)
    expect(level).toHaveAttribute('readonly')
    expect(level).toHaveValue('Organization')
  })

  it('excludes a node’s own subtree from the parents a move offers', async () => {
    renderLocations('/administration/locations?parent=loc-cuc')
    await screen.findByText('Saipan Plant')

    await userEvent.click(screen.getByRole('button', { name: 'Move' }))

    await waitFor(() => {
      // A node cannot move beneath itself, and the server refuses one that tries.
      expect(fetchLocationSubtree).toHaveBeenCalledWith('loc-plant')
    })
  })

  it('says what a location still holds before offering to delete it', async () => {
    renderLocations()
    await screen.findByText('CUC')

    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))

    expect(await screen.findByText('What it still holds')).toBeInTheDocument()
    await waitFor(() => {
      expect(fetchLocationUsage).toHaveBeenCalledWith('loc-cuc')
    })
  })

  it('refuses to offer the delete when the server has already said no', async () => {
    fetchLocationUsage.mockResolvedValue(
      locationUsage({
        childCount: 3,
        canDelete: false,
        references: [{ entityName: 'assets', count: 2 }],
        totalReferences: 2,
      }),
    )
    renderLocations()
    await screen.findByText('CUC')

    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))

    expect(await screen.findByText(/It contains 3 locations/)).toBeInTheDocument()

    const confirm = screen.getAllByRole('button', { name: 'Delete' }).at(-1)
    expect(confirm).toBeDisabled()
  })

  it('deletes a leaf nothing points at', async () => {
    renderLocations()
    await screen.findByText('CUC')

    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))
    await screen.findByText('What it still holds')

    const confirm = screen.getAllByRole('button', { name: 'Delete' }).at(-1)
    await userEvent.click(confirm as HTMLElement)

    await waitFor(() => {
      expect(deleteLocation).toHaveBeenCalledWith('loc-cuc')
    })
  })

  it('says plainly when a node contains nothing', async () => {
    fetchLocationChildren.mockResolvedValue([])
    renderLocations('/administration/locations?parent=loc-cuc')

    expect(await screen.findByText('Nothing is recorded in CUC')).toBeInTheDocument()
  })
})
