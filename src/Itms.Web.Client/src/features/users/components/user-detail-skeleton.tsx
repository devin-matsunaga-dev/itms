import { Skeleton } from '@/components/ui/skeleton'

/**
 * The user page while its first read is in flight.
 *
 * DESIGN.md §4: shimmer inside each card's own shape, never a centred spinner. The shape is
 * the two-column layout the loaded screen uses, so nothing jumps when the panels arrive.
 */
export function UserDetailSkeleton(): React.JSX.Element {
  return (
    <div className="grid grid-cols-1 gap-5 lg:grid-cols-12" aria-busy="true" aria-label="Loading">
      <div className="flex flex-col gap-5 lg:col-span-4">
        <Card>
          <div className="flex items-center gap-3">
            <Skeleton className="size-10 rounded-full" />
            <div className="flex flex-col gap-1.5">
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-3 w-52" />
            </div>
          </div>
          <div className="mt-5 flex flex-col gap-4">
            {[0, 1, 2, 3].map((row) => (
              <div key={row} className="flex flex-col gap-1.5">
                <Skeleton className="h-2.5 w-20" />
                <Skeleton className="h-3.5 w-44" />
              </div>
            ))}
          </div>
        </Card>

        <Card>
          <Skeleton className="h-4 w-32" />
          <div className="mt-4 flex flex-col gap-4">
            {[0, 1].map((row) => (
              <div key={row} className="flex flex-col gap-1.5">
                <Skeleton className="h-3 w-24" />
                <Skeleton className="h-3.5 w-full" />
              </div>
            ))}
          </div>
        </Card>
      </div>

      <div className="flex flex-col gap-5 lg:col-span-8">
        {[0, 1].map((card) => (
          <Card key={card}>
            <Skeleton className="h-4 w-36" />
            <div className="mt-4 flex flex-col gap-4">
              {[0, 1, 2].map((row) => (
                <div key={row} className="flex flex-col gap-1.5">
                  <Skeleton className="h-3 w-20" />
                  <Skeleton className="h-3.5 w-full" />
                </div>
              ))}
            </div>
          </Card>
        ))}
      </div>
    </div>
  )
}

function Card({ children }: { children: React.ReactNode }): React.JSX.Element {
  return (
    <div className="rounded-card border border-border bg-surface p-5 shadow-card">{children}</div>
  )
}
