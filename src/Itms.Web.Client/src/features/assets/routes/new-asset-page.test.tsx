import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useLocation } from 'react-router'
import { NewAssetPage } from '@/features/assets/routes/new-asset-page'
import { ApiError } from '@/lib/api/client'
import type {
  Asset,
  AssetStatus,
  AssetType,
  CreateAssetRequest,
  Department,
  Location,
} from '@/lib/api/types'
import {
  asset,
  assetType,
  department,
  location,
  seededStatuses,
} from '@/features/assets/test/asset-fixtures'
import { renderWithProviders } from '@/test/render'

const createAsset = vi.fn<(request: CreateAssetRequest) => Promise<Asset>>()
const toastError = vi.fn()
const toastSuccess = vi.fn()

vi.mock('sonner', () => ({
  toast: {
    error: (message: string, options?: unknown) => toastError(message, options),
    success: (message: string, options?: unknown) => toastSuccess(message, options),
  },
}))

vi.mock('@/features/assets/api/assets-api', () => ({
  createAsset: (request: CreateAssetRequest) => createAsset(request),
  fetchAssetTypes: (): Promise<AssetType[]> => Promise.resolve([assetType()]),
  fetchAssetStatuses: (): Promise<AssetStatus[]> => Promise.resolve(seededStatuses),
  fetchAssets: vi.fn(),
  fetchAsset: vi.fn(),
  fetchAssetHistory: vi.fn(),
  fetchAssetTickets: vi.fn(),
  fetchAssetHolders: vi.fn(),
  updateAsset: vi.fn(),
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
        path="/assets/new"
        element={
          <>
            <NewAssetPage />
            <PathProbe />
          </>
        }
      />
      <Route path="/assets/:id" element={<PathProbe />} />
    </Routes>,
    { route: '/assets/new' },
  )
}

function path(): string {
  return screen.getByTestId('path').textContent ?? ''
}

beforeEach(() => {
  createAsset.mockReset()
  createAsset.mockResolvedValue(asset())
  toastError.mockReset()
  toastSuccess.mockReset()
})

describe('NewAssetPage', () => {
  it('sends what was typed, and nulls the fields left blank', async () => {
    const person = userEvent.setup()
    renderForm()

    await person.type(screen.getByLabelText(/^Asset tag/), 'LAP-0042')
    await person.click(screen.getByLabelText(/^Asset type/))
    await person.click(await screen.findByRole('option', { name: 'Laptop' }))
    await person.type(screen.getByLabelText(/^Name/), 'Reception desktop')
    await person.type(screen.getByLabelText(/^Cost/), '1499.50')

    await person.click(screen.getByRole('button', { name: /record asset/i }))

    await waitFor(() => {
      expect(createAsset).toHaveBeenCalledWith(
        expect.objectContaining({
          assetTag: 'LAP-0042',
          assetTypeId: 'type-laptop',
          name: 'Reception desktop',
          cost: 1499.5,
          // Empty is null on the wire, not an empty string: the column is nullable and a
          // blank serial must not reserve one.
          serialNumber: null,
          vendor: null,
          notes: null,
          departmentId: null,
          locationId: null,
          // Omitted status means the seeded In Stock, which is the server's own fallback.
          assetStatusId: null,
        }),
      )
    })
  })

  it('goes to the new asset and names it', async () => {
    const person = userEvent.setup()
    renderForm()

    await person.type(screen.getByLabelText(/^Asset tag/), 'LAP-0042')
    await person.click(screen.getByLabelText(/^Asset type/))
    await person.click(await screen.findByRole('option', { name: 'Laptop' }))
    await person.click(screen.getByRole('button', { name: /record asset/i }))

    await waitFor(() => {
      expect(path()).toBe('/assets/asset-1')
    })
    expect(toastSuccess).toHaveBeenCalledWith('LAP-0042 recorded.', undefined)
  })

  it('will not submit without a tag or a type', async () => {
    const person = userEvent.setup()
    renderForm()

    await person.click(screen.getByRole('button', { name: /record asset/i }))

    expect(await screen.findByText('Enter an asset tag.')).toBeInTheDocument()
    expect(screen.getByText('Choose an asset type.')).toBeInTheDocument()
    expect(createAsset).not.toHaveBeenCalled()
  })

  /**
   * Whitespace is what turns one tag into two when it is scanned, pasted, or put in a URL —
   * `AssetTagRules` refuses it, and the field says so in the same words rather than waiting
   * for a round trip.
   */
  it('refuses a tag containing a space at the field', async () => {
    const person = userEvent.setup()
    renderForm()

    await person.type(screen.getByLabelText(/^Asset tag/), 'LAP 0042')
    await person.click(screen.getByRole('button', { name: /record asset/i }))

    expect(await screen.findByText('An asset tag cannot contain spaces.')).toBeInTheDocument()
    expect(createAsset).not.toHaveBeenCalled()
  })

  it('refuses a cost that is not an amount', async () => {
    const person = userEvent.setup()
    renderForm()

    await person.type(screen.getByLabelText(/^Asset tag/), 'LAP-0042')
    await person.type(screen.getByLabelText(/^Cost/), 'about a thousand')
    await person.click(screen.getByRole('button', { name: /record asset/i }))

    expect(await screen.findByText('Enter an amount, like 1499.50')).toBeInTheDocument()
  })

  /**
   * A duplicate tag is a 409 with no per-field map — WP-2.1 chose a conflict because the
   * request is well formed and it is the state of the world that refuses it. The message
   * still belongs on the tag, because that is the field the person has to change.
   */
  it('puts a duplicate tag on the tag field rather than in a toast', async () => {
    createAsset.mockRejectedValue(
      new ApiError(
        409,
        { code: 'assets.duplicate_asset_tag' } as never,
        "An asset with the tag 'LAP-0042' already exists. An asset tag cannot be reused.",
      ),
    )

    const person = userEvent.setup()
    renderForm()

    await person.type(screen.getByLabelText(/^Asset tag/), 'LAP-0042')
    await person.click(screen.getByLabelText(/^Asset type/))
    await person.click(await screen.findByRole('option', { name: 'Laptop' }))
    await person.click(screen.getByRole('button', { name: /record asset/i }))

    expect(await screen.findByText(/already exists/)).toBeInTheDocument()
    expect(toastError).not.toHaveBeenCalled()
  })

  /**
   * `ProblemDetails` carries per-field messages keyed by camel-cased field name (WP-0.3),
   * which is exactly what a form needs — a retired type lands on the type select rather
   * than in a toast nobody can act on.
   */
  it('maps a server field error back onto its field', async () => {
    createAsset.mockRejectedValue(
      new ApiError(
        400,
        {
          code: 'assets.asset_type_retired',
          errors: { assetTypeId: ['That asset type has been retired. Choose another.'] },
        } as never,
        'Validation failed',
      ),
    )

    const person = userEvent.setup()
    renderForm()

    await person.type(screen.getByLabelText(/^Asset tag/), 'LAP-0042')
    await person.click(screen.getByLabelText(/^Asset type/))
    await person.click(await screen.findByRole('option', { name: 'Laptop' }))
    await person.click(screen.getByRole('button', { name: /record asset/i }))

    expect(
      await screen.findByText('That asset type has been retired. Choose another.'),
    ).toBeInTheDocument()
  })

  it('reports a failure that names no field in a toast', async () => {
    createAsset.mockRejectedValue(new Error('network'))

    const person = userEvent.setup()
    renderForm()

    await person.type(screen.getByLabelText(/^Asset tag/), 'LAP-0042')
    await person.click(screen.getByLabelText(/^Asset type/))
    await person.click(await screen.findByRole('option', { name: 'Laptop' }))
    await person.click(screen.getByRole('button', { name: /record asset/i }))

    await waitFor(() => {
      expect(toastError).toHaveBeenCalledWith(
        'The asset could not be recorded.',
        expect.objectContaining({ description: 'network' }),
      )
    })
  })

  /**
   * The create form offers the status, because booking in equipment that is already
   * deployed is recording a fact rather than making a transition — which is the line
   * `Asset.Create`'s own remarks draw. The edit form does not.
   */
  it('offers the status when recording', async () => {
    renderForm()

    expect(await screen.findByLabelText(/^Status/)).toBeInTheDocument()
  })

  it('offers no holder, because an assignment owes a history entry', async () => {
    renderForm()

    await screen.findByLabelText(/^Asset tag/)
    expect(screen.queryByLabelText(/assigned/i)).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/issue to/i)).not.toBeInTheDocument()
  })

  it('offers a way back to the register', async () => {
    renderForm()

    expect(await screen.findByRole('link', { name: 'Back to assets' })).toHaveAttribute(
      'href',
      '/assets',
    )
  })
})
