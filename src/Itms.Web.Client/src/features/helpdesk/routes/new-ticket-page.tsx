import { useCallback } from 'react'
import { Link, useNavigate } from 'react-router'
import { Controller, useForm, type SubmitHandler } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowLeft } from 'lucide-react'
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
import { ApiError } from '@/lib/api/client'
import { hasAnyRole, Roles } from '@/lib/roles'
import type { CreateTicketRequest } from '@/lib/api/types'
import { useCurrentUser } from '@/features/auth/hooks/use-current-user'
import { useCreateTicket } from '../hooks/use-ticket'
import { useAssignableUsers, useDepartments, useTicketCategories, useTicketPriorities } from '../hooks/use-tickets'
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
 * Raising a ticket (WP-1.10).
 *
 * A route rather than a dialog: the address is linkable, and DESIGN.md §4 puts a long
 * form in section cards on a page. The button that reaches it lives in the Tickets page
 * header and in that screen's empty state — WP-0.8 moved ticket creation off the sidebar
 * and rewrote DESIGN.md §3 and §4 to say so, and WP-1.10's own "New Ticket button in the
 * sidebar" is the superseded wording.
 *
 * **Two fields are offered only to somebody who works the queue.** A User files for
 * themselves: the requester defaults to the caller and the department to the requester's
 * own account, so an end user is not asked a question their record already answers. That
 * is a courtesy and not the enforcement — a User naming somebody else is refused with a
 * 403 rather than quietly coerced (WP-1.5), and would be if this form were hand-crafted.
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

  return (
    <>
      <PageHeader
        title="New ticket"
        subtitle="Describe the problem. A technician picks it up from the queue."
        actions={
          <Button variant="outline" render={<Link to="/tickets" />}>
            <ArrowLeft className="size-4" aria-hidden="true" />
            Queue
          </Button>
        }
      />

      <form
        noValidate
        className="flex max-w-3xl flex-col gap-5"
        onSubmit={(event) => {
          void form.handleSubmit(onSubmit)(event)
        }}
      >
        <section className="flex flex-col gap-5 rounded-card border border-border bg-surface p-5 shadow-card">
          <Field label="Title" htmlFor="ticket-subject" required error={errors.subject?.message}>
            <Input
              id="ticket-subject"
              maxLength={subjectMaxLength}
              placeholder="One line saying what is wrong"
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
              placeholder="What happened, what you expected, and anything already tried"
              aria-invalid={errors.description !== undefined}
              {...form.register('description')}
            />
          </Field>
        </section>

        <section className="flex flex-col gap-5 rounded-card border border-border bg-surface p-5 shadow-card">
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

            {worksTheQueue ? (
              <>
                <Field label="Requester" htmlFor="ticket-requester" error={errors.requesterId?.message}>
                  <Controller
                    control={form.control}
                    name="requesterId"
                    render={({ field }) => (
                      <FormSelect
                        id="ticket-requester"
                        placeholder="Me"
                        value={field.value}
                        invalid={errors.requesterId !== undefined}
                        options={(people.data ?? []).map((person) => ({
                          value: person.id,
                          label: person.displayName,
                        }))}
                        onValueChange={field.onChange}
                      />
                    )}
                  />
                </Field>

                <Field
                  label="Department"
                  htmlFor="ticket-department"
                  error={errors.departmentId?.message}
                >
                  <Controller
                    control={form.control}
                    name="departmentId"
                    render={({ field }) => (
                      <FormSelect
                        id="ticket-department"
                        placeholder="The requester's own"
                        value={field.value}
                        invalid={errors.departmentId !== undefined}
                        options={(departments.data ?? []).map((department) => ({
                          value: department.id,
                          label: department.name,
                        }))}
                        onValueChange={field.onChange}
                      />
                    )}
                  />
                </Field>
              </>
            ) : null}
          </div>
        </section>

        <div className="flex items-center justify-end gap-3">
          <Button variant="outline" type="button" render={<Link to="/tickets" />}>
            Cancel
          </Button>
          <Button type="submit" disabled={create.isPending}>
            {create.isPending ? 'Raising…' : 'Raise ticket'}
          </Button>
        </div>
      </form>
    </>
  )
}

interface FieldProps {
  label: string
  htmlFor: string
  required?: boolean
  error?: string
  children: React.ReactNode
}

/** DESIGN.md §4, Forms: label above, error below, required marked in `danger`. */
function Field({ label, htmlFor, required, error, children }: FieldProps): React.JSX.Element {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={htmlFor} className="text-field-label font-medium text-heading">
        {label}
        {required === true ? (
          <span className="text-danger" aria-hidden="true">
            *
          </span>
        ) : null}
      </Label>
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
