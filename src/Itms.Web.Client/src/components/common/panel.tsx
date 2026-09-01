import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

interface PanelProps {
  /** The 18px header icon, rendered in `primary`. */
  icon?: LucideIcon
  title: string
  /** The right-aligned control: a "View All" link, a select, or a kebab menu. */
  action?: React.ReactNode
  children: React.ReactNode
  className?: string
  /** Padding is dropped when the body is a full-bleed list with its own row rules. */
  flush?: boolean
}

/**
 * DESIGN.md §4's panel card: an icon and a title, an optional control on the right, and a
 * body with no internal header rule — separation comes from spacing.
 *
 * Built from the tokens rather than from shadcn's `Card`, which carries its own radius,
 * ring, and spacing scale. The queue's filter bar (WP-1.9) already writes this shape
 * inline; this is the same shape given a name, because the detail screen needs it five
 * times over.
 */
export function Panel({
  icon: Icon,
  title,
  action,
  children,
  className,
  flush = false,
}: PanelProps): React.JSX.Element {
  return (
    <section className={cn('rounded-card border border-border bg-surface shadow-card', className)}>
      <div className="flex items-center justify-between gap-4 px-5 pt-5 pb-4">
        <h2 className="flex items-center gap-2 text-card-title font-semibold text-heading">
          {Icon ? <Icon className="size-[18px] text-primary" aria-hidden="true" /> : null}
          {title}
        </h2>
        {action ? <div className="shrink-0">{action}</div> : null}
      </div>

      <div className={cn(flush ? 'pb-0' : 'px-5 pb-5')}>{children}</div>
    </section>
  )
}
