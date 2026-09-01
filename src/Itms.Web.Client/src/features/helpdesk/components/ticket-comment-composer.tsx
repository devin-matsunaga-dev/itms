import { useState } from 'react'
import { Lock, Send } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

/** `TicketComment.BodyMaxLength`, which is `Ticket.DescriptionMaxLength`. */
const bodyMaxLength = 8000

interface TicketCommentComposerProps {
  /** True for a Technician or an Admin. An end user is not offered the internal toggle. */
  canWriteInternal: boolean
  busy: boolean
  onSubmit: (body: string, isInternal: boolean) => void
}

/**
 * The composer for a comment, or for a note only the queue can read.
 *
 * WP-1.7's criterion asks for "a clear visual distinction", and it is worth more here
 * than anywhere else on the screen: an internal note cannot be unpublished. There is no
 * method that clears the flag, by design — text written on the understanding it would not
 * be seen must not become visible by a later click — so the moment to be unambiguous
 * about the audience is *before* it is posted, not after.
 *
 * So the whole composer changes: the surface takes the `warning` wash, a lock appears,
 * and the button says what it will do. The checkbox alone would be a four-pixel
 * difference between a public reply and a private one.
 *
 * The toggle is withheld from an end user rather than shown and refused. That is not the
 * enforcement — the server answers `helpdesk.internal_not_permitted` with a 403, and
 * would do so if this were hand-crafted.
 */
export function TicketCommentComposer({
  canWriteInternal,
  busy,
  onSubmit,
}: TicketCommentComposerProps): React.JSX.Element {
  const [body, setBody] = useState('')
  const [isInternal, setIsInternal] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = (): void => {
    const trimmed = body.trim()
    if (trimmed.length === 0) {
      setError('Write something before posting.')
      return
    }

    onSubmit(trimmed, isInternal)
    setBody('')
    setIsInternal(false)
    setError(null)
  }

  return (
    <div
      className={cn(
        'rounded-tile border p-4 transition-colors duration-150',
        isInternal
          ? 'border-warning/40 bg-warning/12 dark:bg-warning/15'
          : 'border-border bg-canvas',
      )}
    >
      <Label htmlFor="comment-body" className="text-field-label font-medium text-heading">
        {isInternal ? 'Internal note' : 'Add a comment'}
      </Label>

      <Textarea
        id="comment-body"
        className="mt-1.5 bg-surface"
        rows={4}
        maxLength={bodyMaxLength}
        placeholder={
          isInternal
            ? 'Only technicians and administrators will see this.'
            : 'The requester will see this.'
        }
        value={body}
        aria-invalid={error !== null}
        aria-describedby={error === null ? undefined : 'comment-body-error'}
        onChange={(event) => {
          setBody(event.target.value)
          setError(null)
        }}
      />

      {error === null ? null : (
        <p id="comment-body-error" className="mt-1.5 text-caption text-danger">
          {error}
        </p>
      )}

      <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
        {canWriteInternal ? (
          <div className="flex items-center gap-2">
            <Checkbox
              id="comment-internal"
              checked={isInternal}
              disabled={busy}
              onCheckedChange={(checked: boolean) => {
                setIsInternal(checked)
              }}
            />
            <Label htmlFor="comment-internal" className="text-copy font-normal text-body">
              <Lock
                className={cn('size-3.5', isInternal ? 'text-warning' : 'text-muted-foreground')}
                aria-hidden="true"
              />
              Internal note — the requester cannot see this
            </Label>
          </div>
        ) : (
          <p className="text-caption text-muted-foreground">
            Your technician will be notified of your reply.
          </p>
        )}

        <Button disabled={busy} onClick={submit}>
          <Send className="size-4" aria-hidden="true" />
          {isInternal ? 'Post internal note' : 'Post comment'}
        </Button>
      </div>
    </div>
  )
}
