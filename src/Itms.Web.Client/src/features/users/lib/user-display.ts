import type { Department, Location } from '@/lib/api/types'

/**
 * How the directory renders the two things a user carries as bare identifiers.
 *
 * A user row holds a `departmentId` and a `locationId` and nothing else — §3 rule 6
 * forbids the foreign key that would let Identity join Directory's tables, and
 * `UserSummary` deliberately carries no cached name the way an asset row does. So the
 * names are resolved on the client, from the directory reads the screen already holds.
 */

/** What a resolved name is: the text to render, and whether it is the real one. */
export interface ResolvedName {
  readonly text: string
  /**
   * False when the record names something the lookup did not contain.
   *
   * **Not the same as having none**, and the difference is what this flag exists for. The
   * flat location read is one page of two hundred (WP-2.6a), so on a large estate a room
   * can be perfectly real and simply absent from it. Rendering an em dash there would say
   * "this person has no location recorded", which is a different and false statement. The
   * caller says "not listed" instead, and the cascading picker and the 360's own ancestor
   * read are the two places that are never wrong.
   */
  readonly known: boolean
}

/** Nobody has been placed there, and the record says so. */
export const unset: ResolvedName = { text: '—', known: true }

/** The department's name, or an honest stand-in. */
export function departmentName(
  departmentId: string | null | undefined,
  departments: readonly Department[],
): ResolvedName {
  if (departmentId === null || departmentId === undefined) {
    return unset
  }

  const match = departments.find((department) => department.id === departmentId)
  return match === undefined
    ? { text: 'Not listed', known: false }
    : { text: match.name, known: true }
}

/**
 * The location's full path, or an honest stand-in.
 *
 * The **path** rather than the room's own name, for the reason the picker renders one:
 * three buildings can each have a "Server Room", and the name alone does not say which.
 */
export function locationPath(
  locationId: string | null | undefined,
  locations: readonly Location[],
): ResolvedName {
  if (locationId === null || locationId === undefined) {
    return unset
  }

  const match = locations.find((location) => location.id === locationId)
  return match === undefined
    ? { text: 'Not listed', known: false }
    : { text: match.path, known: true }
}

/**
 * How an account's roles read in a cell.
 *
 * Every role it holds, in the order the server sorted them, rather than only the most
 * privileged: the topbar's `primaryRole` answers "who am I signed in as", where a
 * directory answers "what can this person do", and an account holding two is a fact
 * somebody administering the system needs to see rather than have collapsed.
 */
export function roleLabel(roles: readonly string[]): string {
  return roles.length === 0 ? 'No role assigned' : roles.join(', ')
}
