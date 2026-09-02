import { Skeleton } from '@/components/ui/skeleton'

/**
 * The register while it loads.
 *
 * DESIGN.md §4: shimmer inside the card's own shape, never a centred spinner. The shape
 * is the two-line identifying column and the row height it produces, so the screen does
 * not jump when the rows arrive.
 */
export function AssetTableSkeleton({ rows = 8 }: { rows?: number }): React.JSX.Element {
  return (
    <div
      className="overflow-hidden rounded-card border border-border bg-surface shadow-card"
      aria-busy="true"
      aria-label="Loading assets"
    >
      <div className="h-10 border-b border-border" />
      {Array.from({ length: rows }, (_, index) => (
        <div
          key={index}
          className="flex items-center gap-4 border-b border-border px-4 py-3 last:border-b-0"
        >
          <div className="flex w-[26rem] flex-col gap-1.5">
            <Skeleton className="h-3 w-24" />
            <Skeleton className="h-3 w-56" />
            <Skeleton className="h-2.5 w-32" />
          </div>
          <Skeleton className="h-5 w-24 rounded-md" />
          <Skeleton className="h-3 w-20" />
          <Skeleton className="h-6 w-32 rounded-full" />
          <Skeleton className="h-3 w-40" />
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-3 w-16" />
        </div>
      ))}
    </div>
  )
}
