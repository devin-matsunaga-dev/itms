/**
 * The create form's shape, as zod (CONVENTIONS.md: react-hook-form + zod, the schema
 * shared with the display layer).
 *
 * The bounds are the server's own — `Ticket.SubjectMaxLength` and
 * `Ticket.DescriptionMaxLength`, which `CreateTicketValidator` enforces and the columns
 * hold. Checking them here does not make the server's check redundant: it means somebody
 * typing an eight-thousand-and-first character is told at the field rather than by a
 * round trip, and everything the server refuses still comes back mapped onto the field
 * that caused it.
 *
 * Two fields are deliberately not validated for existence. Whether a category has been
 * retired, or a requester's account deactivated, is a question only the database can
 * answer, and a client that tried would still lose the race to the row it read — the
 * same call `CreateTicketValidator`'s own remarks make.
 */

import { z } from 'zod'

/** `Ticket.SubjectMaxLength`. */
export const subjectMaxLength = 200

/** `Ticket.DescriptionMaxLength`. */
export const descriptionMaxLength = 8000

/**
 * SPEC.md §2 calls this the title and a form must label it so. The wire calls it
 * `subject`, because `TicketCreated.Subject` has been frozen in `Itms.Contracts` since
 * WP-0.3 — the mismatch is internal and stops here.
 */
export const newTicketSchema = z.object({
  subject: z
    .string()
    .trim()
    .min(1, 'Enter a title for the ticket.')
    .max(subjectMaxLength, `A title cannot be longer than ${String(subjectMaxLength)} characters.`),
  description: z
    .string()
    .trim()
    .min(1, 'Describe what is wrong.')
    .max(
      descriptionMaxLength,
      `A description cannot be longer than ${String(descriptionMaxLength)} characters.`,
    ),
  categoryId: z.string().min(1, 'Choose a category.'),
  priorityId: z.string().min(1, 'Choose a priority.'),
  /**
   * Empty means "for me". Only a Technician or an Admin is offered the field at all, and
   * a User naming somebody else is refused by the server with 403 rather than quietly
   * coerced (WP-1.5).
   */
  requesterId: z.string(),
  /** Empty means "take the requester's own", which is what the server does. */
  departmentId: z.string(),
})

export type NewTicketForm = z.infer<typeof newTicketSchema>

/** What the form holds before anything is typed. */
export const emptyNewTicket: NewTicketForm = {
  subject: '',
  description: '',
  categoryId: '',
  priorityId: '',
  requesterId: '',
  departmentId: '',
}
