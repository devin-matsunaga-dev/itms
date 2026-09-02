import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { AssetDetailPage } from '@/features/assets/routes/asset-detail-page'
import { ApiError } from '@/lib/api/client'
import { formatDate } from '@/lib/datetime'
import type { Asset, PagedAssetHistory, PagedTicketSummaries } from '@/lib/api/types'
import {
  asset,
  assetId,
  historyEntry,
  ticketSummary,
} from '@/features/assets/test/asset-fixtures'
import { renderWithProviders } from '@/test/render'

const fetchAsset = vi.fn<() => Promise<Asset>>()
const fetchAssetHistory = vi.fn<() => Promise<PagedAssetHistory>>()
const fetchAssetTickets = vi.fn<() => Promise<PagedTicketSummaries>>()

vi.mock('@/features/assets/api/assets-api', () => ({
  fetchAsset: () => fetchAsset(),
  fetchAssetHistory: () => fetchAssetHistory(),
  fetchAssetTickets: () => fetchAssetTickets(),
  fetchAssets: vi.fn(),
  fetchAssetTypes: vi.fn(),
  fetchAssetStatuses: vi.fn(),
  fetchAssetHolders: vi.fn(),
}))

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

  fetchAsset.mockResolvedValue(asset())
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
      asset({ assetStatusCode: 'retired', assetStatusName: 'Retired', assignedToUserName: null }),
    )
    renderDetail()

    expect(
      await screen.findByText('This asset has reached the end of its lifecycle.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Nobody')).toBeInTheDocument()
  })

  it('renders no lifecycle actions, because the server does not yet say which are legal', async () => {
    // `WP-2.6b` adds the legal-destination list to `AssetResponse` and the buttons that
    // read it. Until then there is nothing here that could render them honestly, and
    // WP-2.6's own criterion is that an illegal action is absent rather than disabled.
    renderDetail()

    await screen.findByRole('heading', { name: 'Jane’s laptop' })

    for (const action of [/retire/i, /send for repair/i, /assign/i, /transfer/i, /edit/i]) {
      expect(screen.queryByRole('button', { name: action })).not.toBeInTheDocument()
    }
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

    fetchAsset.mockResolvedValue(asset())
    await userEvent.click(within(alert).getByRole('button', { name: 'Try again' }))

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'Jane’s laptop' })).toBeInTheDocument()
    })
  })
})
