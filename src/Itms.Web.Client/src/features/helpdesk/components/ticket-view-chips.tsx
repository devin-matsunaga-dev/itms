import { cn } from '@/lib/utils'
import { ticketViews, type TicketViewId } from '../lib/ticket-views'

interface TicketViewChipsProps {
  /** Which of the three views the current URL already satisfies. */
  activeViews: readonly TicketViewId[]
  /**
   * How many tickets each view holds, scope-wide, or null while they load. A chip with no
   * count reads as a chip; a chip showing zero says the view is genuinely empty, which is
   * a different and useful thing to know before clicking it.
   */
  counts: Partial<Record<TicketViewId, number>> | null
  onSelect: (view: TicketViewId) => void
}

/**
 * The three built-in queue views (WP-1.9): My tickets, Unassigned, Overdue.
 *
 * They are pills that write filter parameters, so picking one produces an ordinary
 * linkable URL and the filter bar below stays the truth about what is being shown. More
 * than one can read as active — "my overdue tickets" is two of them at once — which is
 * why they are toggles rather than a tab strip.
 */
export function TicketViewChips({
  activeViews,
  counts,
  onSelect,
}: TicketViewChipsProps): React.JSX.Element {
  return (
    <div className="flex flex-wrap items-center gap-2" role="group" aria-label="Saved views">
      {ticketViews.map((view) => {
        const active = activeViews.includes(view.id)
        const count = counts?.[view.id]

        return (
          <button
            key={view.id}
            type="button"
            aria-pressed={active}
            title={view.description}
            onClick={() => {
              onSelect(view.id)
            }}
            className={cn(
              'rounded-full border px-3 py-1.5 text-caption font-semibold transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none',
              active
                ? 'border-primary bg-primary text-white'
                : 'border-border bg-surface text-body hover:bg-canvas',
            )}
          >
            {view.label}
            {count === undefined ? null : (
              <span
                className={cn(
                  'ml-2 inline-flex min-w-5 justify-center rounded-full px-1.5 tabular',
                  active ? 'bg-white/20 text-white' : 'bg-canvas text-muted-foreground',
                )}
              >
                {count}
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}
