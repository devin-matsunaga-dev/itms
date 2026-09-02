import { RefreshCw } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { SortDirection, UserSort } from '@/lib/api/types'

/** The orderings the toolbar offers, worded as a person would ask for them. */
const sortOptions: readonly {
  value: string
  label: string
  sort: UserSort
  direction: SortDirection
}[] = [
  { value: 'DisplayName:Ascending', label: 'Name A–Z', sort: 'DisplayName', direction: 'Ascending' },
  {
    value: 'DisplayName:Descending',
    label: 'Name Z–A',
    sort: 'DisplayName',
    direction: 'Descending',
  },
  { value: 'Email:Ascending', label: 'Email address', sort: 'Email', direction: 'Ascending' },
  {
    value: 'CreatedAt:Descending',
    label: 'Recently added',
    sort: 'CreatedAt',
    direction: 'Descending',
  },
]

interface UserToolbarProps {
  /** How many people the current query matches, server-side. */
  total: number
  sort: UserSort
  direction: SortDirection
  /** True while the directory is refetching, so the refresh control can say so. */
  refreshing: boolean
  onSortChange: (sort: UserSort, direction: SortDirection) => void
  onRefresh: () => void
}

/**
 * The strip between the filters and the table: how many people are being shown, and in
 * what order.
 *
 * The sort select and the sortable column header are two doors to one thing — the `sort`
 * and `direction` in the URL. Neither holds state of its own, so they cannot disagree; the
 * select exists because "recently added" is how somebody asks for it, and finding that
 * behind a column header would mean knowing which column carries the creation date. There
 * is no column or density control, for the reason `user-table.tsx` gives.
 */
export function UserToolbar({
  total,
  sort,
  direction,
  refreshing,
  onSortChange,
  onRefresh,
}: UserToolbarProps): React.JSX.Element {
  const current = `${sort}:${direction}`

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-4 py-2.5">
      <p className="text-copy text-body tabular" aria-live="polite">
        {total === 1 ? '1 person' : `${String(total)} people`}
      </p>

      <div className="flex flex-wrap items-center gap-2">
        <div className="flex items-center gap-2">
          <Label htmlFor="user-sort" className="text-caption text-muted-foreground">
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
            <SelectTrigger id="user-sort" size="sm" className="w-40">
              {/* The column header can produce an ordering the select does not name — the
                  address descending, say. The placeholder says so rather than the trigger
                  naming an ordering that is not in force. */}
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
          aria-label="Refresh the directory"
          disabled={refreshing}
          onClick={onRefresh}
        >
          <RefreshCw
            className={cn('size-4', refreshing && 'motion-safe:animate-spin')}
            aria-hidden="true"
          />
        </Button>
      </div>
    </div>
  )
}
