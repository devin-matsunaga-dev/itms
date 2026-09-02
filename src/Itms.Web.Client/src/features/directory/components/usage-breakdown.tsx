import type { UsageCount } from '@/lib/api/types'

interface UsageBreakdownProps {
  references: readonly UsageCount[]
  /** What to say when every module reports zero. */
  emptyMessage: string
}

/**
 * What still points at a department or a location, per module (WP-2.4).
 *
 * The counts come from `IDirectoryUsageLookup`, which every owning module implements — so
 * this is Assets saying how much equipment is in the room and Identity saying how many
 * people, neither of them a foreign key the database could have answered from (§3 rule 6).
 *
 * **A module reporting zero is shown rather than dropped.** "No assets here" is an answer,
 * and a breakdown that silently omits the modules with nothing in them reads as though
 * those modules were never asked. The order is the server's, by entity name, so two reads
 * of one entry render identically.
 */
export function UsageBreakdown({
  references,
  emptyMessage,
}: UsageBreakdownProps): React.JSX.Element {
  const total = references.reduce((sum, reference) => sum + reference.count, 0)

  if (total === 0) {
    return <p className="text-copy text-body">{emptyMessage}</p>
  }

  return (
    <ul className="flex flex-col gap-1">
      {references.map((reference) => (
        <li key={reference.entityName} className="flex justify-between gap-4 text-copy">
          <span className="text-body">{reference.entityName}</span>
          <span className="tabular font-semibold text-heading">{reference.count}</span>
        </li>
      ))}
    </ul>
  )
}
