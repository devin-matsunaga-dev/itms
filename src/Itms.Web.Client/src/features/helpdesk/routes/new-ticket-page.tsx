import { useCallback } from 'react'
import { Link, useNavigate } from 'react-router'
import { Controller, useForm, type SubmitHandler } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Info, Paperclip } from 'lucide-react'
import { toast } from 'sonner'
import { PageHeader } from '@/components/layout/page-header'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { ApiError } from '@/lib/api/client'
import { hasAnyRole, Roles } from '@/lib/roles'
import type { CreateTicketRequest } from '@/lib/api/types'
import { useCurrentUser } from '@/features/auth/hooks/use-current-user'
import { DepartmentPicker } from '../components/department-picker'
import { useCreateTicket } from '../hooks/use-ticket'
import {
  useAssignableUsers,
  useDepartments,
  useTicketCategories,
  useTicketPriorities,
} from '../hooks/use-tickets'
import {
  descriptionMaxLength,
  emptyNewTicket,
  newTicketSchema,
  subjectMaxLength,
  type NewTicketForm,
} from '../lib/new-ticket-schema'

/** The form fields the server can name in a validation failure. */
const formFields = [
  'subject',
  'description',
  'categoryId',
  'priorityId',
  'requesterId',
  'departmentId',
] as const

/**
 * Raising a ticket.
 *
 * A route rather than a dialog: the address is linkable, and DESIGN.md §4 puts a long
 * form in section cards on a page. The button that reaches it lives in the Tickets page
 * header and in that screen's empty state — WP-0.8 moved ticket creation off the sidebar
 * and rewrote DESIGN.md §3 and §4 to say so.
 *
 * **The requester field is shown to everybody and editable by nobody but staff.** WP-1.10
 * hid it from an end user entirely; showing it read-only as "their name (you)" says the
 * same thing out loud, which is better than a form that quietly has different fields for
 * different people. The tooltip explains why it is fixed. None of that is the enforcement
 * — a User naming somebody else is refused with 403 (WP-1.5) and would be if this form
 * were hand-crafted.
 */
export function NewTicketPage(): React.JSX.Element {
  const navigate = useNavigate()
  const { data: currentUser } = useCurrentUser()
  const worksTheQueue = hasAnyRole(currentUser?.roles ?? [], [Roles.admin, Roles.technician])

  const categories = useTicketCategories()
  const priorities = useTicketPriorities()
  const departments = useDepartments()
  // `GET /api/v1/users` is the general Technician-guarded picker; the queue reads it for
  // its assignee filter and this form reads it for the requester. One endpoint, one
  // cache entry.
  const people = useAssignableUsers(worksTheQueue)

  const create = useCreateTicket()

  const form = useForm<NewTicketForm>({
    resolver: zodResolver(newTicketSchema),
    defaultValues: emptyNewTicket,
  })

  const onSubmit = useCallback<SubmitHandler<NewTicketForm>>(
    (values) => {
      const request: CreateTicketRequest = {
        subject: values.subject,
        description: values.description,
        categoryId: values.categoryId,
        priorityId: values.priorityId,
        requesterId: values.requesterId === '' ? null : values.requesterId,
        departmentId: values.departmentId === '' ? null : values.departmentId,
      }

      create.mutate(request, {
        onSuccess: (ticket) => {
          toast.success(`${ticket.number} raised.`)
          void navigate(`/tickets/${ticket.id}`)
        },
        onError: (error) => {
          // ProblemDetails carries per-field messages keyed by camel-cased field name
          // (WP-0.3), which is exactly what a form needs — so a retired category lands on
          // the category select rather than in a toast nobody can act on.
          if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
            for (const field of formFields) {
              const messages = error.fieldErrors[field]
              if (messages && messages.length > 0) {
                form.setError(field, { type: 'server', message: messages[0] })
              }
            }
            return
          }

          toast.error('The ticket could not be raised.', {
            description: error instanceof Error ? error.message : undefined,
          })
        },
      })
    },
    [create, form, navigate],
  )

  const errors = form.formState.errors
  // "You" while the account is still loading, rather than the "you (you)" that naming a
  // person we do not have yet would produce.
  const me =
    currentUser === null || currentUser === undefined
      ? 'You'
      : `${currentUser.displayName} (you)`

  return (
    <>
      <PageHeader
        title="New ticket"
        subtitle="Create a new service request. Provide as much detail as possible."
        back={{ to: '/tickets', label: 'Back to tickets' }}
      />

      <form
        noValidate
        className="flex flex-col gap-5"
        onSubmit={(event) => {
          void form.handleSubmit(onSubmit)(event)
        }}
      >
        <FormSection title="Request details">
          <Field label="Title" htmlFor="ticket-subject" required error={errors.subject?.message}>
            <Input
              id="ticket-subject"
              maxLength={subjectMaxLength}
              placeholder="Briefly describe the issue"
              aria-invalid={errors.subject !== undefined}
              {...form.register('subject')}
            />
          </Field>

          <Field
            label="Description"
            htmlFor="ticket-description"
            required
            error={errors.description?.message}
          >
            <Textarea
              id="ticket-description"
              rows={6}
              maxLength={descriptionMaxLength}
              placeholder="Describe what happened, what you expected, and any troubleshooting steps you already tried."
              aria-invalid={errors.description !== undefined}
              {...form.register('description')}
            />
          </Field>

          {/*
            A statement, not a control. The API attaches only to a ticket that already
            exists (WP-1.7), so attaching here would mean create-then-upload: two requests
            that can half-fail and leave a ticket whose files silently did not arrive.
            Saying where the affordance is beats a link that does nothing, which WP-0.8
            settled, and beats a half-failing one.
          */}
          <p className="flex items-start gap-2 text-copy text-muted-foreground">
            <Paperclip className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <span>
              Attachments are added on the ticket itself.
              <span className="block text-caption">
                Create the ticket first, then attach files from its page.
              </span>
            </span>
          </p>
        </FormSection>

        <FormSection title="Ticket details">
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <Field label="Category" htmlFor="ticket-category" required error={errors.categoryId?.message}>
              <Controller
                control={form.control}
                name="categoryId"
                render={({ field }) => (
                  <FormSelect
                    id="ticket-category"
                    placeholder="Choose a category"
                    value={field.value}
                    invalid={errors.categoryId !== undefined}
                    options={(categories.data ?? []).map((category) => ({
                      value: category.id,
                      label: category.name,
                    }))}
                    onValueChange={field.onChange}
                  />
                )}
              />
            </Field>

            <Field label="Priority" htmlFor="ticket-priority" required error={errors.priorityId?.message}>
              <Controller
                control={form.control}
                name="priorityId"
                render={({ field }) => (
                  <FormSelect
                    id="ticket-priority"
                    placeholder="Choose a priority"
                    value={field.value}
                    invalid={errors.priorityId !== undefined}
                    options={(priorities.data ?? []).map((priority) => ({
                      value: priority.id,
                      label: priority.name,
                    }))}
                    onValueChange={field.onChange}
                  />
                )}
              />
            </Field>

            <Field
              label="Requester"
              htmlFor="ticket-requester"
              error={errors.requesterId?.message}
              hint={
                worksTheQueue
                  ? 'Leave this as yourself, or file the ticket on somebody else’s behalf.'
                  : 'A ticket is always raised in your own name. Only a technician can file one for somebody else.'
              }
            >
              {worksTheQueue ? (
                <Controller
                  control={form.control}
                  name="requesterId"
                  render={({ field }) => (
                    <FormSelect
                      id="ticket-requester"
                      placeholder={me}
                      value={field.value}
                      invalid={errors.requesterId !== undefined}
                      options={[
                        { value: '', label: me },
                        ...(people.data ?? [])
                          .filter((person) => person.id !== currentUser?.id)
                          .map((person) => ({ value: person.id, label: person.displayName })),
                      ]}
                      onValueChange={field.onChange}
                    />
                  )}
                />
              ) : (
                // Shown rather than hidden, so the form reads the same for everybody and
                // says why the field is fixed instead of leaving a gap where it was.
                <Input id="ticket-requester" readOnly value={me} className="bg-canvas" />
              )}
            </Field>

            <Field label="Department" htmlFor="ticket-department" error={errors.departmentId?.message}>
              <Controller
                control={form.control}
                name="departmentId"
                render={({ field }) => (
                  <DepartmentPicker
                    id="ticket-department"
                    departments={departments.data ?? []}
                    value={field.value}
                    invalid={errors.departmentId !== undefined}
                    onValueChange={field.onChange}
                  />
                )}
              />
            </Field>
          </div>
        </FormSection>

        <div className="flex items-center justify-end gap-3">
          <Button variant="outline" type="button" render={<Link to="/tickets" />}>
            Cancel
          </Button>
          <Button type="submit" disabled={create.isPending}>
            {create.isPending ? 'Creating…' : 'Create ticket'}
          </Button>
        </div>
      </form>
    </>
  )
}

/**
 * One section of the form (DESIGN.md §4: long forms use section cards, not accordions).
 *
 * The heading is the card's own label — uppercase, tracked, in `primary` — which is what
 * makes two stacked cards read as two parts of one form rather than two unrelated panels.
 */
function FormSection({
  title,
  children,
}: {
  title: string
  children: React.ReactNode
}): React.JSX.Element {
  return (
    <section className="rounded-card border border-border bg-surface p-5 shadow-card">
      <h2 className="text-label font-semibold tracking-[0.06em] text-primary uppercase">
        {title}
      </h2>
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
function Field({
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
        whatever control it wraps, so nesting the button here would make "Requester" the
        accessible name of an info icon as well as of the field.
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

interface FormSelectProps {
  id: string
  placeholder: string
  value: string
  invalid: boolean
  options: readonly { value: string; label: string }[]
  onValueChange: (value: string) => void
}

function FormSelect({
  id,
  placeholder,
  value,
  invalid,
  options,
  onValueChange,
}: FormSelectProps): React.JSX.Element {
  return (
    <Select
      items={options}
      value={value === '' ? null : value}
      onValueChange={(next: string | null) => {
        onValueChange(next ?? '')
      }}
    >
      <SelectTrigger id={id} size="default" className="w-full" aria-invalid={invalid}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
