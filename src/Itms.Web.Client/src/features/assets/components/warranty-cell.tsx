import { cn } from '@/lib/utils'
import { formatDate } from '@/lib/datetime'
import { readWarranty, warrantyLabel, warrantyTone } from '../lib/warranty'

interface WarrantyCellProps {
  /** The `DateOnly` the API sent, or null when none was recorded. */
  expiresAt: string | null | undefined
  /** The instant to measure against, threaded from the screen. */
  now: Date
  /** True on the detail screen, where the absolute date sits under the countdown. */
  showDate?: boolean
  className?: string
}

/**
 * How long a warranty has left (DESIGN.md §4, *expiration list row*).
 *
 * The countdown carries the hue — `warning` under 30 days, `danger` under 7 and once it
 * has lapsed — with the absolute date under it in `muted`. Unlike a status pill, the
 * colour here is on **14px text at `warning` or `danger` against the card**, not against
 * a wash of itself, which clears AA in both schemes; the words say the same thing anyway,
 * so DESIGN.md §6's rule against colour as the only encoding holds either way.
 *
 * The absolute date is always available: on the detail screen it is rendered under the
 * countdown, and in a table cell it is the `title`, which is what §6 asks for wherever a
 * relative value is shown.
 */
export function WarrantyCell({
  expiresAt,
  now,
  showDate = false,
  className,
}: WarrantyCellProps): React.JSX.Element {
  const warranty = readWarranty(expiresAt, now)

  if (warranty.expiresAt === null) {
    return <span className={cn('text-muted-foreground', className)}>—</span>
  }

  const absolute = formatDate(warranty.expiresAt)

  return (
    <span className={cn('flex flex-col', className)} title={absolute}>
      <span className={cn('tabular', warrantyTone(warranty.state))}>
        {warrantyLabel(warranty)}
      </span>
      {showDate ? (
        <span className="text-caption text-muted-foreground tabular">{absolute}</span>
      ) : null}
    </span>
  )
}
