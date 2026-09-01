import { ArrowLeft } from 'lucide-react'
import { Link } from 'react-router'

interface PageHeaderProps {
  title: string
  /** The one-line description under the title. */
  subtitle: string
  /** The screen's primary action, rendered on the right. */
  actions?: React.ReactNode
  /**
   * Where this screen came from, rendered as a link *above* the title.
   *
   * A screen reached from a list — a ticket, an asset, a create form — owes the reader a
   * way back that is not the browser's own button. It sits above the title rather than
   * among the actions on the right because it is not one: the actions are things done to
   * the thing on screen, and leaving is not one of them.
   */
  back?: { to: string; label: string }
}

/**
 * The page header (DESIGN.md §3): an optional back link, then the title and subtitle on
 * the left with the screen's actions on the right. The date and time are not here — they
 * are stated once, in the topbar.
 */
export function PageHeader({
  title,
  subtitle,
  actions,
  back,
}: PageHeaderProps): React.JSX.Element {
  return (
    <div className="mb-5">
      {back ? (
        <Link
          to={back.to}
          className="mb-2 inline-flex items-center gap-1.5 rounded-sm text-copy font-medium text-primary transition-colors hover:text-primary-hover focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
        >
          <ArrowLeft className="size-4" aria-hidden="true" />
          {back.label}
        </Link>
      ) : null}

      <div className="flex items-start justify-between gap-5">
        <div className="min-w-0">
          <h1 className="text-page-title font-semibold text-heading">{title}</h1>
          <p className="mt-1 text-copy text-body">{subtitle}</p>
        </div>

        {actions ? <div className="flex shrink-0 items-center gap-4">{actions}</div> : null}
      </div>
    </div>
  )
}
