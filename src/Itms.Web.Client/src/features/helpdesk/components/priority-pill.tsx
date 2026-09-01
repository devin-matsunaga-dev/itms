import { ArrowDown, ArrowUp, ChevronsUp, Minus, type LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { priorityTone, type PriorityArrow } from '../lib/ticket-display'

const arrows: Record<PriorityArrow, LucideIcon> = {
  'up-double': ChevronsUp,
  up: ArrowUp,
  flat: Minus,
  down: ArrowDown,
}

interface PriorityLabelProps {
  /** The priority's immutable code — `critical`, `high`, `medium`, `low` (WP-1.1). */
  code: string
  /** Its name as it reads now, which an administrator may have changed. */
  name: string
  className?: string
}

/**
 * A ticket's priority as an arrow + label in a soft pill (DESIGN.md §4).
 *
 * Three encodings of one fact, deliberately: the fill, the arrow's hue, and the arrow's
 * direction. §6 forbids relying on colour alone, and the direction is what survives a
 * greyscale print and a red-green deficiency both.
 *
 * The label sets in `heading` rather than the semantic hue, for the reason
 * `ticket-display.ts` sets out at length and the status pill already follows: `warning`
 * over a 12% wash of itself reaches about 1.8:1, and §6 calls AA non-negotiable in both
 * colour schemes. The hue is carried by the fill and the glyph, which is a 14px shape
 * rather than letterforms.
 */
export function PriorityLabel({ code, name, className }: PriorityLabelProps): React.JSX.Element {
  const tone = priorityTone(code)
  const Arrow = arrows[tone.arrow]

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-md px-2 py-0.5 text-label font-semibold text-heading',
        tone.fill,
        className,
      )}
    >
      <Arrow className={cn('size-3.5 shrink-0', tone.icon)} aria-hidden="true" />
      {name}
    </span>
  )
}
