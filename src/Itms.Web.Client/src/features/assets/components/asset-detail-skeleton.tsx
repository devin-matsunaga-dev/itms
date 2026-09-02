import { Skeleton } from '@/components/ui/skeleton'

/**
 * The asset detail while it loads.
 *
 * DESIGN.md §4: the shimmer sits inside the shape the content will take — two columns,
 * the same cards — never a centred spinner in a card.
 */
export function AssetDetailSkeleton(): React.JSX.Element {
  return (
    <div className="grid grid-cols-1 gap-5 lg:grid-cols-12" aria-busy="true" aria-live="polite">
      <span className="sr-only">Loading the asset…</span>

      <div className="flex flex-col gap-5 lg:col-span-8">
        <div className="rounded-card border border-border bg-surface p-5 shadow-card">
          <div className="flex gap-5">
            <Skeleton className="h-5 w-24" />
            <Skeleton className="h-5 w-20" />
          </div>
          <div className="mt-5 grid grid-cols-3 gap-5 border-t border-border pt-5">
            {[0, 1, 2].map((field) => (
              <div key={field} className="space-y-1.5">
                <Skeleton className="h-3 w-20" />
                <Skeleton className="h-4 w-32" />
              </div>
            ))}
          </div>
        </div>

        <div className="rounded-card border border-border bg-surface p-5 shadow-card">
          <Skeleton className="h-5 w-32" />
          {[0, 1, 2].map((row) => (
            <div key={row} className="mt-4 flex items-start gap-3">
              <Skeleton className="size-9 shrink-0 rounded-full" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-4 w-40" />
                <Skeleton className="h-4 w-3/4" />
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="flex flex-col gap-5 lg:col-span-4">
        <div className="rounded-card border border-border bg-surface p-5 shadow-card">
          <Skeleton className="h-5 w-24" />
          {[0, 1, 2, 3, 4, 5].map((row) => (
            <div key={row} className="mt-4 space-y-1.5">
              <Skeleton className="h-3 w-20" />
              <Skeleton className="h-4 w-36" />
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
