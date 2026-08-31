import { cn } from '@/lib/utils'
import { priorityDot } from '../lib/ticket-display'

interface PriorityLabelProps {
  /** The priority's immutable code — `critical`, `high`, `medium`, `low` (WP-1.1). */
  code: string
  /** Its name as it reads now, which an administrator may have changed. */
  name: string
  className?: string
}

/**
 * A ticket's priority as dot + label — DESIGN.md §4's treatment for a priority column,
 * and what `reference-dashboard.png` shows in the Open Tickets table.
 *
 * Keyed on the code rather than the name, so renaming "High" to "Urgent" moves the word
 * and not the colour.
 */
export function PriorityLabel({ code, name, className }: PriorityLabelProps): React.JSX.Element {
  return (
    <span className={cn('inline-flex items-center gap-2 text-cell text-heading', className)}>
      <span className={cn('size-2 shrink-0 rounded-full', priorityDot(code))} aria-hidden="true" />
      {name}
    </span>
  )
}
