import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useLocation } from 'react-router'
import { AssetsPage } from '@/features/assets/routes/assets-page'
import type {
  AssetStatus,
  AssetType,
  Department,
  Location,
  PagedAssets,
  UserSummary,
} from '@/lib/api/types'
import type { AssetQuery } from '@/features/assets/lib/asset-query'
import {
  assetListItem,
  assetType,
  department,
  holder,
  location,
  seededStatuses,
} from '@/features/assets/test/asset-fixtures'
import { renderWithProviders } from '@/test/render'

const fetchAssets = vi.fn<(query: AssetQuery) => Promise<PagedAssets>>()

vi.mock('@/features/assets/api/assets-api', () => ({
  fetchAssets: (query: AssetQuery) => fetchAssets(query),
  fetchAssetTypes: (): Promise<AssetType[]> => Promise.resolve([assetType()]),
  fetchAssetStatuses: (): Promise<AssetStatus[]> => Promise.resolve(seededStatuses),
  fetchAssetHolders: (): Promise<UserSummary[]> => Promise.resolve([holder]),
  fetchAsset: vi.fn(),
  fetchAssetHistory: vi.fn(),
  fetchAssetTickets: vi.fn(),
}))

vi.mock('@/features/directory/api/directory-api', () => ({
  fetchDepartments: (): Promise<Department[]> => Promise.resolve([department]),
  fetchLocations: (): Promise<Location[]> => Promise.resolve([location]),
}))

function page(items: PagedAssets['items'], total = items.length): PagedAssets {
  return { items, total, page: 1, pageSize: 25, totalPages: 1, hasNextPage: false }
}

/** Reports the address the screen has navigated to, so the URL can be asserted on. */
function AddressProbe(): React.JSX.Element {
  const { search } = useLocation()
  return <output data-testid="address">{search}</output>
}

function renderRegister(route = '/assets') {
  return renderWithProviders(
    <Routes>
      <Route
        path="/assets"
        element={
          <>
            <AssetsPage />
            <AddressProbe />
          </>
        }
      />
    </Routes>,
    { route },
  )
}

function address(): string {
  return screen.getByTestId('address').textContent ?? ''
}

beforeEach(() => {
  window.localStorage.clear()
  fetchAssets.mockReset()
  fetchAssets.mockResolvedValue(page([assetListItem()]))
})

describe('AssetsPage', () => {
  it('writes the ordering into the address on arrival, so a bare /assets is linkable', async () => {
    // WP-1.9's call, applied here: an address that says what it is sorted by survives a
    // later change to the API's default.
    renderRegister()

    await waitFor(() => {
      expect(address()).toContain('sort=AssetTag')
    })
    expect(address()).toContain('direction=Ascending')
    expect(address()).toContain('pageSize=25')
    expect(address()).not.toContain('page=1')
  })

  it('renders the asset tag over what the machine is called', async () => {
    renderRegister()

    expect(await screen.findByRole('button', { name: 'LAP-0042' })).toBeInTheDocument()
    expect(screen.getByText('Jane’s laptop')).toBeInTheDocument()
    expect(screen.getByText('Deployed')).toBeInTheDocument()
  })

  it('falls back to make and model when the asset has no name', async () => {
    fetchAssets.mockResolvedValue(page([assetListItem({ name: null })]))
    renderRegister()

    expect(await screen.findByText('Dell Latitude 5430')).toBeInTheDocument()
  })

  it('hides serial and department by default and draws the rest', async () => {
    renderRegister()

    await screen.findByRole('button', { name: 'LAP-0042' })

    expect(screen.queryByRole('columnheader', { name: /serial number/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('columnheader', { name: /department/i })).not.toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: /location/i })).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: /warranty/i })).toBeInTheDocument()
  })

  it('never offers a column for the five fields the list contract withholds', async () => {
    // WP-2.3 kept cost, notes, barcode, vendor, and the purchase date off the row, and the
    // human upheld it: an inventory list is the thing somebody screenshots.
    renderRegister()

    await screen.findByRole('button', { name: 'LAP-0042' })
    await userEvent.click(screen.getByRole('button', { name: 'Columns' }))

    for (const withheld of [/cost/i, /notes/i, /barcode/i, /vendor/i, /purchase/i]) {
      expect(screen.queryByRole('checkbox', { name: withheld })).not.toBeInTheDocument()
    }
  })

  it('writes a status filter into the address as a code, not an id', async () => {
    // A code is the same in every deployment, so the link survives a restore (WP-2.3).
    renderRegister()

    await screen.findByRole('button', { name: 'LAP-0042' })
    await userEvent.click(screen.getByRole('combobox', { name: 'Status' }))
    await userEvent.click(await screen.findByRole('option', { name: 'Repair' }))

    await waitFor(() => {
      expect(address()).toContain('statusCode=repair')
    })
    expect(address()).not.toContain('assetStatusId')
  })

  it('writes both warranty parameters from the one control', async () => {
    renderRegister()

    await screen.findByRole('button', { name: 'LAP-0042' })
    await userEvent.click(screen.getByRole('combobox', { name: 'Warranty' }))
    await userEvent.click(await screen.findByRole('option', { name: 'Expired or expiring in 30 days' }))

    // The pairing the server unions rather than narrows.
    await waitFor(() => {
      expect(address()).toContain('warrantyExpiringInDays=30')
    })
    expect(address()).toContain('warrantyExpired=true')
  })

  it('returns to page one when a filter changes', async () => {
    renderRegister('/assets?sort=AssetTag&direction=Ascending&page=3&pageSize=25')

    await screen.findByRole('button', { name: 'LAP-0042' })
    await userEvent.click(screen.getByRole('combobox', { name: 'Type' }))
    await userEvent.click(await screen.findByRole('option', { name: 'Laptop' }))

    await waitFor(() => {
      expect(address()).toContain('assetTypeId=type-laptop')
    })
    expect(address()).not.toContain('page=3')
  })

  it('asks the server for exactly what the address says', async () => {
    renderRegister('/assets?statusCode=repair&search=LAP&sort=Status&direction=Descending&pageSize=50')

    await waitFor(() => {
      expect(fetchAssets).toHaveBeenCalled()
    })

    const query = fetchAssets.mock.calls.at(-1)?.[0]
    expect(query?.statusCode).toEqual(['repair'])
    expect(query?.search).toBe('LAP')
    expect(query?.sort).toBe('Status')
    expect(query?.direction).toBe('Descending')
    expect(query?.pageSize).toBe(50)
  })

  it('reverses the ordering when the sorted column is clicked again', async () => {
    renderRegister()

    await screen.findByRole('button', { name: 'LAP-0042' })
    await userEvent.click(screen.getByRole('button', { name: /^asset/i }))

    await waitFor(() => {
      expect(address()).toContain('direction=Descending')
    })
  })

  it('says the register is empty when nothing has been recorded, and offers no action', async () => {
    // `WP-2.6b` writes the create form. A button navigating to a route that resolves to
    // the in-shell 404 is what WP-1.9 declined to ship.
    fetchAssets.mockResolvedValue(page([]))
    renderRegister()

    expect(await screen.findByText('No assets yet')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /record the first asset/i })).not.toBeInTheDocument()
  })

  it('distinguishes an empty register from an over-narrowed one, and offers to clear', async () => {
    fetchAssets.mockResolvedValue(page([]))
    renderRegister('/assets?search=nothing&sort=AssetTag&direction=Ascending&pageSize=25')

    expect(await screen.findByText('No assets match these filters')).toBeInTheDocument()

    // Two of them, deliberately: DESIGN.md §4 puts `Clear all` at the end of the filter
    // bar and has the empty state offer "the same action a second time", in the same
    // words. The second is the one inside the empty state.
    const clear = screen.getAllByRole('button', { name: 'Clear all' })
    expect(clear).toHaveLength(2)
    await userEvent.click(clear[1] as HTMLElement)

    await waitFor(() => {
      expect(address()).not.toContain('search=')
    })
  })

  it('states what failed and offers a retry', async () => {
    fetchAssets.mockRejectedValue(new Error('network'))
    renderRegister()

    const alert = await screen.findByRole('alert')
    expect(within(alert).getByText('The asset register could not be loaded.')).toBeInTheDocument()

    fetchAssets.mockResolvedValue(page([assetListItem()]))
    await userEvent.click(within(alert).getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('button', { name: 'LAP-0042' })).toBeInTheDocument()
  })

  it('counts only the filters the popover holds', async () => {
    renderRegister('/assets?statusCode=repair&departmentId=dep-it&sort=AssetTag&direction=Ascending&pageSize=25')

    await screen.findByRole('button', { name: 'LAP-0042' })

    // Status is inline and speaks for itself; the department is behind the button.
    const filters = screen.getByRole('button', { name: /filters/i })
    expect(within(filters).getByText('1')).toBeInTheDocument()
  })
})
