import { Skeleton } from '@/components/ui/skeleton'

interface FullPageLoadingProps {
  /** Announced to assistive technology while the wait lasts. */
  label: string
}

/**
 * The shell's own loading state, used while the session is being established and there
 * is not yet a page shape to put skeletons into. DESIGN.md bans a centred spinner
 * inside a card; this is the frame itself, so it draws the frame.
 */
export function FullPageLoading({ label }: FullPageLoadingProps): React.JSX.Element {
  return (
    <div className="flex min-h-screen bg-canvas" role="status" aria-live="polite" aria-busy="true">
      <span className="sr-only">{label}</span>
      <div className="w-sidebar shrink-0 bg-gradient-to-b from-sidebar to-sidebar-deep" />
      <div className="flex min-w-0 flex-1 flex-col">
        <div className="h-topbar shrink-0 border-b border-border bg-surface" />
        <div className="flex flex-col gap-5 p-8">
          <Skeleton className="h-9 w-72" />
          <Skeleton className="h-4 w-96" />
          <div className="grid grid-cols-4 gap-5">
            <Skeleton className="h-28 rounded-card" />
            <Skeleton className="h-28 rounded-card" />
            <Skeleton className="h-28 rounded-card" />
            <Skeleton className="h-28 rounded-card" />
          </div>
        </div>
      </div>
    </div>
  )
}
