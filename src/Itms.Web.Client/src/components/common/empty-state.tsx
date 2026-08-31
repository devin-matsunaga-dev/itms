import type { LucideIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'

interface EmptyStateProps {
  icon: LucideIcon
  /** A plain sentence saying what would appear here. */
  title: string
  description?: string
  action?: { label: string; onClick: () => void }
}

/**
 * DESIGN.md §4: an outlined icon, a plain sentence saying what would appear here, and
 * the primary action. No apology, no illustration.
 */
export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
}: EmptyStateProps): React.JSX.Element {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-card border border-border bg-surface px-5 py-16 text-center shadow-card">
      <span className="flex size-12 items-center justify-center rounded-tile bg-primary-soft">
        <Icon className="size-6 text-primary" strokeWidth={1.5} aria-hidden="true" />
      </span>
      <p className="text-card-title font-semibold text-heading">{title}</p>
      {description ? <p className="max-w-prose text-copy text-body">{description}</p> : null}
      {action ? (
        <Button size="lg" className="mt-2" onClick={action.onClick}>
          {action.label}
        </Button>
      ) : null}
    </div>
  )
}
