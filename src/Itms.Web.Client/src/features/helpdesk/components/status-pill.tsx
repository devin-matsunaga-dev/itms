import { cn } from '@/lib/utils'
import type { TicketStatus } from '@/lib/api/types'
import { statusLabels, statusTones } from '../lib/ticket-display'

interface StatusPillProps {
  status: TicketStatus
  className?: string
}

/**
 * A ticket's status as a pill (DESIGN.md §4) — the treatment for a dense list cell.
 *
 * The hue is carried by the fill and the dot rather than by the label, which reads in
 * `heading` so it clears WCAG AA in both colour schemes. `ticket-display.ts` explains
 * why at length; the short version is that §6 calls AA on status pills non-negotiable
 * and several of the semantic hues cannot carry 11px text.
 */
export function StatusPill({ status, className }: StatusPillProps): React.JSX.Element {
  const tone = statusTones[status]

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-md px-2 py-0.5 text-label font-semibold text-heading',
        tone.fill,
        className,
      )}
    >
      <span className={cn('size-1.5 shrink-0 rounded-full', tone.dot)} aria-hidden="true" />
      {statusLabels[status]}
    </span>
  )
}
