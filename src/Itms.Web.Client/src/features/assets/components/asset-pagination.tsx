import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { pageSizeOptions } from '../lib/asset-query'

interface AssetPaginationProps {
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}

/**
 * The register's paging footer.
 *
 * Both the page and the page size are in the URL, so a link to page three of fifty rows
 * lands on page three of fifty rows. The range is stated in words as well as in numbers,
 * because "1–25 of 512" answers the question the two arrows only imply.
 *
 * This is the **second** copy of this footer — the ticket queue has the first, differing
 * only in the noun. Two is where a shape gets left alone; a third is where it gets hoisted
 * into `components/common`, which is the trigger recorded in STATUS.md.
 */
export function AssetPagination({
  page,
  pageSize,
  total,
  onPageChange,
  onPageSizeChange,
}: AssetPaginationProps): React.JSX.Element {
  const lastPage = Math.max(1, Math.ceil(total / pageSize))
  const first = total === 0 ? 0 : (page - 1) * pageSize + 1
  const last = Math.min(page * pageSize, total)

  return (
    <div className="flex flex-wrap items-center justify-between gap-4">
      <p className="text-caption text-muted-foreground tabular" aria-live="polite">
        {total === 0 ? 'No assets' : `${String(first)}–${String(last)} of ${String(total)} assets`}
      </p>

      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2">
          <label htmlFor="asset-page-size" className="text-caption text-muted-foreground">
            Rows
          </label>
          <Select
            items={pageSizeOptions.map((size) => ({ label: String(size), value: String(size) }))}
            value={String(pageSize)}
            onValueChange={(value: string | null) => {
              if (value !== null) {
                onPageSizeChange(Number(value))
              }
            }}
          >
            <SelectTrigger id="asset-page-size" size="sm" className="w-20">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {pageSizeOptions.map((size) => (
                <SelectItem key={size} value={String(size)}>
                  {size}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            aria-label="Previous page"
            onClick={() => {
              onPageChange(page - 1)
            }}
          >
            <ChevronLeft className="size-4" aria-hidden="true" />
          </Button>

          <span className="text-caption text-body tabular">
            Page {page} of {lastPage}
          </span>

          <Button
            variant="outline"
            size="sm"
            disabled={page >= lastPage}
            aria-label="Next page"
            onClick={() => {
              onPageChange(page + 1)
            }}
          >
            <ChevronRight className="size-4" aria-hidden="true" />
          </Button>
        </div>
      </div>
    </div>
  )
}
