import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useLocation } from 'react-router'
import { EditAssetPage } from '@/features/assets/routes/edit-asset-page'
import { ApiError } from '@/lib/api/client'
import type {
  Asset,
  AssetStatus,
  AssetType,
  Department,
  Location,
} from '@/lib/api/types'
import type { AssetRead } from '@/features/assets/api/assets-api'
import {
  asset,
  assetId,
  assetType,
  department,
  location,
  seededStatuses,
} from '@/features/assets/test/asset-fixtures'
import { renderWithProviders } from '@/test/render'

const fetchAsset = vi.fn<() => Promise<AssetRead>>()
const updateAsset = vi.fn<(...args: unknown[]) => Promise<Asset>>()
const toastError = vi.fn()
const toastSuccess = vi.fn()

vi.mock('sonner', () => ({
  toast: {
    error: (message: string, options?: unknown) => toastError(message, options),
    success: (message: string, options?: unknown) => toastSuccess(message, options),
  },
}))

vi.mock('@/features/assets/api/assets-api', () => ({
  fetchAsset: () => fetchAsset(),
  updateAsset: (...args: unknown[]) => updateAsset(...args),
  fetchAssetTypes: (): Promise<AssetType[]> => Promise.resolve([assetType()]),
  fetchAssetStatuses: (): Promise<AssetStatus[]> => Promise.resolve(seededStatuses),
  fetchAssets: vi.fn(),
  fetchAssetHistory: vi.fn(),
  fetchAssetTickets: vi.fn(),
  fetchAssetHolders: vi.fn(),
  createAsset: vi.fn(),
  assignAsset: vi.fn(),
  moveAsset: vi.fn(),
}))

vi.mock('@/features/directory/api/directory-api', () => ({
  fetchDepartments: (): Promise<Department[]> => Promise.resolve([department]),
  fetchLocations: (): Promise<Location[]> => Promise.resolve([location]),
}))

function PathProbe(): React.JSX.Element {
  const { pathname } = useLocation()
  return <output data-testid="path">{pathname}</output>
}

function renderForm() {
  return renderWithProviders(
    <Routes>
      <Route
        path="/assets/:id/edit"
        element={
          <>
            <EditAssetPage />
            <PathProbe />
          </>
        }
      />
      <Route path="/assets/:id" element={<PathProbe />} />
    </Routes>,
    { route: `/assets/${assetId}/edit` },
  )
}

function path(): string {
  return screen.getByTestId('path').textContent ?? ''
}

/** The read as the API layer answers it: the asset, and the tag its writes send back. */
function read(overrides: Partial<Asset> = {}, etag = '"41"'): AssetRead {
  return { asset: asset(overrides), etag }
}

beforeEach(() => {
  fetchAsset.mockReset()
  updateAsset.mockReset()
  fetchAsset.mockResolvedValue(read())
  updateAsset.mockResolvedValue(asset())
  toastError.mockReset()
  toastSuccess.mockReset()
})

describe('EditAssetPage', () => {
  it('opens holding what the asset already says', async () => {
    renderForm()

    expect(await screen.findByLabelText(/^Asset tag/)).toHaveValue('LAP-0042')
    expect(screen.getByLabelText(/^Name/)).toHaveValue('Jane’s laptop')
    expect(screen.getByLabelText(/^Serial number/)).toHaveValue('SN-99001')
    expect(screen.getByLabelText(/^Manufacturer/)).toHaveValue('Dell')
    expect(screen.getByLabelText(/^Cost/)).toHaveValue('1499.5')
    expect(screen.getByLabelText(/^Warranty expires/)).toHaveValue('2026-09-20')
  })

  /**
   * Invariant 4: the tag is immutable once created. DESIGN.md §4 says a field that is fixed
   * for the person reading it is shown read-only with the reason given, rather than hidden —
   * a form that quietly has different fields in different places is harder to trust.
   */
  it('shows the tag read-only rather than hiding it', async () => {
    renderForm()

    expect(await screen.findByLabelText(/^Asset tag/)).toHaveAttribute('readonly')
  })

  /**
   * A lifecycle move owes a history entry (invariant 5) and a domain event, and belongs to
   * the detail screen's actions. An edit that could set the column would route round both,
   * so the field is not on this form and `UpdateAssetRequest` has no place to put one.
   */
  it('offers no status and no holder', async () => {
    renderForm()

    await screen.findByLabelText(/^Asset tag/)
    expect(screen.queryByLabelText(/^Status/)).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/assigned/i)).not.toBeInTheDocument()
  })

  it('sends the changed fields and the tag it read the asset at', async () => {
    const person = userEvent.setup()
    renderForm()

    const name = await screen.findByLabelText(/^Name/)
    await person.clear(name)
    await person.type(name, 'Front desk PC')
    await person.click(screen.getByRole('button', { name: /save changes/i }))

    await waitFor(() => {
      expect(updateAsset).toHaveBeenCalledWith(
        assetId,
        expect.objectContaining({ name: 'Front desk PC', assetTypeId: 'type-laptop' }),
        '"41"',
      )
    })

    // The tag is not in the shape at all, so it cannot be sent even by accident.
    expect(updateAsset.mock.calls[0]?.[1]).not.toHaveProperty('assetTag')
    expect(updateAsset.mock.calls[0]?.[1]).not.toHaveProperty('assetStatusId')
  })

  /** A PUT is a full replacement: a field the operator empties is cleared, not kept. */
  it('clears a field the operator emptied', async () => {
    const person = userEvent.setup()
    renderForm()

    await person.clear(await screen.findByLabelText(/^Vendor/))
    await person.click(screen.getByRole('button', { name: /save changes/i }))

    await waitFor(() => {
      expect(updateAsset).toHaveBeenCalledWith(
        assetId,
        expect.objectContaining({ vendor: null }),
        '"41"',
      )
    })
  })

  it('goes back to the asset and says it saved', async () => {
    const person = userEvent.setup()
    renderForm()

    await screen.findByLabelText(/^Asset tag/)
    await person.click(screen.getByRole('button', { name: /save changes/i }))

    await waitFor(() => {
      expect(path()).toBe(`/assets/${assetId}`)
    })
    expect(toastSuccess).toHaveBeenCalledWith('LAP-0042 saved.', undefined)
  })

  /**
   * The point of stating a precondition: told that somebody else got there first, rather
   * than overwriting them. 412 and 409 read the same way to the person in front of it.
   */
  it.each([412, 409])('says the asset moved when the write is refused with %i', async (status) => {
    updateAsset.mockRejectedValue(new ApiError(status, null, 'stale'))

    const person = userEvent.setup()
    renderForm()

    await screen.findByLabelText(/^Asset tag/)
    await person.click(screen.getByRole('button', { name: /save changes/i }))

    await waitFor(() => {
      expect(toastError).toHaveBeenCalledWith(
        'This asset changed while you were editing it.',
        expect.objectContaining({
          description: 'Reload the asset, check what happened, and make the change again.',
        }),
      )
    })
  })

  /**
   * A duplicate serial is a 409 too, but it names a field the person can act on — so it
   * lands there rather than in the "somebody else moved it" message.
   */
  it('puts a duplicate serial on the serial field', async () => {
    updateAsset.mockRejectedValue(
      new ApiError(
        409,
        { code: 'assets.duplicate_serial_number' } as never,
        "Dell already has an asset with the serial number 'SN-99001'.",
      ),
    )

    const person = userEvent.setup()
    renderForm()

    await screen.findByLabelText(/^Asset tag/)
    await person.click(screen.getByRole('button', { name: /save changes/i }))

    expect(await screen.findByText(/already has an asset with the serial/)).toBeInTheDocument()
    expect(toastError).not.toHaveBeenCalled()
  })

  it('maps a server field error back onto its field', async () => {
    updateAsset.mockRejectedValue(
      new ApiError(
        400,
        {
          code: 'assets.location_not_found',
          errors: { locationId: ['No such location.'] },
        } as never,
        'Validation failed',
      ),
    )

    const person = userEvent.setup()
    renderForm()

    await screen.findByLabelText(/^Asset tag/)
    await person.click(screen.getByRole('button', { name: /save changes/i }))

    expect(await screen.findByText('No such location.')).toBeInTheDocument()
  })

  it('says what the server said when the asset is not there', async () => {
    fetchAsset.mockRejectedValue(new ApiError(404, null, 'Not found'))
    renderForm()

    expect(await screen.findByText('No such asset')).toBeInTheDocument()
  })

  it('offers a retry when the read failed for any other reason', async () => {
    fetchAsset.mockRejectedValue(new Error('network'))
    renderForm()

    expect(await screen.findByText('The asset could not be loaded.')).toBeInTheDocument()
  })

  it('offers a way back to the asset it is editing', async () => {
    renderForm()

    expect(await screen.findByRole('link', { name: 'Back to LAP-0042' })).toHaveAttribute(
      'href',
      `/assets/${assetId}`,
    )
  })
})
