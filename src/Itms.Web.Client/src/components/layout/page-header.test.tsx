import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { PageHeader } from '@/components/layout/page-header'
import { formatDate } from '@/lib/datetime'

describe('PageHeader', () => {
  it('renders the title as the page heading, with its subtitle', () => {
    render(<PageHeader title="Tickets" subtitle="Every request raised." />)

    expect(screen.getByRole('heading', { level: 1, name: 'Tickets' })).toBeInTheDocument()
    expect(screen.getByText('Every request raised.')).toBeInTheDocument()
  })

  it('renders the screen’s actions on the right', () => {
    render(
      <PageHeader title="Tickets" subtitle="…" actions={<button type="button">New Ticket</button>} />,
    )

    expect(screen.getByRole('button', { name: 'New Ticket' })).toBeInTheDocument()
  })

  it('states no date — the clock is in the topbar, once for the whole application', () => {
    render(<PageHeader title="Tickets" subtitle="…" />)

    expect(screen.queryByText(formatDate(new Date()))).not.toBeInTheDocument()
  })
})
