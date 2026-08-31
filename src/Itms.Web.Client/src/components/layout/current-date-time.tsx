import { formatDate, formatDateTime, formatTime, formatWeekday } from '@/lib/datetime'
import { useNow } from '@/lib/use-now'

/**
 * Today's date and the time, in the viewer's own timezone (DESIGN.md §3). It sits in
 * the topbar beside the account block rather than in each page header, so it is stated
 * once for the whole application instead of once per screen — and quietly, at caption
 * size with no icon, because it is context and not a control.
 */
export function CurrentDateTime(): React.JSX.Element {
  const now = useNow()

  return (
    <span
      className="flex shrink-0 flex-col items-end leading-tight"
      // The full absolute value, per DESIGN.md §6, without spending a second line on it.
      title={formatDateTime(now)}
    >
      <span className="text-caption font-semibold text-heading tabular">{formatDate(now)}</span>
      <span className="text-caption text-muted-foreground tabular">
        {formatWeekday(now)}, {formatTime(now)}
      </span>
    </span>
  )
}
