import { useState } from 'react'
import { Link } from 'react-router'
import { Pencil } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import type { Asset, UserSummary } from '@/lib/api/types'
import { assetActions, noteMaxLength, type AssetAction } from '../lib/asset-lifecycle'

interface AssetLifecycleActionsProps {
  asset: Asset
  /** People equipment can be issued to. Everybody, not only the queue. */
  holders: readonly UserSummary[]
  /** True while a write is in flight, so an action cannot be pressed twice. */
  busy: boolean
  onAct: (action: AssetAction, holderId: string | null, note: string | null) => void
}

/**
 * The lifecycle actions for an asset, in the detail screen's page header.
 *
 * **Every button here came from the server's `allowedNextStatusCodes` and `canBeAssigned`.**
 * WP-2.6b's criterion is that an illegal action is *not rendered*, and the only way that
 * stays true as `AssetLifecycle`'s table changes is for the buttons to be told rather than
 * to decide — which is what `AssetLifecycle.DestinationsFrom`'s own doc comment asks for.
 * `asset-lifecycle.ts` holds that derivation and explains the two fields.
 *
 * **Every action stops for a dialog**, unlike a ticket's transitions, where only two do.
 * Equipment moves are consequential and unwitnessed — nobody else is watching an asset the
 * way a requester watches a ticket — and each one can carry a note that travels with the
 * history entry forever. The note is optional on all six, deliberately: a technician
 * booking a box of laptops back in from repair should not have to justify each one, and
 * requiring it would produce a column full of full stops.
 *
 * Two of the six also need a person before they can be confirmed, and the dialog's confirm
 * button stays disabled until one is chosen rather than failing on submission.
 */
export function AssetLifecycleActions({
  asset,
  holders,
  busy,
  onAct,
}: AssetLifecycleActionsProps): React.JSX.Element {
  const actions = assetActions(asset)
  const [pending, setPending] = useState<AssetAction | null>(null)
  const [holderId, setHolderId] = useState('')
  const [note, setNote] = useState('')

  const close = (): void => {
    setPending(null)
    setHolderId('')
    setNote('')
  }

  const confirm = (): void => {
    if (pending === null) {
      return
    }

    if (pending.needsHolder && holderId === '') {
      return
    }

    onAct(pending, pending.needsHolder ? holderId : null, note.trim() === '' ? null : note.trim())
    close()
  }

  // The people a transfer can go to, minus whoever already holds it: issuing an asset to
  // the person who has it is refused with `assets.already_assigned_to_that_user`, because
  // succeeding would write a history line saying it moved from somebody to the same
  // somebody. Offering them would be offering a button that always fails.
  const candidates = holders.filter((person) => person.id !== asset.assignedToUserId)

  return (
    <>
      {actions.map((action, index) => (
        <Button
          key={action.id}
          variant={action.destructive ? 'destructive' : index === 0 ? 'default' : 'outline'}
          disabled={busy}
          onClick={() => {
            setPending(action)
            setHolderId('')
            setNote('')
          }}
        >
          {action.label}
        </Button>
      ))}

      {/*
        Edit is not a lifecycle action and is always available — correcting a mistyped
        serial on a retired asset is a thing somebody genuinely has to do, and the server
        accepts it. It sits last so it never displaces the move that carries the asset
        forward from the primary position.
      */}
      <Button variant="outline" render={<Link to={`/assets/${asset.id}/edit`} />}>
        <Pencil className="size-4" aria-hidden="true" />
        Edit
      </Button>

      <Dialog
        open={pending !== null}
        onOpenChange={(open) => {
          if (!open) {
            close()
          }
        }}
      >
        <DialogContent>
          {pending === null ? null : (
            <>
              <DialogHeader>
                <DialogTitle>{`${pending.label} — ${asset.assetTag}`}</DialogTitle>
                <DialogDescription>{pending.description}</DialogDescription>
              </DialogHeader>

              {pending.needsHolder ? (
                <div className="flex flex-col gap-1.5">
                  <Label
                    htmlFor="asset-action-holder"
                    className="text-field-label font-medium text-heading"
                  >
                    {pending.id === 'transfer' ? 'Transfer to' : 'Issue to'}
                    <span className="text-danger" aria-label="required">
                      *
                    </span>
                  </Label>
                  <Select
                    items={candidates.map((person) => ({
                      value: person.id,
                      label: person.displayName,
                    }))}
                    value={holderId === '' ? null : holderId}
                    onValueChange={(next: string | null) => {
                      setHolderId(next ?? '')
                    }}
                  >
                    <SelectTrigger id="asset-action-holder" size="default" className="w-full">
                      <SelectValue placeholder="Choose who is taking it on" />
                    </SelectTrigger>
                    <SelectContent>
                      {candidates.map((person) => (
                        <SelectItem key={person.id} value={person.id}>
                          {person.displayName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              ) : null}

              <div className="flex flex-col gap-1.5">
                <Label
                  htmlFor="asset-action-note"
                  className="text-field-label font-medium text-heading"
                >
                  Note
                </Label>
                <Textarea
                  id="asset-action-note"
                  rows={3}
                  maxLength={noteMaxLength}
                  placeholder="Optional. Which vendor, what failed, who authorised it."
                  value={note}
                  onChange={(event) => {
                    setNote(event.target.value)
                  }}
                />
                <p className="text-caption text-muted-foreground">
                  Whatever is written here stays on the asset’s history.
                </p>
              </div>

              <DialogFooter>
                <Button variant="outline" onClick={close}>
                  Back
                </Button>
                <Button
                  variant={pending.destructive ? 'destructive' : 'default'}
                  disabled={busy || (pending.needsHolder && holderId === '')}
                  onClick={confirm}
                >
                  {pending.label}
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>
    </>
  )
}
