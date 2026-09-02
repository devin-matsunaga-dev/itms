import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { LocationPicker } from '@/features/directory/components/location-picker'
import type { Location, LocationKind } from '@/lib/api/types'
import { location, organisation, room } from '@/features/directory/test/directory-fixtures'
import { renderWithProviders } from '@/test/render'

const fetchLocationRoots = vi.fn<(adoptableFor?: LocationKind) => Promise<Location[]>>()
const fetchLocationChildren =
  vi.fn<(parentId: string, adoptableFor?: LocationKind) => Promise<Location[]>>()
const searchLocations = vi.fn<(term: string, adoptableFor?: LocationKind) => Promise<Location[]>>()
const fetchLocationAncestors = vi.fn<(id: string) => Promise<Location[]>>()

vi.mock('@/features/directory/api/directory-api', () => ({
  fetchLocationRoots: (adoptableFor?: LocationKind) => fetchLocationRoots(adoptableFor),
  fetchLocationChildren: (parentId: string, adoptableFor?: LocationKind) =>
    fetchLocationChildren(parentId, adoptableFor),
  searchLocations: (term: string, adoptableFor?: LocationKind) => searchLocations(term, adoptableFor),
  fetchLocationAncestors: (id: string) => fetchLocationAncestors(id),
  fetchDepartments: vi.fn(),
  fetchDepartmentPage: vi.fn(),
  fetchDepartmentUsage: vi.fn(),
  createDepartment: vi.fn(),
  updateDepartment: vi.fn(),
  setDepartmentActive: vi.fn(),
  fetchLocations: vi.fn(),
  fetchLocationSubtree: vi.fn(),
  fetchLocationUsage: vi.fn(),
  createLocation: vi.fn(),
  updateLocation: vi.fn(),
  moveLocation: vi.fn(),
  deleteLocation: vi.fn(),
}))

beforeEach(() => {
  fetchLocationRoots.mockReset()
  fetchLocationChildren.mockReset()
  searchLocations.mockReset()
  fetchLocationAncestors.mockReset()

  fetchLocationRoots.mockResolvedValue([organisation])
  fetchLocationChildren.mockResolvedValue([location()])
  searchLocations.mockResolvedValue([room])
  fetchLocationAncestors.mockResolvedValue([organisation, location(), room])
})

function renderPicker(props: Partial<React.ComponentProps<typeof LocationPicker>> = {}) {
  const onValueChange = vi.fn()

  renderWithProviders(
    <LocationPicker
      id="test-location"
      value={null}
      placeholder="Any location"
      onValueChange={onValueChange}
      {...props}
    />,
  )

  return { onValueChange }
}

describe('LocationPicker', () => {
  it('asks for one level at a time rather than the whole tree', async () => {
    renderPicker()

    await userEvent.click(screen.getByRole('button', { name: /any location/i }))

    expect(await screen.findByText('CUC')).toBeInTheDocument()
    expect(fetchLocationRoots).toHaveBeenCalled()
    expect(fetchLocationChildren).not.toHaveBeenCalled()
  })

  it('drills into a node with the chevron and selects with the row', async () => {
    const { onValueChange } = renderPicker()

    await userEvent.click(screen.getByRole('button', { name: /any location/i }))
    await userEvent.click(await screen.findByRole('button', { name: 'Open CUC' }))

    // The row's accessible name starts with the node's name and ends with its level; the
    // chevron beside it is "Open …", which is what makes the two reachable separately.
    const child = await screen.findByRole('button', { name: /^Saipan Plant/ })
    await userEvent.click(child)

    expect(onValueChange).toHaveBeenCalledWith('loc-plant')
  })

  it('offers a way back up the levels it walked', async () => {
    renderPicker()

    await userEvent.click(screen.getByRole('button', { name: /any location/i }))
    await userEvent.click(await screen.findByRole('button', { name: 'Open CUC' }))
    await screen.findByRole('button', { name: /^Saipan Plant/ })

    await userEvent.click(screen.getByRole('button', { name: 'All locations' }))

    expect(await screen.findByRole('button', { name: /^CUCOrganization/ })).toBeInTheDocument()
  })

  it('turns into a flat search of the whole tree the moment anything is typed', async () => {
    renderPicker()

    await userEvent.click(screen.getByRole('button', { name: /any location/i }))
    await userEvent.type(await screen.findByRole('searchbox'), 'server')

    expect(await screen.findByText('CUC → Saipan Plant → Server Room')).toBeInTheDocument()
    await waitFor(() => {
      expect(searchLocations).toHaveBeenCalledWith('server', undefined)
    })
  })

  it('shows the chosen room’s full path on the trigger', async () => {
    renderPicker({ value: room.id })

    expect(
      await screen.findByRole('button', { name: /CUC → Saipan Plant → Server Room/ }),
    ).toBeInTheDocument()
    expect(fetchLocationAncestors).toHaveBeenCalledWith('loc-server')
  })

  it('clears the value, which is a real answer rather than an absence', async () => {
    const { onValueChange } = renderPicker({ value: room.id })
    await screen.findByRole('button', { name: /Server Room/ })

    await userEvent.click(screen.getByRole('button', { name: 'Clear the location' }))

    expect(onValueChange).toHaveBeenCalledWith(null)
  })

  it('passes the hierarchy question to the server rather than answering it', async () => {
    // WP-2.4 resolved `adoptableFor` server-side precisely so a picker filtering
    // client-side would not become a second copy of the hierarchy rule.
    renderPicker({ adoptableFor: 'Room' })

    await userEvent.click(screen.getByRole('button', { name: /any location/i }))
    await screen.findByText('CUC')

    expect(fetchLocationRoots).toHaveBeenCalledWith('Room')
  })

  it('never offers an id the caller excluded', async () => {
    renderPicker({ excludedIds: [organisation.id] })

    await userEvent.click(screen.getByRole('button', { name: /any location/i }))

    await waitFor(() => {
      expect(fetchLocationRoots).toHaveBeenCalled()
    })
    expect(screen.queryByText('CUC')).not.toBeInTheDocument()
  })

  it('says a level is empty rather than leaving a chevron that does nothing', async () => {
    fetchLocationChildren.mockResolvedValue([])
    renderPicker({ adoptableFor: 'Floor' })

    await userEvent.click(screen.getByRole('button', { name: /any location/i }))
    await userEvent.click(await screen.findByRole('button', { name: 'Open CUC' }))

    expect(await screen.findByText('Nothing here can hold a floor.')).toBeInTheDocument()
  })
})
