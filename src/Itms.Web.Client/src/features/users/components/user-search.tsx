import { useEffect, useState } from 'react'
import { Search, X } from 'lucide-react'
import { Input } from '@/components/ui/input'

/** How long to wait after the last keystroke before asking the server. */
const debounceMs = 300

interface UserSearchProps {
  /** The term the URL is currently carrying. */
  value: string
  onChange: (search: string) => void
}

/**
 * The directory's free-text search over a person's display name and email address
 * (WP-2.7).
 *
 * **The sign-in name is deliberately not searched, and the placeholder does not offer it.**
 * `UserSummary` carries no `userName` — it never leaves Identity — and matching on a field
 * the caller can never see produces results that look arbitrary from outside. The server's
 * own note on `ListUsersQuery.Search` says the same thing from the other side.
 *
 * **This is the one control on the screen that holds a draft.** Every other filter writes
 * straight through to the URL, because a select changes once per decision — but a search
 * box changes once per keystroke, and putting each of those in the address would push a
 * dozen entries onto the history stack and fire a dozen queries for one word. So the input
 * is local and the URL is written after a pause. The address and the box can disagree for
 * 300ms; they are reconciled whenever the URL moves underneath, by the render-time
 * adjustment below. The register's search box (WP-2.6a) is where this shape was settled.
 */
export function UserSearch({ value, onChange }: UserSearchProps): React.JSX.Element {
  const [draft, setDraft] = useState(value)
  const [lastValue, setLastValue] = useState(value)

  // The URL is the truth. When it moves for a reason that is not this box — "Clear all", a
  // filter change, the back button — the box follows it.
  if (value !== lastValue) {
    setLastValue(value)
    setDraft(value)
  }

  // A real effect, this one: a timer is an external system.
  useEffect(() => {
    if (draft === value) {
      return
    }

    const timer = setTimeout(() => {
      onChange(draft)
    }, debounceMs)

    return () => {
      clearTimeout(timer)
    }
  }, [draft, onChange, value])

  return (
    <div className="relative min-w-64 flex-1">
      <Search
        className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
        aria-hidden="true"
      />
      <Input
        type="search"
        aria-label="Search people"
        placeholder="Search by name or email address…"
        className="pl-9"
        value={draft}
        onChange={(event) => {
          setDraft(event.target.value)
        }}
        onKeyDown={(event) => {
          // Enter asks now rather than waiting out the debounce; Escape abandons the term.
          if (event.key === 'Enter') {
            onChange(draft)
          }
          if (event.key === 'Escape') {
            setDraft('')
            onChange('')
          }
        }}
      />
      {draft.length > 0 ? (
        <button
          type="button"
          aria-label="Clear the search"
          className="absolute top-1/2 right-2 -translate-y-1/2 rounded-sm p-1 text-muted-foreground transition-colors hover:text-heading focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
          onClick={() => {
            setDraft('')
            onChange('')
          }}
        >
          <X className="size-4" aria-hidden="true" />
        </button>
      ) : null}
    </div>
  )
}
