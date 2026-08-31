import { Skeleton } from '@/components/ui/skeleton'

/**
 * The queue while it loads.
 *
 * DESIGN.md §4: shimmer inside the card's own shape, never a centred spinner. The header
 * rule and the 44px rows are the shape, so the screen does not jump when the rows arrive.
 */
export function TicketTableSkeleton({ rows = 8 }: { rows?: number }): React.JSX.Element {
  return (
    <div
      className="overflow-hidden rounded-card border border-border bg-surface shadow-card"
      aria-busy="true"
      aria-label="Loading tickets"
    >
      <div className="h-10 border-b border-border" />
      {Array.from({ length: rows }, (_, index) => (
        <div
          key={index}
          className="flex h-11 items-center gap-4 border-b border-border px-4 last:border-b-0"
        >
          <Skeleton className="h-3 w-16" />
          <Skeleton className="h-3 flex-1" />
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-3 w-20" />
          <Skeleton className="h-4 w-20 rounded-md" />
          <Skeleton className="h-3 w-10" />
        </div>
      ))}
    </div>
  )
}
