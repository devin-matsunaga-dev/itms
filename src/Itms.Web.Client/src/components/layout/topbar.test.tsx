import { afterEach, describe, expect, it, vi } from 'vitest'
import { act, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Topbar } from '@/components/layout/topbar'
import { formatDate } from '@/lib/datetime'
import { Roles } from '@/lib/roles'
import type { AuthenticatedUser } from '@/lib/api/generated-pending'
import { renderWithProviders } from '@/test/render'

const user: AuthenticatedUser = {
  id: '11111111-1111-1111-1111-111111111111',
  userName: 'admin',
  email: 'admin@itms.local',
  displayName: 'John Santos',
  roles: [Roles.admin],
  departmentId: null,
  locationId: null,
}

describe('Topbar', () => {
  it('names the account and the role it acts under', () => {
    renderWithProviders(
      <Topbar user={user} onSearch={vi.fn()} onSignOut={vi.fn()} signingOut={false} />,
    )

    expect(screen.getByText('John Santos')).toBeInTheDocument()
    expect(screen.getByText('Administrator')).toBeInTheDocument()
  })

  it('renders the notification and message icons without a count', () => {
    renderWithProviders(
      <Topbar user={user} onSearch={vi.fn()} onSignOut={vi.fn()} signingOut={false} />,
    )

    // There is no notifications module yet (Phase 4). A badge here would be a claim
    // about the system that is not true, so nothing numeric may appear on these.
    const bell = screen.getByRole('button', { name: 'Notifications' })
    const messages = screen.getByRole('button', { name: 'Messages' })
    expect(bell.parentElement?.textContent).toBe('')
    expect(messages.parentElement?.textContent).toBe('')
  })

  it('opens the search palette from the pill', async () => {
    const onSearch = vi.fn()
    const person = userEvent.setup()
    renderWithProviders(
      <Topbar user={user} onSearch={onSearch} onSignOut={vi.fn()} signingOut={false} />,
    )

    await person.click(screen.getByRole('button', { name: /search anything/i }))
    expect(onSearch).toHaveBeenCalledOnce()
  })

  it('signs out from the account menu', async () => {
    const onSignOut = vi.fn()
    const person = userEvent.setup()
    renderWithProviders(
      <Topbar user={user} onSearch={vi.fn()} onSignOut={onSignOut} signingOut={false} />,
    )

    await person.click(screen.getByRole('button', { name: /john santos/i }))
    await person.click(await screen.findByRole('menuitem', { name: /sign out/i }))

    expect(onSignOut).toHaveBeenCalledOnce()
  })
})

describe('Topbar clock', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('states today’s date and the time, quietly and without an icon', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 4, 23, 10, 30))

    renderWithProviders(
      <Topbar user={user} onSearch={vi.fn()} onSignOut={vi.fn()} signingOut={false} />,
    )

    const date = screen.getByText(formatDate(new Date(2026, 4, 23, 10, 30)))
    expect(date).toBeInTheDocument()
    expect(screen.getByText(/^Saturday, /)).toBeInTheDocument()

    // It is context, not a control: caption-sized, no tile, no icon.
    expect(date).toHaveClass('text-caption')
    expect(date.closest('span')?.querySelector('svg')).toBeNull()
  })

  it('sits beside the account it is signed in as', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 4, 23, 10, 30))

    renderWithProviders(
      <Topbar user={user} onSearch={vi.fn()} onSignOut={vi.fn()} signingOut={false} />,
    )

    const date = screen.getByText(formatDate(new Date(2026, 4, 23, 10, 30)))
    const account = screen.getByRole('button', { name: /john santos/i })

    // Adjacent in the document, in that order — the clock reads as part of the
    // account corner rather than as a fourth toolbar item.
    expect(date.parentElement?.nextElementSibling).toBe(account)
  })

  it('keeps ticking, because the topbar mounts once and never remounts', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 4, 23, 10, 30))

    renderWithProviders(
      <Topbar user={user} onSearch={vi.fn()} onSignOut={vi.fn()} signingOut={false} />,
    )

    const before = screen.getByText(/^Saturday, /).textContent

    await act(async () => {
      vi.advanceTimersByTime(60_000)
    })

    expect(screen.getByText(/^Saturday, /).textContent).not.toBe(before)
  })
})
