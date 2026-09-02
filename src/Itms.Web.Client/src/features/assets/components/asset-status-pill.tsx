import { cn } from '@/lib/utils'
import { statusTone } from '../lib/asset-display'

interface AssetStatusPillProps {
  /** The status's immutable code — what the colour is keyed on (WP-2.1). */
  code: string
  /** The status's current name — what the reader sees. An administrator may rename it. */
  name: string
  className?: string
}

/**
 * An asset's lifecycle status as a pill (DESIGN.md §4).
 *
 * Two fields rather than one, and that is the whole point: the hue comes from `code`,
 * which never changes, and the word comes from `name`, which an administrator is free to
 * edit. Renaming "In Stock" to "Warehouse" moves the label and leaves the blue alone.
 *
 * The hue is carried by the fill and the dot rather than by the label, which sets in
 * `heading` so it clears WCAG AA in both colour schemes — `asset-display.ts` sets out why
 * at length, and it is the same treatment the ticket queue's pill uses so one status is
 * not two treatments on two screens.
 */
export function AssetStatusPill({
  code,
  name,
  className,
}: AssetStatusPillProps): React.JSX.Element {
  const tone = statusTone(code)

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-md px-2 py-0.5 text-label font-semibold text-heading',
        tone.fill,
        className,
      )}
    >
      <span className={cn('size-1.5 shrink-0 rounded-full', tone.dot)} aria-hidden="true" />
      {name}
    </span>
  )
}
