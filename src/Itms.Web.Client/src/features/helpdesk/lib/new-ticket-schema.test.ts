import { describe, expect, it } from 'vitest'
import {
  descriptionMaxLength,
  emptyNewTicket,
  newTicketSchema,
  subjectMaxLength,
} from './new-ticket-schema'

const valid = {
  subject: 'Laptop will not connect to Wi-Fi',
  description: 'It drops the connection every few minutes.',
  categoryId: 'cat-network',
  priorityId: 'pri-high',
  requesterId: '',
  departmentId: '',
}

describe('newTicketSchema', () => {
  it('accepts a ticket filed for the caller, with no requester and no department', () => {
    expect(newTicketSchema.safeParse(valid).success).toBe(true)
  })

  it('names every required field that was left empty', () => {
    const result = newTicketSchema.safeParse(emptyNewTicket)

    expect(result.success).toBe(false)
    const fields = result.error?.issues.map((issue) => issue.path.join('.')) ?? []
    expect(fields).toEqual(
      expect.arrayContaining(['subject', 'description', 'categoryId', 'priorityId']),
    )
  })

  it('refuses whitespace as a title, which the server would refuse too', () => {
    const result = newTicketSchema.safeParse({ ...valid, subject: '   ' })

    expect(result.success).toBe(false)
    expect(result.error?.issues[0]?.message).toBe('Enter a title for the ticket.')
  })

  it('holds the bounds the columns hold', () => {
    expect(newTicketSchema.safeParse({ ...valid, subject: 'x'.repeat(subjectMaxLength) }).success).toBe(
      true,
    )
    expect(
      newTicketSchema.safeParse({ ...valid, subject: 'x'.repeat(subjectMaxLength + 1) }).success,
    ).toBe(false)
    expect(
      newTicketSchema.safeParse({ ...valid, description: 'x'.repeat(descriptionMaxLength + 1) })
        .success,
    ).toBe(false)
  })

  it('trims what it accepts, so a title is stored as it reads', () => {
    const result = newTicketSchema.safeParse({ ...valid, subject: '  Printer jam  ' })

    expect(result.success).toBe(true)
    expect(result.data?.subject).toBe('Printer jam')
  })
})
