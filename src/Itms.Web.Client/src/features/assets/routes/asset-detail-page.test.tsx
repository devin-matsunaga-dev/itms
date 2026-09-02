import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { AssetDetailPage } from '@/features/assets/routes/asset-detail-page'
import { ApiError } from '@/lib/api/client'
import { formatDate } from '@/lib/datetime'
import type {
  Asset,
  PagedAssetHistory,
  PagedTicketSummaries,
  UserSummary,
} from '@/lib/api/types'
import type { AssetRead } from '@/features/assets/api/assets-api'
import {
  asset,
  assetId,
  holder,
  historyEntry,
  ticketSummary,
} from '@/features/assets/test/asset-fixtures'
import { renderWithProviders } from '@/test/render'

const toastError = vi.fn()
const toastSuccess = vi.fn()

vi.mock('sonner', () => ({
  toast: {
    error: (message: string, options?: unknown) => toastError(message, options),
    success: (message: string, options?: unknown) => toastSuccess(message, options),
  },
}))

const fetchAsset = vi.fn<() => Promise<AssetRead>>()
const fetchAssetHistory = vi.fn<() => Promise<PagedAssetHistory>>()
const fetchAssetTickets = vi.fn<() => Promise<PagedTicketSummaries>>()
const assignAsset = vi.fn<(...args: unknown[]) => Promise<Asset>>()
const moveAsset = vi.fn<(...args: unknown[]) => Promise<Asset>>()

vi.mock('@/features/assets/api/assets-api', () => ({
  fetchAsset: () => fetchAsset(),
  fetchAssetHistory: () => fetchAssetHistory(),
  fetchAssetTickets: () => fetchAssetTickets(),
  fetchAssetHolders: (): Promise<UserSummary[]> => Promise.resolve([holder, otherPerson]),
  assignAsset: (...args: unknown[]) => assignAsset(...args),
  moveAsset: (...args: unknown[]) => moveAsset(...args),
  fetchAssets: vi.fn(),
  fetchAssetTypes: vi.fn(),
  fetchAssetStatuses: vi.fn(),
  createAsset: vi.fn(),
  updateAsset: vi.fn(),
}))

/** Somebody a transfer can go to who is not already holding the asset. */
const otherPerson: UserSummary = { ...holder, id: 'user-2', displayName: 'Mark Reyes' }

/** The read as the API layer answers it: the asset, and the tag its writes send back. */
function read(overrides: Partial<Asset> = {}, etag = '"41"'): AssetRead {
  return { asset: asset(overrides), etag }
}

function historyPage(items: PagedAssetHistory['items'], total = items.length): PagedAssetHistory {
  return { items, total, page: 1, pageSize: 50, totalPages: 1, hasNextPage: false }
}

function ticketsPage(
  items: PagedTicketSummaries['items'],
  total = items.length,
): PagedTicketSummaries {
  return { items, total, page: 1, pageSize: 20, totalPages: 1, hasNextPage: false }
}

function renderDetail() {
  return renderWithProviders(
    <Routes>
      <Route path="/assets/:id" element={<AssetDetailPage />} />
    </Routes>,
    { route: `/assets/${assetId}` },
  )
}

beforeEach(() => {
  fetchAsset.mockReset()
  fetchAssetHistory.mockReset()
  fetchAssetTickets.mockReset()
  assignAsset.mockReset()
  moveAsset.mockReset()
  assignAsset.mockResolvedValue(asset())
  moveAsset.mockResolvedValue(asset())
  toastError.mockReset()
  toastSuccess.mockReset()

  fetchAsset.mockResolvedValue(read())
  fetchAssetHistory.mockResolvedValue(historyPage([]))
  fetchAssetTickets.mockResolvedValue(ticketsPage([]))
})

describe('AssetDetailPage', () => {
  it('leads with what the machine is called and names the tag beneath it', async () => {
    renderDetail()

    expect(await screen.findByRole('heading', { name: 'Jane’s laptop' })).toBeInTheDocument()
    expect(screen.getByText(/LAP-0042 · Laptop · Recorded/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to assets' })).toHaveAttribute('href', '/assets')
  })

  it('shows the five fields the register deliberately withholds', async () => {
    // WP-2.3 kept cost, notes, barcode, vendor, and the purchase date off the list row,
    // and the human upheld it. This is the screen where one asset is read on purpose, so
    // this is where they belong — and the only place they appear.
    renderDetail()

    await screen.findByRole('heading', { name: 'Jane’s laptop' })

    expect(screen.getByText('BC-4410')).toBeInTheDocument()
    expect(screen.getByText('Island Computing')).toBeInTheDocument()
    expect(screen.getByText('1,499.50')).toBeInTheDocument()
    expect(screen.getByText('Docking station issued with it.')).toBeInTheDocument()
    // The purchase date reads as the calendar day recorded, not the day before it.
    expect(screen.getByText(formatDate(new Date(2023, 8, 20)))).toBeInTheDocument()
  })

  it('renders a bare date as the day it names, not as UTC midnight', async () => {
    // `new Date('2023-09-20')` is the 19th for anybody behind Greenwich. A warranty and a
    // purchase date are calendar facts with no zone (WP-2.3).
    renderDetail()

    await screen.findByRole('heading', { name: 'Jane’s laptop' })
    expect(screen.queryByText(formatDate(new Date(2023, 8, 19)))).not.toBeInTheDocument()
  })

  it('groups the two lines one operation wrote into one timeline event', async () => {
    fetchAssetHistory.mockResolvedValue(
      historyPage([
        historyEntry({ id: 'a', kind: 'Assignment', fromValue: null, toValue: 'Jane Doe', sequence: 0 }),
        historyEntry({
          id: 'b',
          kind: 'Status',
          fromValue: 'In Stock',
          toValue: 'Deployed',
          sequence: 1,
        }),
      ]),
    )
    renderDetail()

    const timeline = await screen.findByRole('list', { name: 'Asset history' })
    // The events, not the lines inside them: each event renders its own list of the
    // dimensions that moved, so `listitem` would count both levels.
    const events = within(timeline).getAllByRole('article')

    // One event for the operation, one for the synthesised recorded line.
    expect(events).toHaveLength(2)
    expect(within(events[0] as HTMLElement).getByText('Assigned to:')).toBeInTheDocument()
    expect(within(events[0] as HTMLElement).getByText('Status:')).toBeInTheDocument()
    expect(within(events[0] as HTMLElement).getByText('Mark Reyes updated this asset')).toBeInTheDocument()
  })

  it('words an empty assignment value rather than showing a dash', async () => {
    fetchAssetHistory.mockResolvedValue(
      historyPage([historyEntry({ kind: 'Assignment', fromValue: null, toValue: 'Jane Doe' })]),
    )
    renderDetail()

    const timeline = await screen.findByRole('list', { name: 'Asset history' })
    expect(within(timeline).getByText('Nobody')).toBeInTheDocument()
  })

  it('always shows a beginning, because recording an asset writes no history entry', async () => {
    renderDetail()

    const timeline = await screen.findByRole('list', { name: 'Asset history' })
    expect(within(timeline).getByText('Recorded as')).toBeInTheDocument()
  })

  it('marks the gap when the page on screen does not reach the beginning', async () => {
    fetchAssetHistory.mockResolvedValue(historyPage([historyEntry()], 62))
    renderDetail()

    expect(
      await screen.findByText('Older history for this asset is not shown.'),
    ).toBeInTheDocument()
  })

  it('lists the tickets raised about the asset and links each to its detail', async () => {
    fetchAssetTickets.mockResolvedValue(ticketsPage([ticketSummary()]))
    renderDetail()

    const panel = await screen.findByRole('list', { name: 'Tickets about this asset' })
    expect(within(panel).getByRole('link', { name: 'TKT-0001' })).toHaveAttribute(
      'href',
      '/tickets/ticket-1',
    )
    expect(within(panel).getByText('Laptop will not connect to Wi-Fi')).toBeInTheDocument()
    expect(within(panel).getByText('New')).toBeInTheDocument()
  })

  it('says so when the asset has never been the subject of a ticket', async () => {
    renderDetail()

    expect(
      await screen.findByText('No tickets have been raised about this asset.'),
    ).toBeInTheDocument()
  })

  it('keeps reading when a panel fails, because the three reads are independent', async () => {
    // WP-2.5's shape: one round trip per panel, so the timeline failing does not take the
    // asset down with it.
    fetchAssetHistory.mockRejectedValue(new Error('network'))
    renderDetail()

    expect(await screen.findByText('The history could not be loaded.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Jane’s laptop' })).toBeInTheDocument()
    expect(screen.getByText('Island Computing')).toBeInTheDocument()
  })

  it('says an asset in a terminal status has reached the end of its life', async () => {
    fetchAsset.mockResolvedValue(
      read({
        assetStatusCode: 'retired',
        assetStatusName: 'Retired',
        assignedToUserId: null,
        assignedToUserName: null,
        allowedNextStatusCodes: [],
        canBeAssigned: false,
      }),
    )
    renderDetail()

    expect(
      await screen.findByText('This asset has reached the end of its lifecycle.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Nobody')).toBeInTheDocument()
  })

  /**
   * WP-2.6b's criterion, on the screen. The buttons come from the server's
   * `allowedNextStatusCodes` and `canBeAssigned` — the fixture is a deployed asset that
   * somebody holds — and an action the server would refuse is not rendered at all.
   */
  it('renders the lifecycle actions the server says are legal, and no others', async () => {
    renderDetail()

    await screen.findByRole('heading', { name: 'Jane’s laptop' })

    for (const legal of [/^transfer$/i, /^return$/i, /send for repair/i, /^retire$/i]) {
      expect(screen.getByRole('button', { name: legal })).toBeInTheDocument()
    }

    // Not legal from `deployed`, and so absent rather than greyed out: an asset that is
    // already deployed is not issued out of stock, and is not coming back from repair.
    expect(screen.queryByRole('button', { name: /^assign$/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /return to service/i })).not.toBeInTheDocument()
  })

  it('renders no lifecycle actions at all for an asset that has reached the end of its life', async () => {
    fetchAsset.mockResolvedValue(
      read({
        assetStatusCode: 'retired',
        assetStatusName: 'Retired',
        assignedToUserId: null,
        assignedToUserName: null,
        allowedNextStatusCodes: [],
        canBeAssigned: false,
      }),
    )
    renderDetail()

    await screen.findByRole('heading', { name: 'Jane’s laptop' })

    for (const action of [/^retire$/i, /send for repair/i, /^assign$/i, /^transfer$/i, /^return$/i]) {
      expect(screen.queryByRole('button', { name: action })).not.toBeInTheDocument()
    }

    // Correcting a mistyped serial on a retired asset is a thing somebody has to do, and
    // the server accepts it — so the edit link stays.
    expect(screen.getByRole('link', { name: /edit/i })).toBeInTheDocument()
  })

  /**
   * The precondition ARCHITECTURE.md §6 asks for. The tag the detail was read at goes back
   * with every write, so a stale copy is refused with 412 before the move is attempted
   * rather than losing a race.
   */
  it('sends the tag it read the asset at with a lifecycle write', async () => {
    renderDetail()

    await userEvent.click(await screen.findByRole('button', { name: /send for repair/i }))
    await userEvent.click(
      within(screen.getByRole('dialog')).getByRole('button', { name: /send for repair/i }),
    )

    await waitFor(() => {
      expect(moveAsset).toHaveBeenCalledWith(assetId, 'repairs', null, '"41"')
    })
  })

  it('carries the note somebody typed onto the move', async () => {
    renderDetail()

    await userEvent.click(await screen.findByRole('button', { name: /^retire$/i }))
    const dialog = screen.getByRole('dialog')
    await userEvent.type(within(dialog).getByLabelText('Note'), 'Water damage, written off')
    await userEvent.click(within(dialog).getByRole('button', { name: /^retire$/i }))

    await waitFor(() => {
      expect(moveAsset).toHaveBeenCalledWith(
        assetId,
        'retirements',
        'Water damage, written off',
        '"41"',
      )
    })
  })

  /**
   * The three assignment acts share one route and are told apart by what they send: a
   * person for an issue or a transfer, and null for a return (WP-2.2).
   */
  it('sends a return down the assignment route with nobody named', async () => {
    renderDetail()

    await userEvent.click(await screen.findByRole('button', { name: /^return$/i }))
    await userEvent.click(
      within(screen.getByRole('dialog')).getByRole('button', { name: /^return$/i }),
    )

    await waitFor(() => {
      expect(assignAsset).toHaveBeenCalledWith(assetId, null, null, '"41"')
    })
  })

  /** A transfer cannot be confirmed until somebody is named, rather than failing on submit. */
  it('will not confirm a transfer until a person is chosen', async () => {
    renderDetail()

    await userEvent.click(await screen.findByRole('button', { name: /^transfer$/i }))
    const dialog = screen.getByRole('dialog')

    expect(within(dialog).getByRole('button', { name: /^transfer$/i })).toBeDisabled()

    // The person already holding it is not offered: issuing an asset to its own holder is
    // refused with `assets.already_assigned_to_that_user`.
    await userEvent.click(within(dialog).getByLabelText(/^Transfer to/))
    expect(screen.queryByRole('option', { name: 'Jane Doe' })).not.toBeInTheDocument()
    await userEvent.click(await screen.findByRole('option', { name: 'Mark Reyes' }))

    await userEvent.click(within(dialog).getByRole('button', { name: /^transfer$/i }))

    await waitFor(() => {
      expect(assignAsset).toHaveBeenCalledWith(assetId, 'user-2', null, '"41"')
    })
  })

  /**
   * A stale write and an ordinary failure read differently to the person in front of them.
   * Both 412 and 409 mean somebody else got there first.
   */
  it('says the asset moved when a write is refused with a precondition failure', async () => {
    moveAsset.mockRejectedValue(new ApiError(412, null, 'stale'))
    renderDetail()

    await userEvent.click(await screen.findByRole('button', { name: /send for repair/i }))
    await userEvent.click(
      within(screen.getByRole('dialog')).getByRole('button', { name: /send for repair/i }),
    )

    await waitFor(() => {
      expect(toastError).toHaveBeenCalledWith(
        'This asset changed while you were reading it.',
        expect.objectContaining({
          description: 'It has been reloaded. Check what happened and try again.',
        }),
      )
    })
  })

  it('offers the edit form for the asset on screen', async () => {
    renderDetail()

    await screen.findByRole('heading', { name: 'Jane’s laptop' })

    expect(screen.getByRole('link', { name: /edit/i })).toHaveAttribute(
      'href',
      `/assets/${assetId}/edit`,
    )
  })

  it('says what the server said when the asset is not there', async () => {
    // A soft-deleted asset answers 404 like one that never existed, and the list never
    // returns deleted rows either.
    fetchAsset.mockRejectedValue(new ApiError(404, null, 'Not found'))
    renderDetail()

    expect(await screen.findByText('No such asset')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('offers a retry when the read failed for any other reason', async () => {
    fetchAsset.mockRejectedValue(new Error('network'))
    renderDetail()

    const alert = await screen.findByRole('alert')
    expect(within(alert).getByText('The asset could not be loaded.')).toBeInTheDocument()

    fetchAsset.mockResolvedValue(read())
    await userEvent.click(within(alert).getByRole('button', { name: 'Try again' }))

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'Jane’s laptop' })).toBeInTheDocument()
    })
  })
})
