import { ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { AssetListItem, AssetSort } from '@/lib/api/types'
import { formatDateTime, formatRelative } from '@/lib/datetime'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { PersonCell } from '@/features/helpdesk/components/person-cell'
import { AssetStatusPill } from './asset-status-pill'
import { WarrantyCell } from './warranty-cell'
import { assetTitle } from '../lib/asset-display'
import { isVisible, type AssetColumnId, type AssetTablePreferences } from '../lib/asset-columns'
import type { AssetQuery } from '../lib/asset-query'

interface AssetTableProps {
  assets: readonly AssetListItem[]
  query: AssetQuery
  /** The instant every age and countdown in the table is measured against. */
  now: Date
  preferences: AssetTablePreferences
  onSort: (column: AssetSort) => void
  onOpen: (asset: AssetListItem) => void
  /** The toolbar, rendered as this card's header — it describes the list it sits on. */
  toolbar?: React.ReactNode
}

/**
 * The asset register (DESIGN.md §4, *Data table*).
 *
 * The identifying column is two lines, exactly as the ticket queue's is: the **asset tag**
 * as a `primary` link over what the machine is called, with a caption saying how long ago
 * it was recorded. The tag leads rather than the name because it is what is printed on the
 * label somebody is holding while they look at this screen — and because it is the one
 * field that is unique, immutable, and never null (invariant 4).
 *
 * Which of the remaining columns are drawn, and how tall a row is, come from the reader's
 * own stored preferences rather than from the URL — `asset-columns.ts` says why.
 * Everything that decides *which rows* is still in the address.
 *
 * The table scrolls horizontally inside its own card rather than reflowing (§6), which is
 * what the primitive's container already does.
 */
export function AssetTable({
  assets,
  query,
  now,
  preferences,
  onSort,
  onOpen,
  toolbar,
}: AssetTableProps): React.JSX.Element {
  const shows = (id: AssetColumnId): boolean => isVisible(preferences, id)
  const compact = preferences.density === 'compact'
  const cell = compact ? 'px-4 py-1.5' : 'px-4 py-2.5'

  return (
    <div className="overflow-hidden rounded-card border border-border bg-surface shadow-card">
      {toolbar}
      <Table className="text-cell">
        <TableHeader>
          <TableRow className="border-border hover:bg-transparent">
            <SortableHead column="AssetTag" query={query} onSort={onSort}>
              Asset
            </SortableHead>
            {shows('status') ? (
              <SortableHead column="Status" query={query} onSort={onSort}>
                Status
              </SortableHead>
            ) : null}
            {shows('type') ? <PlainHead>Type</PlainHead> : null}
            {shows('serial') ? <PlainHead>Serial number</PlainHead> : null}
            {shows('holder') ? <PlainHead>Assigned to</PlainHead> : null}
            {shows('department') ? <PlainHead>Department</PlainHead> : null}
            {shows('location') ? <PlainHead>Location</PlainHead> : null}
            {shows('warranty') ? (
              <SortableHead column="WarrantyExpiresAt" query={query} onSort={onSort}>
                Warranty
              </SortableHead>
            ) : null}
            {shows('updated') ? (
              <SortableHead column="UpdatedAt" query={query} onSort={onSort} align="end">
                Updated
              </SortableHead>
            ) : null}
          </TableRow>
        </TableHeader>

        <TableBody>
          {assets.map((asset) => (
            <TableRow
              key={asset.id}
              // DESIGN.md §4: a row opens the detail page. The keyboard path to the same
              // action is the asset tag, which is a real control.
              onClick={() => {
                onOpen(asset)
              }}
              className="cursor-pointer border-border hover:bg-canvas"
            >
              <TableCell className={cn(cell, 'max-w-[26rem]')}>
                <button
                  type="button"
                  onClick={(event) => {
                    event.stopPropagation()
                    onOpen(asset)
                  }}
                  className="rounded-sm font-semibold text-primary transition-colors hover:text-primary-hover focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
                >
                  {asset.assetTag}
                </button>
                <p className="truncate text-cell text-heading" title={assetTitle(asset)}>
                  {assetTitle(asset)}
                </p>
                {compact ? null : (
                  <p className="text-caption text-muted-foreground">
                    Recorded {formatRelative(asset.createdAt, now)}
                  </p>
                )}
              </TableCell>

              {shows('status') ? (
                <TableCell className={cell}>
                  <AssetStatusPill code={asset.assetStatusCode} name={asset.assetStatusName} />
                </TableCell>
              ) : null}

              {shows('type') ? (
                <TableCell className={cn(cell, 'text-body')}>{asset.assetTypeName}</TableCell>
              ) : null}

              {shows('serial') ? (
                <TableCell className={cn(cell, 'text-body tabular')}>
                  {asset.serialNumber ?? '—'}
                </TableCell>
              ) : null}

              {shows('holder') ? (
                <TableCell className={cell}>
                  {/* "Unassigned" rather than a dash: nobody holding a machine is a fact
                      about the estate, and it is the thing the holder filter asks for. */}
                  <PersonCell name={asset.assignedToUserName} absent="Unassigned" />
                </TableCell>
              ) : null}

              {shows('department') ? (
                <TableCell className={cn(cell, 'text-body')}>
                  {asset.departmentName ?? '—'}
                </TableCell>
              ) : null}

              {shows('location') ? (
                <TableCell className={cn(cell, 'text-body')}>
                  {/* The cached path, which reads "Site → Building → Floor → Room" and is
                      as fresh as the last time the asset was written. STATUS.md carries
                      the gap: nothing refreshes it when a room is renamed or moved. */}
                  {asset.locationPath ?? '—'}
                </TableCell>
              ) : null}

              {shows('warranty') ? (
                <TableCell className={cell}>
                  <WarrantyCell expiresAt={asset.warrantyExpiresAt} now={now} />
                </TableCell>
              ) : null}

              {shows('updated') ? (
                <TableCell
                  className={cn(cell, 'text-right text-muted-foreground tabular')}
                  title={formatDateTime(asset.updatedAt)}
                >
                  {formatRelative(asset.updatedAt, now)}
                </TableCell>
              ) : null}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

const headClass =
  'h-10 bg-surface px-4 text-label font-semibold tracking-[0.06em] text-muted-foreground uppercase'

function PlainHead({ children }: { children: React.ReactNode }): React.JSX.Element {
  return <TableHead className={headClass}>{children}</TableHead>
}

interface SortableHeadProps {
  column: AssetSort
  query: AssetQuery
  onSort: (column: AssetSort) => void
  align?: 'start' | 'end'
  children: React.ReactNode
}

/**
 * A column header that orders the register.
 *
 * The sort is server-side and lives in the URL, so the header reports what the address
 * says rather than any state of its own — and `aria-sort` says the same thing to a screen
 * reader that the arrow says to everyone else. The toolbar's sort select is the other door
 * to the same two parameters.
 */
function SortableHead({
  column,
  query,
  onSort,
  align = 'start',
  children,
}: SortableHeadProps): React.JSX.Element {
  const active = query.sort === column
  const ascending = query.direction === 'Ascending'
  const Icon = active ? (ascending ? ArrowUp : ArrowDown) : ChevronsUpDown

  return (
    <TableHead
      className={cn(headClass, align === 'end' && 'text-right')}
      aria-sort={active ? (ascending ? 'ascending' : 'descending') : 'none'}
    >
      <button
        type="button"
        onClick={() => {
          onSort(column)
        }}
        className={cn(
          'inline-flex items-center gap-1.5 rounded-sm tracking-[0.06em] uppercase transition-colors hover:text-heading focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none',
          align === 'end' && 'flex-row-reverse',
          active && 'text-heading',
        )}
      >
        {children}
        <Icon className={cn('size-3', !active && 'opacity-50')} aria-hidden="true" />
      </button>
    </TableHead>
  )
}
