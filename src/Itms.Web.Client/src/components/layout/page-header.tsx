interface PageHeaderProps {
  title: string
  /** The one-line description under the title. */
  subtitle: string
  /** The screen's primary action, rendered on the right. */
  actions?: React.ReactNode
}

/**
 * The page header (DESIGN.md §3): title and subtitle on the left, the screen's actions
 * on the right. The date and time are not here — they are stated once, in the topbar.
 */
export function PageHeader({ title, subtitle, actions }: PageHeaderProps): React.JSX.Element {
  return (
    <div className="mb-5 flex items-start justify-between gap-5">
      <div className="min-w-0">
        <h1 className="text-page-title font-semibold text-heading">{title}</h1>
        <p className="mt-1 text-copy text-body">{subtitle}</p>
      </div>

      {actions ? <div className="flex shrink-0 items-center gap-4">{actions}</div> : null}
    </div>
  )
}
