import { useEffect, useState } from 'react'
import { Controller, useForm, type SubmitHandler } from 'react-hook-form'
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api/client'
import type { Location } from '@/lib/api/types'
import { useLocationSubtree, useLocationUsage } from '../hooks/use-directory'
import {
  useCreateLocation,
  useDeleteLocation,
  useMoveLocation,
  useUpdateLocation,
} from '../hooks/use-directory-write'
import {
  descriptionMaxLength,
  locationFormSchema,
  locationKinds,
  nameMaxLength,
  orNull,
  type LocationFormValues,
} from '../lib/directory-schemas'
import { LocationPicker } from './location-picker'
import { UsageBreakdown } from './usage-breakdown'

/**
 * The four things an administrator does to the location tree (WP-0.6, WP-2.4, WP-2.7).
 *
 * ## The hierarchy rule is the server's, and stays there
 *
 * Whether a Room may sit under a Floor is `LocationHierarchy`'s, resolved server-side so no
 * client holds a second copy (WP-2.4). Two consequences show up here, and both are
 * deliberate:
 *
 * - **The create dialog offers all six kinds** and lets the server refuse an illegal one
 *   with `directory.illegal_placement`, whose message names the whole hierarchy in a
 *   sentence — "A Building cannot sit under a Room. The hierarchy runs Organization, Site,
 *   Building, Floor or Area, Room." Narrowing the list here would mean ranking the kinds in
 *   TypeScript, which is the copy WP-2.4 spent an interface to avoid. It is the one place
 *   in this package where an illegal choice is offered rather than absent, and the cure is
 *   a server field naming the legal child kinds — the shape `allowedNextStatusCodes` has on
 *   an asset (WP-2.6b). That is recorded in STATUS.md rather than built here, because it is
 *   a server change this package was not granted.
 * - **The move dialog is server-driven and offers nothing illegal**, because the read for
 *   it already exists: `?adoptableFor=<kind>` returns exactly the nodes that could legally
 *   adopt this one, and the node's own subtree is excluded on top of that — a node cannot
 *   move beneath itself.
 */

interface LocationFormDialogProps {
  /** The node being renamed, or null when creating. */
  location: Location | null
  /** The parent a new node is created under, or null for a new root. */
  parent: Location | null
  open: boolean
  onOpenChange: (open: boolean) => void
}

/** Creating a location under a parent, and renaming one. */
export function LocationFormDialog({
  location,
  parent,
  open,
  onOpenChange,
}: LocationFormDialogProps): React.JSX.Element {
  const create = useCreateLocation()
  const update = useUpdateLocation()
  const editing = location !== null

  const form = useForm<LocationFormValues>({
    resolver: zodResolver(locationFormSchema),
    defaultValues: { name: '', kind: 'Room', description: '' },
  })

  // The dialog outlives the node it was opened on, so the fields are refilled whenever it
  // reopens on a different one.
  useEffect(() => {
    if (open) {
      form.reset({
        name: location?.name ?? '',
        // A sensible starting point rather than a rule: a new node under a building is
        // usually a room, and a new top-level node can only be an organisation. The server
        // decides whether the choice is legal.
        kind: location?.kind ?? (parent === null ? 'Organization' : 'Room'),
        description: location?.description ?? '',
      })
    }
  }, [form, location, open, parent])

  const errors = form.formState.errors
  const busy = create.isPending || update.isPending

  const submit: SubmitHandler<LocationFormValues> = (values) => {
    const done = {
      onSuccess: (saved: Location) => {
        toast.success(editing ? `${saved.name} updated.` : `${saved.name} created.`)
        onOpenChange(false)
      },
      onError: (error: unknown) => {
        if (error instanceof ApiError && error.status === 409) {
          // A duplicate sibling name belongs on the name field; an illegal placement or a
          // tree that is already five deep belongs on the kind, which is what decides it.
          const field = error.code === 'directory.duplicate_location_name' ? 'name' : 'kind'
          form.setError(field, { message: error.message })
          return
        }

        toast.error(editing ? 'The location could not be saved.' : 'The location could not be created.', {
          description: error instanceof Error ? error.message : undefined,
        })
      },
    }

    if (editing) {
      update.mutate(
        {
          id: location.id,
          request: { name: values.name.trim(), description: orNull(values.description) },
        },
        done,
      )
      return
    }

    create.mutate(
      {
        name: values.name.trim(),
        kind: values.kind,
        parentId: parent?.id ?? null,
        description: orNull(values.description),
      },
      done,
    )
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {editing
              ? `Rename ${location.name}`
              : parent === null
                ? 'New organisation'
                : `New location in ${parent.name}`}
          </DialogTitle>
          <DialogDescription>
            {editing
              ? 'Renaming a node rewrites the path of everything beneath it, in one step.'
              : 'The hierarchy runs Organization, Site, Building, Floor or Area, Room. A level may be skipped, but never inverted.'}
          </DialogDescription>
        </DialogHeader>

        <form
          id="location-form"
          className="flex flex-col gap-5"
          onSubmit={(event) => {
            void form.handleSubmit(submit)(event)
          }}
        >
          <Field label="Name" htmlFor="location-name" required error={errors.name?.message}>
            <Input
              id="location-name"
              maxLength={nameMaxLength}
              placeholder="Server Room"
              aria-invalid={errors.name !== undefined}
              {...form.register('name')}
            />
          </Field>

          {editing ? (
            // The kind is fixed once a node exists: `UpdateLocationRequest` carries only
            // the name and the description, so a Room cannot quietly become a Building
            // under equipment that is already in it. Shown read-only with the reason
            // rather than hidden (DESIGN.md §4).
            <Field
              label="Level"
              htmlFor="location-kind-fixed"
              hint="A location's level is fixed once it exists. Create the node you want at the right level and move what is under it."
            >
              <Input id="location-kind-fixed" readOnly value={location.kind} className="bg-canvas" />
            </Field>
          ) : (
            <Field label="Level" htmlFor="location-kind" required error={errors.kind?.message}>
              <Controller
                control={form.control}
                name="kind"
                render={({ field }) => (
                  <Select
                    items={locationKinds.map((kind) => ({ value: kind, label: kind }))}
                    value={field.value}
                    onValueChange={(value: string | null) => {
                      if (value !== null) {
                        field.onChange(value)
                      }
                    }}
                  >
                    <SelectTrigger
                      id="location-kind"
                      size="default"
                      className="w-full"
                      aria-invalid={errors.kind !== undefined}
                    >
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {locationKinds.map((kind) => (
                        <SelectItem key={kind} value={kind}>
                          {kind}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </Field>
          )}

          <Field
            label="Description"
            htmlFor="location-description"
            error={errors.description?.message}
          >
            <Textarea
              id="location-description"
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
          <Button type="submit" form="location-form" disabled={busy}>
            {editing ? 'Save changes' : 'Create location'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

interface LocationMoveDialogProps {
  /** The node being moved, or null when the dialog is closed. */
  location: Location | null
  onOpenChange: (open: boolean) => void
}

/**
 * Moving a subtree.
 *
 * One call moves the node and rewrites every descendant's path in the same transaction —
 * WP-2.4's own done-criterion — so this dialog says so rather than implying that the
 * children have to follow by hand.
 */
export function LocationMoveDialog({
  location,
  onOpenChange,
}: LocationMoveDialogProps): React.JSX.Element {
  const move = useMoveLocation()

  // A node may not move beneath itself or its own descendants, and the server refuses one
  // that tries with `directory.location_cycle`. Offering them would be offering a button
  // that always fails, which is what the transfer picker's exclusion avoids at WP-2.6b.
  const subtree = useLocationSubtree(location?.id ?? null)
  const excluded = (subtree.data ?? []).map((node) => node.id)

  // The chosen parent starts where the node already sits, and is reset whenever the dialog
  // reopens on a different node. Adjusted during render rather than in an effect — the
  // shape the search boxes and the picker use — so there is no frame in which the dialog
  // shows the previous node's parent.
  const [chosen, setChosen] = useState<{ nodeId: string | null; parentId: string | null }>({
    nodeId: location?.id ?? null,
    parentId: location?.parentId ?? null,
  })

  if (chosen.nodeId !== (location?.id ?? null)) {
    setChosen({ nodeId: location?.id ?? null, parentId: location?.parentId ?? null })
  }

  const parentId = chosen.parentId
  const setParentId = (next: string | null): void => {
    setChosen((current) => ({ ...current, parentId: next }))
  }

  return (
    <Dialog
      open={location !== null}
      onOpenChange={(open) => {
        onOpenChange(open)
      }}
    >
      <DialogContent>
        {location === null ? null : (
          <>
            <DialogHeader>
              <DialogTitle>{`Move ${location.name}`}</DialogTitle>
              <DialogDescription>
                Everything beneath it moves with it, and every path is rewritten in one step.
              </DialogDescription>
            </DialogHeader>

            <Field
              label="New parent"
              htmlFor="location-move-parent"
              hint="Only the levels that could legally hold this one are offered. Clearing the field moves it to the top level, which only an organisation may be."
            >
              {subtree.isPending ? (
                <Skeleton className="h-10 w-full" aria-label="Loading the subtree" />
              ) : (
                <LocationPicker
                  id="location-move-parent"
                  value={parentId}
                  placeholder="Top level (no parent)"
                  adoptableFor={location.kind}
                  excludedIds={excluded}
                  onValueChange={setParentId}
                />
              )}
            </Field>

            <DialogFooter>
              <Button
                variant="outline"
                disabled={move.isPending}
                onClick={() => {
                  onOpenChange(false)
                }}
              >
                Cancel
              </Button>
              <Button
                disabled={move.isPending || parentId === location.parentId}
                onClick={() => {
                  move.mutate(
                    { id: location.id, request: { parentId } },
                    {
                      onSuccess: (moved) => {
                        toast.success(`${moved.name} moved.`)
                        onOpenChange(false)
                      },
                      onError: (error: unknown) => {
                        toast.error('The location could not be moved.', {
                          description: error instanceof Error ? error.message : undefined,
                        })
                      },
                    },
                  )
                }}
              >
                Move
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}

interface LocationDeleteDialogProps {
  /** The node being deleted, or null when the dialog is closed. */
  location: Location | null
  onOpenChange: (open: boolean) => void
}

/**
 * Deleting a location.
 *
 * Unlike a department, a location **can** be deleted — and the server refuses two
 * different ways, with two different codes, because they send an administrator to
 * different places: `directory.location_has_children` means empty the subtree, and
 * `directory.location_in_use` means move the equipment and the people. The usage read shows
 * both ahead of the click.
 *
 * `canDelete` is advisory and the delete is still allowed to fail: the server re-checks
 * inside its own transaction, because either count can change between the two calls
 * (WP-2.4). So the button is disabled when the answer is known to be no, and the refusal is
 * still handled when it is not.
 */
export function LocationDeleteDialog({
  location,
  onOpenChange,
}: LocationDeleteDialogProps): React.JSX.Element {
  const usage = useLocationUsage(location?.id ?? null)
  const remove = useDeleteLocation()

  return (
    <Dialog open={location !== null} onOpenChange={onOpenChange}>
      <DialogContent>
        {location === null ? null : (
          <>
            <DialogHeader>
              <DialogTitle>{`Delete ${location.name}?`}</DialogTitle>
              <DialogDescription>
                A location is deleted outright rather than retired. Nothing that referenced it
                keeps a link to it.
              </DialogDescription>
            </DialogHeader>

            <div className="flex flex-col gap-2">
              <p className="text-label font-semibold tracking-[0.06em] text-primary uppercase">
                What it still holds
              </p>

              {usage.isPending ? (
                <Skeleton className="h-16 w-full" aria-label="Loading the usage" />
              ) : usage.isError ? (
                <p role="alert" className="text-copy text-body">
                  What this location holds could not be read. The delete will still be checked
                  by the server.
                </p>
              ) : (
                <>
                  {usage.data.childCount > 0 ? (
                    <p className="text-copy text-body">
                      It contains {usage.data.childCount}{' '}
                      {usage.data.childCount === 1 ? 'location' : 'locations'}. Delete or move
                      them first.
                    </p>
                  ) : null}
                  <UsageBreakdown
                    references={usage.data.references}
                    emptyMessage="Nothing references this location."
                  />
                </>
              )}
            </div>

            <DialogFooter>
              <Button
                variant="outline"
                disabled={remove.isPending}
                onClick={() => {
                  onOpenChange(false)
                }}
              >
                Cancel
              </Button>
              <Button
                variant="destructive"
                disabled={remove.isPending || usage.data?.canDelete === false}
                onClick={() => {
                  remove.mutate(location.id, {
                    onSuccess: () => {
                      toast.success(`${location.name} deleted.`)
                      onOpenChange(false)
                    },
                    onError: (error: unknown) => {
                      toast.error('The location could not be deleted.', {
                        description: error instanceof Error ? error.message : undefined,
                      })
                    },
                  })
                }}
              >
                Delete
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}
