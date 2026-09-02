/**
 * The directory forms' shapes, as zod (CONVENTIONS.md: react-hook-form + zod, the schema
 * shared with the display layer).
 *
 * ## The bounds are the server's own
 *
 * `Department.NameMaxLength` and its siblings, which the validators enforce and the columns
 * hold. Checking them here does not make the server's check redundant: it means somebody
 * typing one character too many is told at the field rather than by a round trip, and
 * everything the server refuses still comes back mapped onto the field that caused it.
 *
 * ## What is deliberately not validated here
 *
 * Whether a name is already taken — by another department, or by a sibling under the same
 * parent — is a question only the database can answer, and a client that tried would still
 * lose the race to the row it read. Both come back as a 409 naming the collision.
 *
 * **Nor is the hierarchy.** Whether a Room may sit under a Floor is
 * `LocationHierarchy`'s rule, resolved server-side precisely so no client holds a second
 * copy of it (WP-2.4), and the refusal it sends names the whole hierarchy in one sentence.
 * A rank table in this file would be that second copy.
 */

import { z } from 'zod'

/** `Department.NameMaxLength`, and the same bound for a location's name. */
export const nameMaxLength = 128

/** `Department.CodeMaxLength`. */
export const codeMaxLength = 32

/** `Department.DescriptionMaxLength`, and the same bound for a location's. */
export const descriptionMaxLength = 512

export const departmentFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'A department needs a name.')
    .max(nameMaxLength, `A name may be at most ${String(nameMaxLength)} characters.`),
  /** Optional, and unique when present — "FIN", "IT". Empty means none. */
  code: z
    .string()
    .trim()
    .max(codeMaxLength, `A code may be at most ${String(codeMaxLength)} characters.`),
  description: z
    .string()
    .trim()
    .max(
      descriptionMaxLength,
      `A description may be at most ${String(descriptionMaxLength)} characters.`,
    ),
})

export type DepartmentFormValues = z.infer<typeof departmentFormSchema>

/** The six levels SPEC.md §5 names, in the order the hierarchy runs. */
export const locationKinds = ['Organization', 'Site', 'Building', 'Floor', 'Area', 'Room'] as const

export const locationFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'A location needs a name.')
    .max(nameMaxLength, `A name may be at most ${String(nameMaxLength)} characters.`),
  kind: z.enum(locationKinds),
  description: z
    .string()
    .trim()
    .max(
      descriptionMaxLength,
      `A description may be at most ${String(descriptionMaxLength)} characters.`,
    ),
})

export type LocationFormValues = z.infer<typeof locationFormSchema>

/** An empty string is "none", which is what the API's `null` means for these fields. */
export function orNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed.length === 0 ? null : trimmed
}
