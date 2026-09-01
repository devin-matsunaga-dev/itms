import { useRef, useState } from 'react'
import { Download, Lock, Paperclip } from 'lucide-react'
import { Panel } from '@/components/common/panel'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { formatBytes } from '@/lib/filesize'
import { formatDateTime } from '@/lib/datetime'
import type { TicketDetail } from '@/lib/api/types'
import { attachmentDownloadUrl } from '../api/tickets-api'

interface TicketAttachmentsProps {
  ticket: TicketDetail
  /** True for a Technician or an Admin, who alone may attach a file the requester cannot see. */
  canAttachInternal: boolean
  busy: boolean
  onUpload: (file: File, isInternal: boolean) => void
}

/**
 * A ticket's files: what is attached, and the control that adds one.
 *
 * The list is metadata only (WP-1.7). Bytes come from the download route, which is an
 * ordinary authenticated request — a plain link carries the session cookie, and the
 * endpoint re-checks the ticket and the audience every time and answers
 * `Content-Disposition: attachment` with `nosniff`, so nothing here can be rendered
 * inline as a document in this origin.
 *
 * **The accepted extensions and the size cap are deliberately not restated here.** They
 * are configuration the deployment sets (`Helpdesk:Attachments`), and a refusal comes
 * back as a problem document naming what is accepted. A hardcoded allowlist in the client
 * would be a second copy of a policy that can be changed without it.
 *
 * An internal attachment is absent from a requester's payload entirely rather than
 * redacted, so the flag on a row here is only ever seen by somebody inside the queue.
 */
export function TicketAttachments({
  ticket,
  canAttachInternal,
  busy,
  onUpload,
}: TicketAttachmentsProps): React.JSX.Element {
  const inputRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [isInternal, setIsInternal] = useState(false)

  const attachments = ticket.attachments ?? []

  const submit = (): void => {
    if (file === null) {
      return
    }

    onUpload(file, isInternal)
    setFile(null)
    setIsInternal(false)
    if (inputRef.current) {
      inputRef.current.value = ''
    }
  }

  return (
    <Panel
      icon={Paperclip}
      title={`Attachments${attachments.length === 0 ? '' : ` (${String(attachments.length)})`}`}
    >
      {attachments.length === 0 ? (
        <p className="text-copy text-muted-foreground">Nothing has been attached to this ticket.</p>
      ) : (
        <ul aria-label="Attached files" className="flex flex-col">
          {attachments.map((attachment, index) => (
            <li
              key={attachment.id}
              className={
                index === 0
                  ? 'flex items-center gap-3 py-2'
                  : 'flex items-center gap-3 border-t border-border py-2'
              }
            >
              <a
                href={attachmentDownloadUrl(ticket.id, attachment.id)}
                className="flex min-w-0 flex-1 items-center gap-2 rounded-md text-cell text-primary hover:underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
              >
                <Download className="size-4 shrink-0" aria-hidden="true" />
                <span className="truncate">{attachment.fileName}</span>
              </a>

              {attachment.isInternal ? (
                <span className="inline-flex shrink-0 items-center gap-1 rounded-md bg-warning/20 px-1.5 py-0.5 text-label font-semibold text-heading">
                  <Lock className="size-3" aria-hidden="true" />
                  Internal
                </span>
              ) : null}

              <span
                className="tabular shrink-0 text-caption text-muted-foreground"
                title={`${attachment.uploadedByName} · ${formatDateTime(attachment.createdAt)}`}
              >
                {formatBytes(attachment.byteLength)}
              </span>
            </li>
          ))}
        </ul>
      )}

      {ticket.hasMoreAttachments === true ? (
        <p className="mt-2 text-caption text-muted-foreground">
          Only the most recent attachments are shown.
        </p>
      ) : null}

      <div className="mt-4 flex flex-col gap-3 border-t border-border pt-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="attachment-file" className="text-field-label font-medium text-heading">
            Attach a file
          </Label>
          <Input
            id="attachment-file"
            ref={inputRef}
            type="file"
            className="h-auto py-2"
            disabled={busy}
            onChange={(event) => {
              setFile(event.target.files?.[0] ?? null)
            }}
          />
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3">
          {canAttachInternal ? (
            <div className="flex items-center gap-2">
              <Checkbox
                id="attachment-internal"
                checked={isInternal}
                disabled={busy}
                onCheckedChange={(checked: boolean) => {
                  setIsInternal(checked)
                }}
              />
              <Label htmlFor="attachment-internal" className="text-copy font-normal text-body">
                Internal — the requester cannot see this
              </Label>
            </div>
          ) : (
            <span />
          )}

          <Button variant="outline" disabled={busy || file === null} onClick={submit}>
            <Paperclip className="size-4" aria-hidden="true" />
            Attach
          </Button>
        </div>
      </div>
    </Panel>
  )
}
