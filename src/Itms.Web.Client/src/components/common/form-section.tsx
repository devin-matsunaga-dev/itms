import { Info } from 'lucide-react'
import { Label } from '@/components/ui/label'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'

/**
 * The two pieces every long form in this product is built from (DESIGN.md §4).
 *
 * **Hoisted at WP-2.7, on the third copy.** `new-ticket-page.tsx` wrote the first at
 * WP-1.13 and `asset-form.tsx` the second at WP-2.6b, each recording that hoisting from
 * their own package would mean editing a merged package's screen and that the third copy
 * is the one that moves. WP-2.7's directory forms are the third, so all of them now read
 * from here and the two feature-local copies are gone.
 */

/**
 * One section card of a form.
 *
 * The uppercase label in `primary` is what makes two stacked cards read as two parts of
 * one form rather than as two unrelated panels — DESIGN.md §4 asks for a section label
 * rather than an accordion for exactly that reason.
 */
export function FormSection({
  title,
  children,
}: {
  title: string
  children: React.ReactNode
}): React.JSX.Element {
  return (
    <section className="rounded-card border border-border bg-surface p-5 shadow-card">
      <h2 className="text-label font-semibold tracking-[0.06em] text-primary uppercase">{title}</h2>
      <div className="mt-5 flex flex-col gap-5">{children}</div>
    </section>
  )
}

interface FieldProps {
  label: string
  htmlFor: string
  required?: boolean
  error?: string
  /** Explains a field whose behaviour is not obvious, behind an info icon. */
  hint?: string
  children: React.ReactNode
}

/** DESIGN.md §4, Forms: label above, error below, required marked in `danger`. */
export function Field({
  label,
  htmlFor,
  required,
  error,
  hint,
  children,
}: FieldProps): React.JSX.Element {
  return (
    <div className="flex flex-col gap-1.5">
      {/*
        The hint's button is a sibling of the label, never inside it: a <label> labels
        whatever control it wraps, so nesting the button here would make the field's name
        the accessible name of the icon too.
      */}
      <div className="flex items-center gap-1.5">
        <Label htmlFor={htmlFor} className="text-field-label font-medium text-heading">
          {label}
          {required === true ? (
            <span className="text-danger" aria-label="required">
              *
            </span>
          ) : null}
        </Label>
        {hint === undefined ? null : (
          <Tooltip>
            <TooltipTrigger
              render={
                <button
                  type="button"
                  aria-label={`About the ${label.toLowerCase()} field`}
                  className="flex rounded-full text-muted-foreground transition-colors hover:text-heading focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                />
              }
            >
              <Info className="size-3.5" aria-hidden="true" />
            </TooltipTrigger>
            <TooltipContent>{hint}</TooltipContent>
          </Tooltip>
        )}
      </div>
      {children}
      {error === undefined ? null : (
        <p role="alert" className="text-caption text-danger">
          {error}
        </p>
      )}
    </div>
  )
}
