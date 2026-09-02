import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

interface PaginationProps {
  page: number
  pageSize: number
  total: number
  /** The page sizes this list offers. The API clamps at 200 regardless. */
  pageSizeOptions: readonly number[]
  /**
   * What the rows are, plural and lower case — "tickets", "assets", "people". It is read
   * out loud by the live region, so it is the noun a person would use rather than the
   * entity a developer would.
   */
  noun: string
  /** Distinguishes this footer's page-size control from any other on the page. */
  idPrefix: string
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}

/**
 * The paging footer every list screen ends with.
 *
 * **Hoisted at WP-2.7, on the third copy.** The ticket queue wrote the first at WP-1.9 and
 * the asset register the second at WP-2.6a, which recorded that the third — the user
 * directory — is where it moves into `components/common`. The three differed in exactly two
 * things, the noun and the page sizes on offer, and both are now props.
 *
 * Both the page and the page size are in the URL, so a link to page three of fifty rows
 * lands on page three of fifty rows. The range is stated in words as well as in numbers,
 * because "1–25 of 512" answers the question the two arrows only imply.
 */
export function Pagination({
  page,
  pageSize,
  total,
  pageSizeOptions,
  noun,
  idPrefix,
  onPageChange,
  onPageSizeChange,
}: PaginationProps): React.JSX.Element {
  const lastPage = Math.max(1, Math.ceil(total / pageSize))
  const first = total === 0 ? 0 : (page - 1) * pageSize + 1
  const last = Math.min(page * pageSize, total)
  const pageSizeId = `${idPrefix}-page-size`

  return (
    <div className="flex flex-wrap items-center justify-between gap-4">
      <p className="text-caption text-muted-foreground tabular" aria-live="polite">
        {total === 0
          ? `No ${noun}`
          : `${String(first)}–${String(last)} of ${String(total)} ${noun}`}
      </p>

      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2">
          <label htmlFor={pageSizeId} className="text-caption text-muted-foreground">
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
            <SelectTrigger id={pageSizeId} size="sm" className="w-20">
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
