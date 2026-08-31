import { Toaster as Sonner, type ToasterProps } from 'sonner'
import { CircleCheck, Info, Loader2, OctagonX, TriangleAlert } from 'lucide-react'

/**
 * The toast surface (DESIGN.md §1). Themed to the ITMS tokens rather than sonner's own
 * palette, and pinned to light: V1 ships light mode, so there is no theme to read.
 *
 * `aria-live` politeness comes from sonner itself, which is why toasts are the one
 * announcement channel in this application (CONVENTIONS.md, Accessibility).
 */
function Toaster({ ...props }: ToasterProps): React.JSX.Element {
  return (
    <Sonner
      theme="light"
      className="toaster group"
      icons={{
        success: <CircleCheck className="size-4 text-success" />,
        info: <Info className="size-4 text-info" />,
        warning: <TriangleAlert className="size-4 text-warning" />,
        error: <OctagonX className="size-4 text-danger" />,
        loading: <Loader2 className="size-4 animate-spin text-muted-foreground" />,
      }}
      style={
        {
          '--normal-bg': 'var(--color-surface)',
          '--normal-text': 'var(--color-heading)',
          '--normal-border': 'var(--color-border)',
          '--border-radius': 'var(--radius-tile)',
        } as React.CSSProperties
      }
      {...props}
    />
  )
}

export { Toaster }
