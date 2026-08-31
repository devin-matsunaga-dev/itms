import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { LoginForm } from '@/features/auth/components/login-form'
import { ApiError } from '@/lib/api/client'
import { Roles } from '@/lib/roles'
import type { AuthenticatedUser } from '@/lib/api/generated-pending'
import { renderWithProviders } from '@/test/render'

const login = vi.fn<() => Promise<AuthenticatedUser>>()

vi.mock('@/features/auth/api/auth-api', () => ({
  login: () => login(),
  logout: vi.fn(),
  fetchCurrentUser: vi.fn(),
}))

const account: AuthenticatedUser = {
  id: '11111111-1111-1111-1111-111111111111',
  userName: 'admin',
  email: 'admin@itms.local',
  displayName: 'John Santos',
  roles: [Roles.admin],
  departmentId: null,
  locationId: null,
}

async function fillAndSubmit(person: ReturnType<typeof userEvent.setup>) {
  await person.type(screen.getByLabelText(/user name or email/i), 'admin')
  await person.type(screen.getByLabelText(/password/i), 'Dev!Passw0rd123')
  await person.click(screen.getByRole('button', { name: /sign in/i }))
}

beforeEach(() => {
  login.mockReset()
})

describe('LoginForm', () => {
  it('refuses to submit an empty form and says what is missing', async () => {
    const person = userEvent.setup()
    renderWithProviders(<LoginForm onSignedIn={vi.fn()} />)

    await person.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByText('Enter your user name or email address.')).toBeInTheDocument()
    expect(screen.getByText('Enter your password.')).toBeInTheDocument()
    expect(login).not.toHaveBeenCalled()
  })

  it('signs in and reports it to the caller', async () => {
    const onSignedIn = vi.fn()
    const person = userEvent.setup()
    login.mockResolvedValue(account)
    renderWithProviders(<LoginForm onSignedIn={onSignedIn} />)

    await fillAndSubmit(person)

    expect(onSignedIn).toHaveBeenCalledOnce()
  })

  it('shows the server message for bad credentials without naming which half was wrong', async () => {
    const person = userEvent.setup()
    login.mockRejectedValue(
      new ApiError(
        401,
        { status: 401, code: 'auth.invalid_credentials', detail: 'The user name or password is incorrect.' },
        'The user name or password is incorrect.',
      ),
    )
    renderWithProviders(<LoginForm onSignedIn={vi.fn()} />)

    await fillAndSubmit(person)

    expect(await screen.findByText('The user name or password is incorrect.')).toBeInTheDocument()
  })

  it('explains a locked account, which is the one failure the server names', async () => {
    const person = userEvent.setup()
    login.mockRejectedValue(
      new ApiError(
        401,
        { status: 401, code: 'auth.locked_out', detail: 'This account is temporarily locked because of repeated failed sign-ins. Try again later.' },
        'This account is temporarily locked because of repeated failed sign-ins. Try again later.',
      ),
    )
    renderWithProviders(<LoginForm onSignedIn={vi.fn()} />)

    await fillAndSubmit(person)

    expect(await screen.findByText(/temporarily locked/i)).toBeInTheDocument()
  })

  it('explains a rate-limited address rather than showing a bare 429', async () => {
    const person = userEvent.setup()
    login.mockRejectedValue(new ApiError(429, null, 'Too Many Requests'))
    renderWithProviders(<LoginForm onSignedIn={vi.fn()} />)

    await fillAndSubmit(person)

    expect(await screen.findByText(/too many sign-in attempts/i)).toBeInTheDocument()
  })

  it('maps the server field errors back onto the fields', async () => {
    const person = userEvent.setup()
    login.mockRejectedValue(
      new ApiError(
        400,
        {
          status: 400,
          code: 'validation.failed',
          errors: { userName: ['That user name is too long.'] },
        },
        'One or more fields are invalid.',
      ),
    )
    renderWithProviders(<LoginForm onSignedIn={vi.fn()} />)

    await fillAndSubmit(person)

    expect(await screen.findByText('That user name is too long.')).toBeInTheDocument()
    expect(screen.getByLabelText(/user name or email/i)).toHaveAttribute('aria-invalid', 'true')
  })

  it('announces the failure where a screen reader will hear it', async () => {
    const person = userEvent.setup()
    login.mockRejectedValue(new ApiError(401, null, 'The user name or password is incorrect.'))
    renderWithProviders(<LoginForm onSignedIn={vi.fn()} />)

    await fillAndSubmit(person)

    const live = await screen.findByText('The user name or password is incorrect.')
    expect(live).toHaveAttribute('aria-live', 'assertive')
  })
})
