import { useCallback, useMemo } from 'react'
import { Navigate, useNavigate, useSearchParams } from 'react-router'
import { HardDrive } from 'lucide-react'
import { PageHeader } from '@/components/layout/page-header'
import { EmptyState } from '@/components/common/empty-state'
import { ErrorState } from '@/components/common/error-state'
import { useNow } from '@/lib/use-now'
import type { AssetListItem, AssetSort, SortDirection } from '@/lib/api/types'
import { useDepartments, useLocations } from '@/features/directory/hooks/use-directory'
import { AssetFilters } from '../components/asset-filters'
import { AssetPagination } from '../components/asset-pagination'
import { AssetSearch } from '../components/asset-search'
import { AssetTable } from '../components/asset-table'
import { AssetTableSkeleton } from '../components/asset-table-skeleton'
import { AssetToolbar } from '../components/asset-toolbar'
import { useAssetTablePreferences } from '../hooks/use-asset-table-preferences'
import {
  useAssetHolders,
  useAssetStatuses,
  useAssetTypes,
  useAssets,
} from '../hooks/use-assets'
import {
  clearedFilters,
  defaultAssetQuery,
  parseAssetQuery,
  sameAssetQuery,
  serializeAssetQuery,
  withFilters,
  type AssetQuery,
} from '../lib/asset-query'

/**
 * The asset register (WP-2.6a).
 *
 * The URL is the state. Every filter, the ordering, the page, and the page size are read
 * out of the address and written back into it, so a register survives a reload, works in a
 * new tab, and can be sent to somebody else — which is what CONVENTIONS.md and DESIGN.md
 * §6 both ask of a list screen.
 *
 * ## What is not here yet
 *
 * **No KPI row.** DESIGN.md §4's dense variant is written for a queue with counters behind
 * it, and there is no `/assets/counters` endpoint — WP-1.11 settled that a control which
 * silently does nothing is worse than one that is absent, and WP-1.12 is the precedent for
 * building the endpoint first and the tiles after.
 *
 * **No "New asset" action in the header.** DESIGN.md §4 puts a screen's primary action
 * there and this screen owes one; `WP-2.6b` writes the create form it would open, and a
 * button that navigates to a route resolving to the in-shell 404 is the thing WP-1.9
 * declined to ship. The empty state says the same and offers nothing for the same reason.
 *
 * Every route behind this screen is Technician-or-Admin (SPEC.md §14), which the nav and
 * the router already enforce through one rule in `navigation.ts`. Nothing here is the
 * enforcement; the server's policy is.
 */
export function AssetsPage(): React.JSX.Element {
  const [searchParams, setSearchParams] = useSearchParams()
  const routerNavigate = useNavigate()
  const query = useMemo(() => parseAssetQuery(searchParams), [searchParams])

  const now = useNow()

  const assets = useAssets(query)
  const { preferences, toggle, setDensity } = useAssetTablePreferences()
  const types = useAssetTypes()
  const statuses = useAssetStatuses()
  const departments = useDepartments()
  const locations = useLocations()
  const holders = useAssetHolders()

  const navigate = useCallback(
    (next: AssetQuery) => {
      setSearchParams(serializeAssetQuery(next))
    },
    [setSearchParams],
  )

  const onFilterChange = useCallback(
    (changes: Partial<AssetQuery>) => {
      navigate(withFilters(query, changes))
    },
    [navigate, query],
  )

  const onSort = useCallback(
    (column: AssetSort) => {
      // Clicking the column already sorted on reverses it; a new column starts in the
      // direction that column reads best in — a register runs forwards by tag and by
      // lifecycle position, warranties run soonest-first, and "when did this change"
      // means most recent.
      const direction: SortDirection =
        query.sort === column
          ? query.direction === 'Ascending'
            ? 'Descending'
            : 'Ascending'
          : column === 'AssetTag' || column === 'WarrantyExpiresAt' || column === 'Status'
            ? 'Ascending'
            : 'Descending'

      navigate({ ...query, sort: column, direction, page: 1 })
    },
    [navigate, query],
  )

  const onSortChange = useCallback(
    (sort: AssetSort, direction: SortDirection) => {
      navigate({ ...query, sort, direction, page: 1 })
    },
    [navigate, query],
  )

  const openAsset = useCallback(
    (asset: AssetListItem) => {
      void routerNavigate(`/assets/${asset.id}`)
    },
    [routerNavigate],
  )

  // The ordering is written into the address rather than left implicit, so what somebody
  // is looking at is what they can send on. A bare /assets is normalised once, on arrival,
  // and replaces rather than stacks history.
  const canonical = useMemo(() => serializeAssetQuery(query).toString(), [query])
  if (canonical !== searchParams.toString()) {
    return <Navigate to={{ search: `?${canonical}` }} replace />
  }

  const unfiltered = sameAssetQuery(query, defaultAssetQuery)

  return (
    <>
      <PageHeader
        title="Assets"
        subtitle="Every piece of equipment on the books, and who holds it."
      />

      <div className="flex flex-col gap-5">
        <AssetFilters
          query={query}
          types={types.data ?? []}
          statuses={statuses.data ?? []}
          departments={departments.data ?? []}
          locations={locations.data ?? []}
          holders={holders.data ?? []}
          onChange={onFilterChange}
          onClear={() => {
            navigate(clearedFilters(query))
          }}
          search={
            <AssetSearch
              value={query.search}
              onChange={(search) => {
                onFilterChange({ search })
              }}
            />
          }
        />

        {assets.isPending ? (
          <AssetTableSkeleton />
        ) : assets.isError ? (
          <ErrorState
            title="The asset register could not be loaded."
            description="The server did not answer. Nothing has been changed."
            onRetry={() => {
              void assets.refetch()
            }}
          />
        ) : assets.data.items.length === 0 ? (
          <EmptyState
            icon={HardDrive}
            title={unfiltered ? 'No assets yet' : 'No assets match these filters'}
            description={
              unfiltered
                ? 'Equipment recorded against the organisation will appear here.'
                : 'Widen or clear the filters to see more of the register.'
            }
            action={
              unfiltered
                ? undefined
                : {
                    label: 'Clear all',
                    onClick: () => {
                      navigate(clearedFilters(query))
                    },
                  }
            }
          />
        ) : (
          <>
            <AssetTable
              assets={assets.data.items}
              query={query}
              now={now}
              preferences={preferences}
              onSort={onSort}
              onOpen={openAsset}
              toolbar={
                <AssetToolbar
                  total={assets.data.total}
                  sort={query.sort}
                  direction={query.direction}
                  refreshing={assets.isFetching}
                  preferences={preferences}
                  onSortChange={onSortChange}
                  onRefresh={() => {
                    void assets.refetch()
                  }}
                  onToggleColumn={toggle}
                  onDensityChange={setDensity}
                />
              }
            />
            <AssetPagination
              page={query.page}
              pageSize={query.pageSize}
              total={assets.data.total}
              onPageChange={(page) => {
                navigate({ ...query, page })
              }}
              onPageSizeChange={(pageSize) => {
                navigate({ ...query, pageSize, page: 1 })
              }}
            />
          </>
        )}
      </div>
    </>
  )
}
