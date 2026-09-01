import { useState } from 'react'
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
import { Textarea } from '@/components/ui/textarea'
import type { TicketDetail, TicketStatus } from '@/lib/api/types'
import { transitionActions, type TransitionAction } from '../lib/ticket-transitions'
import { statusLabels } from '../lib/ticket-display'

/** `Ticket.ResolutionNotesMaxLength`. */
const resolutionNotesMaxLength = 8000

interface TicketTransitionButtonsProps {
  ticket: TicketDetail
  /** True while a write is in flight, so a transition cannot be pressed twice. */
  busy: boolean
  onMove: (status: TicketStatus, resolutionNotes: string | null) => void
}

/**
 * The transition buttons for a ticket.
 *
 * Every button here came from the server's `allowedNextStatuses` — WP-1.10's criterion is
 * that an illegal transition is *not rendered*, and the only way that stays true as
 * `TicketStateMachine`'s table changes is for the buttons to be told rather than to
 * decide. `transitionActions` explains the two destinations that appear in that list and
 * are still not buttons.
 *
 * Two moves stop for a dialog first: resolving, because the server requires non-blank
 * notes and refuses them on every other destination (WP-1.3), and closing or cancelling,
 * because both are one-way. Cancelled is terminal by WP-1.3's reading of SPEC.md's
 * silence, so a mis-click there is not recoverable through this screen.
 */
export function TicketTransitionButtons({
  ticket,
  busy,
  onMove,
}: TicketTransitionButtonsProps): React.JSX.Element | null {
  const actions = transitionActions(ticket.status, ticket.allowedNextStatuses)
  const [pendingAction, setPendingAction] = useState<TransitionAction | null>(null)
  const [notes, setNotes] = useState('')
  const [notesError, setNotesError] = useState<string | null>(null)

  if (actions.length === 0) {
    return null
  }

  const close = (): void => {
    setPendingAction(null)
    setNotes('')
    setNotesError(null)
  }

  const start = (action: TransitionAction): void => {
    if (action.requiresNotes || action.confirms) {
      setPendingAction(action)
      setNotes('')
      setNotesError(null)
      return
    }
    onMove(action.status, null)
  }

  const confirm = (): void => {
    if (pendingAction === null) {
      return
    }

    if (pendingAction.requiresNotes) {
      const trimmed = notes.trim()
      if (trimmed.length === 0) {
        setNotesError('Describe what was done to resolve the ticket.')
        return
      }
      onMove(pendingAction.status, trimmed)
      close()
      return
    }

    onMove(pendingAction.status, null)
    close()
  }

  return (
    <>
      {actions.map((action, index) => (
        <Button
          key={action.status}
          variant={action.destructive ? 'destructive' : index === 0 ? 'default' : 'outline'}
          disabled={busy}
          onClick={() => {
            start(action)
          }}
        >
          {action.label}
        </Button>
      ))}

      <Dialog
        open={pendingAction !== null}
        onOpenChange={(open) => {
          if (!open) {
            close()
          }
        }}
      >
        <DialogContent>
          {pendingAction === null ? null : (
            <>
              <DialogHeader>
                <DialogTitle>
                  {pendingAction.requiresNotes
                    ? `Resolve ${ticket.number}`
                    : `${pendingAction.label} — ${ticket.number}`}
                </DialogTitle>
                <DialogDescription>
                  {pendingAction.requiresNotes
                    ? 'Say what was done. The requester reads this, and it stays on the ticket if it is reopened.'
                    : `This moves the ticket to ${statusLabels[pendingAction.status]}, which it cannot be moved out of.`}
                </DialogDescription>
              </DialogHeader>

              {pendingAction.requiresNotes ? (
                <div className="flex flex-col gap-1.5">
                  <Label
                    htmlFor="resolution-notes"
                    className="text-field-label font-medium text-heading"
                  >
                    Resolution notes
                    <span className="text-danger" aria-hidden="true">
                      *
                    </span>
                  </Label>
                  <Textarea
                    id="resolution-notes"
                    rows={5}
                    maxLength={resolutionNotesMaxLength}
                    value={notes}
                    aria-invalid={notesError !== null}
                    aria-describedby={notesError === null ? undefined : 'resolution-notes-error'}
                    onChange={(event) => {
                      setNotes(event.target.value)
                      setNotesError(null)
                    }}
                  />
                  {notesError === null ? null : (
                    <p id="resolution-notes-error" className="text-caption text-danger">
                      {notesError}
                    </p>
                  )}
                </div>
              ) : null}

              <DialogFooter>
                <Button variant="outline" onClick={close}>
                  Back
                </Button>
                <Button
                  variant={pendingAction.destructive ? 'destructive' : 'default'}
                  disabled={busy}
                  onClick={confirm}
                >
                  {pendingAction.label}
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>
    </>
  )
}
