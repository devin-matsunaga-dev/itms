import { useEffect } from 'react'
import { useForm, type SubmitHandler } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Field } from '@/components/common/form-section'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api/client'
import type { Department } from '@/lib/api/types'
import { useCreateDepartment, useUpdateDepartment } from '../hooks/use-directory-write'
import {
  codeMaxLength,
  departmentFormSchema,
  descriptionMaxLength,
  nameMaxLength,
  orNull,
  type DepartmentFormValues,
} from '../lib/directory-schemas'

interface DepartmentDialogProps {
  /** The department being edited, or null to create one. Closed when `open` is false. */
  department: Department | null
  open: boolean
  onOpenChange: (open: boolean) => void
}

/**
 * The department form, for creating one and for correcting one (WP-2.7).
 *
 * One dialog and one schema for both, because they ask for exactly the same three facts —
 * the call `asset-form.tsx` made at WP-2.6b. A department has no immutable field, so unlike
 * the asset form there is nothing to render read-only.
 *
 * **A duplicate name or code comes back from the server as a 409 and is mapped onto the
 * field that caused it.** Whether a name is taken is a question only the database can
 * answer, and a client that checked first would still lose the race to the row it read.
 */
export function DepartmentDialog({
  department,
  open,
  onOpenChange,
}: DepartmentDialogProps): React.JSX.Element {
  const create = useCreateDepartment()
  const update = useUpdateDepartment()
  const editing = department !== null

  const form = useForm<DepartmentFormValues>({
    resolver: zodResolver(departmentFormSchema),
    defaultValues: { name: '', code: '', description: '' },
  })

  // The dialog outlives the row it was opened on, so the fields are refilled whenever it
  // reopens on a different department. An effect because the trigger is the prop moving,
  // not anything the form did.
  useEffect(() => {
    if (open) {
      form.reset({
        name: department?.name ?? '',
        code: department?.code ?? '',
        description: department?.description ?? '',
      })
    }
  }, [department, form, open])

  const errors = form.formState.errors
  const busy = create.isPending || update.isPending

  const submit: SubmitHandler<DepartmentFormValues> = (values) => {
    const request = {
      name: values.name.trim(),
      code: orNull(values.code),
      description: orNull(values.description),
    }

    const done = {
      onSuccess: (saved: Department) => {
        toast.success(editing ? `${saved.name} updated.` : `${saved.name} created.`)
        onOpenChange(false)
      },
      onError: (error: unknown) => {
        if (error instanceof ApiError && error.status === 409) {
          // The two collisions the server can report, each onto the field that caused it.
          const field = error.code === 'directory.duplicate_department_code' ? 'code' : 'name'
          form.setError(field, { message: error.message })
          return
        }

        toast.error(editing ? 'The department could not be saved.' : 'The department could not be created.', {
          description: error instanceof Error ? error.message : undefined,
        })
      },
    }

    if (editing) {
      update.mutate({ id: department.id, request }, done)
      return
    }

    create.mutate(request, done)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{editing ? `Edit ${department.name}` : 'New department'}</DialogTitle>
          <DialogDescription>
            Departments are named by tickets, assets, and people, and are retired rather than
            deleted.
          </DialogDescription>
        </DialogHeader>

        <form
          id="department-form"
          className="flex flex-col gap-5"
          onSubmit={(event) => {
            void form.handleSubmit(submit)(event)
          }}
        >
          <Field label="Name" htmlFor="department-name" required error={errors.name?.message}>
            <Input
              id="department-name"
              maxLength={nameMaxLength}
              placeholder="Information Technology"
              aria-invalid={errors.name !== undefined}
              {...form.register('name')}
            />
          </Field>

          <Field
            label="Code"
            htmlFor="department-code"
            error={errors.code?.message}
            hint="A short key such as IT or FIN. Optional, and unique across the organisation when it is set."
          >
            <Input
              id="department-code"
              maxLength={codeMaxLength}
              placeholder="IT"
              aria-invalid={errors.code !== undefined}
              {...form.register('code')}
            />
          </Field>

          <Field
            label="Description"
            htmlFor="department-description"
            error={errors.description?.message}
          >
            <Textarea
              id="department-description"
              rows={3}
              maxLength={descriptionMaxLength}
              aria-invalid={errors.description !== undefined}
              {...form.register('description')}
            />
          </Field>
        </form>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            disabled={busy}
            onClick={() => {
              onOpenChange(false)
            }}
          >
            Cancel
          </Button>
          <Button type="submit" form="department-form" disabled={busy}>
            {editing ? 'Save changes' : 'Create department'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
