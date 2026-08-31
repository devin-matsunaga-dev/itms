import { afterEach, describe, expect, it, vi } from 'vitest'
import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Sidebar } from '@/components/layout/sidebar'
import { Roles } from '@/lib/roles'
import { resetTheme } from '@/lib/theme'
import { renderWithProviders } from '@/test/render'

function renderSidebar(roles: string[], collapsed = false) {
  const onToggleCollapsed = vi.fn()
  const result = renderWithProviders(
    <Sidebar roles={roles} collapsed={collapsed} onToggleCollapsed={onToggleCollapsed} />,
  )
  return { ...result, onToggleCollapsed }
}

afterEach(() => {
  resetTheme()
})

function navLabels(): string[] {
  const nav = screen.getByRole('navigation', { name: 'Main' })
  return within(nav)
    .getAllByRole('link')
    .map((link) => link.textContent?.trim() ?? '')
}

describe('Sidebar nav filtering', () => {
  it('offers an Admin every destination, in the order DESIGN.md fixes', () => {
    renderSidebar([Roles.admin])

    expect(navLabels()).toEqual([
      'Dashboard',
      'Tickets',
      'Assets',
      'Users',
      'Monitoring',
      'Alerts',
      'Knowledge Base',
      'Reports',
      'Administration',
    ])
  })

  it('hides Administration from a Technician and offers the rest', () => {
    renderSidebar([Roles.technician])

    expect(navLabels()).toEqual([
      'Dashboard',
      'Tickets',
      'Assets',
      'Users',
      'Monitoring',
      'Alerts',
      'Knowledge Base',
      'Reports',
    ])
  })

  it('leaves a User their own tickets and the knowledge base', () => {
    renderSidebar([Roles.user])

    expect(navLabels()).toEqual(['Dashboard', 'Tickets', 'Knowledge Base'])
  })

  it('omits an item rather than disabling it in place', () => {
    renderSidebar([Roles.user])

    expect(screen.queryByRole('link', { name: 'Administration' })).not.toBeInTheDocument()
    expect(screen.queryByText('Administration')).not.toBeInTheDocument()
  })

  it('offers nothing but the unrestricted items to an account with no role', () => {
    renderSidebar([])

    expect(navLabels()).toEqual(['Dashboard', 'Tickets', 'Knowledge Base'])
  })
})

describe('Sidebar controls', () => {
  it('keeps every destination reachable by name when collapsed', () => {
    renderSidebar([Roles.admin], true)

    // The label is visually hidden, not removed: the link keeps its accessible name.
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeInTheDocument()
    expect(screen.getByRole('switch', { name: 'Dark mode' })).toBeInTheDocument()
  })

  it('reports its expanded state and asks to be toggled', async () => {
    const user = userEvent.setup()
    const { onToggleCollapsed } = renderSidebar([Roles.admin])

    const collapse = screen.getByRole('button', { name: 'Collapse' })
    expect(collapse).toHaveAttribute('aria-expanded', 'true')

    await user.click(collapse)
    expect(onToggleCollapsed).toHaveBeenCalledOnce()
  })

  it('does not offer ticket creation — that belongs to the Tickets screen', () => {
    renderSidebar([Roles.technician])

    expect(screen.queryByRole('button', { name: /new ticket/i })).not.toBeInTheDocument()
  })
})

describe('Sidebar colour scheme switch', () => {
  it('offers the mode it would move to, and reports the mode in force', () => {
    renderSidebar([Roles.admin])

    const toggle = screen.getByRole('switch', { name: 'Dark mode' })
    expect(toggle).toHaveAttribute('aria-checked', 'false')
    expect(document.documentElement).not.toHaveClass('dark')
  })

  it('switches the document to dark and back', async () => {
    const user = userEvent.setup()
    renderSidebar([Roles.admin])

    await user.click(screen.getByRole('switch', { name: 'Dark mode' }))

    expect(document.documentElement).toHaveClass('dark')
    const toggle = screen.getByRole('switch', { name: 'Light mode' })
    expect(toggle).toHaveAttribute('aria-checked', 'true')

    await user.click(toggle)

    expect(document.documentElement).not.toHaveClass('dark')
    expect(screen.getByRole('switch', { name: 'Dark mode' })).toBeInTheDocument()
  })

  it('remembers the choice, so a reload does not undo it', async () => {
    const user = userEvent.setup()
    renderSidebar([Roles.admin])

    await user.click(screen.getByRole('switch', { name: 'Dark mode' }))

    expect(localStorage.getItem('itms.theme')).toBe('dark')
  })
})

describe('Sidebar brand block', () => {
  it('names the organisation, not the system', () => {
    renderSidebar([Roles.admin])

    expect(screen.getByText('Commonwealth Utilities Corporation')).toBeInTheDocument()
    expect(screen.getByText('Unified IT Management')).toBeInTheDocument()
  })

  it('hides the wordmark when collapsed, leaving the mark', () => {
    renderSidebar([Roles.admin], true)

    expect(screen.queryByText('Commonwealth Utilities Corporation')).not.toBeInTheDocument()
  })
})
