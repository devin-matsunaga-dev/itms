import { Columns3, RefreshCw, Rows3 } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { Popover, PopoverContent, PopoverTitle, PopoverTrigger } from '@/components/ui/popover'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { AssetSort, SortDirection } from '@/lib/api/types'
import {
  assetColumns,
  isVisible,
  type AssetColumnId,
  type AssetTablePreferences,
} from '../lib/asset-columns'

/** The orderings the toolbar offers, worded as a person would ask for them. */
const sortOptions: readonly {
  value: string
  label: string
  sort: AssetSort
  direction: SortDirection
}[] = [
  { value: 'AssetTag:Ascending', label: 'Asset tag', sort: 'AssetTag', direction: 'Ascending' },
  { value: 'Status:Ascending', label: 'Lifecycle status', sort: 'Status', direction: 'Ascending' },
  {
    value: 'WarrantyExpiresAt:Ascending',
    label: 'Warranty soonest',
    sort: 'WarrantyExpiresAt',
    direction: 'Ascending',
  },
  { value: 'CreatedAt:Descending', label: 'Recently added', sort: 'CreatedAt', direction: 'Descending' },
  {
    value: 'UpdatedAt:Descending',
    label: 'Recently changed',
    sort: 'UpdatedAt',
    direction: 'Descending',
  },
]

interface AssetToolbarProps {
  /** How many assets the current query matches, server-side. */
  total: number
  sort: AssetSort
  direction: SortDirection
  /** True while the register is refetching, so the refresh control can say so. */
  refreshing: boolean
  preferences: AssetTablePreferences
  onSortChange: (sort: AssetSort, direction: SortDirection) => void
  onRefresh: () => void
  onToggleColumn: (id: AssetColumnId) => void
  onDensityChange: (density: AssetTablePreferences['density']) => void
}

/**
 * The strip between the filters and the table: what is being shown, how it is ordered,
 * and how the reader wants it laid out.
 *
 * The sort select and the sortable column headers are two doors to one thing — the `sort`
 * and `direction` in the URL. Neither holds state of its own, so they cannot disagree; the
 * select exists because "warranty soonest" is how somebody asks for it, and finding that
 * behind a column header means knowing which column carries the expiry.
 *
 * Columns and density are per-browser preferences rather than URL state, for the reason
 * `asset-columns.ts` sets out: they describe how one person reads, not which rows are
 * shown, and a link should carry the second and not the first.
 */
export function AssetToolbar({
  total,
  sort,
  direction,
  refreshing,
  preferences,
  onSortChange,
  onRefresh,
  onToggleColumn,
  onDensityChange,
}: AssetToolbarProps): React.JSX.Element {
  const current = `${sort}:${direction}`
  const compact = preferences.density === 'compact'

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-4 py-2.5">
      <p className="text-copy text-body tabular" aria-live="polite">
        {total === 1 ? '1 asset' : `${String(total)} assets`}
      </p>

      <div className="flex flex-wrap items-center gap-2">
        <div className="flex items-center gap-2">
          <Label htmlFor="asset-sort" className="text-caption text-muted-foreground">
            Sort
          </Label>
          <Select
            items={sortOptions.map((option) => ({ label: option.label, value: option.value }))}
            value={sortOptions.some((option) => option.value === current) ? current : null}
            onValueChange={(value: string | null) => {
              const chosen = sortOptions.find((option) => option.value === value)
              if (chosen) {
                onSortChange(chosen.sort, chosen.direction)
              }
            }}
          >
            <SelectTrigger id="asset-sort" size="sm" className="w-44">
              {/* A column header can produce an ordering the select does not name — say,
                  the tag descending. The placeholder is what says so rather than the
                  trigger showing an ordering that is not in force. */}
              <SelectValue placeholder="Custom" />
            </SelectTrigger>
            <SelectContent>
              {sortOptions.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <Button
          variant="outline"
          size="sm"
          aria-label="Refresh the register"
          disabled={refreshing}
          onClick={onRefresh}
        >
          <RefreshCw
            className={cn('size-4', refreshing && 'motion-safe:animate-spin')}
            aria-hidden="true"
          />
        </Button>

        <Popover>
          <PopoverTrigger render={<Button variant="outline" size="sm" />}>
            <Columns3 className="size-4" aria-hidden="true" />
            Columns
          </PopoverTrigger>
          <PopoverContent align="end" className="w-56">
            <PopoverTitle>Columns</PopoverTitle>
            <div className="flex flex-col gap-3">
              {assetColumns.map((column) => (
                <div key={column.id} className="flex items-center gap-2">
                  <Checkbox
                    id={`asset-column-${column.id}`}
                    checked={isVisible(preferences, column.id)}
                    onCheckedChange={() => {
                      onToggleColumn(column.id)
                    }}
                  />
                  <Label
                    htmlFor={`asset-column-${column.id}`}
                    className="text-copy font-normal text-body"
                  >
                    {column.label}
                  </Label>
                </div>
              ))}
            </div>
          </PopoverContent>
        </Popover>

        <Button
          variant="outline"
          size="sm"
          role="switch"
          aria-checked={compact}
          aria-label="Compact rows"
          onClick={() => {
            onDensityChange(compact ? 'comfortable' : 'compact')
          }}
        >
          <Rows3 className={cn('size-4', compact && 'text-primary')} aria-hidden="true" />
        </Button>
      </div>
    </div>
  )
}
