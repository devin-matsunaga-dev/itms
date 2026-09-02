import { Skeleton } from '@/components/ui/skeleton'

/**
 * The directory while it loads.
 *
 * DESIGN.md §4: shimmer inside the card's own shape, never a centred spinner. The shape is
 * the round initials tile beside a two-line name-and-address cell, so the screen does not
 * jump when the rows arrive.
 */
export function UserTableSkeleton({ rows = 8 }: { rows?: number }): React.JSX.Element {
  return (
    <div
      className="overflow-hidden rounded-card border border-border bg-surface shadow-card"
      aria-busy="true"
      aria-label="Loading people"
    >
      <div className="h-10 border-b border-border" />
      {Array.from({ length: rows }, (_, index) => (
        <div
          key={index}
          className="flex items-center gap-4 border-b border-border px-4 py-3 last:border-b-0"
        >
          <Skeleton className="size-8 rounded-full" />
          <div className="flex w-[22rem] flex-col gap-1.5">
            <Skeleton className="h-3 w-40" />
            <Skeleton className="h-2.5 w-52" />
          </div>
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-3 w-32" />
          <Skeleton className="h-3 w-40" />
          <Skeleton className="h-5 w-20 rounded-md" />
        </div>
      ))}
    </div>
  )
}
