import { describe, expect, it } from 'vitest'
import { hasAnyRole, initials, primaryRole, Roles } from '@/lib/roles'

describe('hasAnyRole', () => {
  it('allows anyone when no role is required', () => {
    expect(hasAnyRole([Roles.user], [])).toBe(true)
  })

  it('matches on any one of the allowed roles', () => {
    expect(hasAnyRole([Roles.technician], [Roles.admin, Roles.technician])).toBe(true)
  })

  it('refuses a role that is not allowed', () => {
    expect(hasAnyRole([Roles.user], [Roles.admin, Roles.technician])).toBe(false)
  })
})

describe('primaryRole', () => {
  it('names the most privileged role held', () => {
    expect(primaryRole([Roles.user, Roles.admin])).toBe('Administrator')
    expect(primaryRole([Roles.technician, Roles.user])).toBe('Technician')
    expect(primaryRole([Roles.user])).toBe('User')
  })

  it('says so when an account holds no role at all', () => {
    expect(primaryRole([])).toBe('No role assigned')
  })
})

describe('initials', () => {
  it('takes the first and last name', () => {
    expect(initials('John Santos')).toBe('JS')
  })

  it('handles a single name', () => {
    expect(initials('Admin')).toBe('A')
  })

  it('never returns an empty label', () => {
    expect(initials('   ')).toBe('?')
  })
})
