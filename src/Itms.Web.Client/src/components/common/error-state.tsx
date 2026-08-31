import { TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'

interface ErrorStateProps {
  /** What failed, stated plainly. */
  title: string
  description?: string
  onRetry?: () => void
}

/**
 * DESIGN.md §4: errors state what failed and offer a retry. They do not apologize.
 */
export function ErrorState({ title, description, onRetry }: ErrorStateProps): React.JSX.Element {
  return (
    <div
      role="alert"
      className="flex flex-col items-center justify-center gap-3 rounded-card border border-border bg-surface px-5 py-16 text-center shadow-card"
    >
      <span className="flex size-12 items-center justify-center rounded-tile bg-danger/12">
        <TriangleAlert className="size-6 text-danger" strokeWidth={1.5} aria-hidden="true" />
      </span>
      <p className="text-card-title font-semibold text-heading">{title}</p>
      {description ? <p className="max-w-prose text-copy text-body">{description}</p> : null}
      {onRetry ? (
        <Button variant="outline" size="lg" className="mt-2" onClick={onRetry}>
          Try again
        </Button>
      ) : null}
    </div>
  )
}
