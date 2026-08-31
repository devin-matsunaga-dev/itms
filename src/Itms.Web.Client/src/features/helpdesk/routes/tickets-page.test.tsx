import { describe, expect, it } from 'vitest'
import { screen } from '@testing-library/react'
import { TicketsPage } from '@/features/helpdesk/routes/tickets-page'
import { renderWithProviders } from '@/test/render'

describe('TicketsPage', () => {
  it('offers ticket creation from the screen the ticket belongs to', () => {
    renderWithProviders(<TicketsPage />)

    // The action moved off the sidebar and onto this page's header; the empty state
    // offers the same action, which is what DESIGN.md asks an empty state to do.
    expect(screen.getAllByRole('button', { name: /new ticket/i })).toHaveLength(2)
  })

  it('says what will fill the screen rather than showing an empty table', () => {
    renderWithProviders(<TicketsPage />)

    expect(screen.getByText('No ticket queue yet')).toBeInTheDocument()
  })
})
