import { describe, expect, it } from 'vitest'
import type { Department, Location } from '@/lib/api/types'
import { departmentName, locationPath, roleLabel } from './user-display'

const department: Department = {
  id: 'dep-it',
  name: 'Information Technology',
  code: 'IT',
  description: null,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

const room: Location = {
  id: 'loc-server',
  name: 'Server Room',
  kind: 'Room',
  parentId: 'loc-floor',
  path: 'CUC → Saipan Plant → Admin Building → Ground Floor → Server Room',
  depth: 4,
  description: null,
  childCount: 0,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

describe('departmentName', () => {
  it('names the department', () => {
    expect(departmentName('dep-it', [department])).toEqual({
      text: 'Information Technology',
      known: true,
    })
  })

  it('reads a person with no department as having none, not as unknown', () => {
    expect(departmentName(null, [department])).toEqual({ text: '—', known: true })
  })

  it('says "not listed" rather than "none" for an id the lookup did not contain', () => {
    // The distinction this exists for: an em dash would claim the person has no
    // department, which is a different and false statement.
    expect(departmentName('dep-missing', [department])).toEqual({
      text: 'Not listed',
      known: false,
    })
  })
})

describe('locationPath', () => {
  it('renders the full path rather than the room name', () => {
    // Three buildings can each have a "Server Room"; the name alone does not say which.
    expect(locationPath('loc-server', [room]).text).toContain('Admin Building')
  })

  it('says "not listed" for a room past the flat read\'s two hundred', () => {
    expect(locationPath('loc-elsewhere', [room])).toEqual({ text: 'Not listed', known: false })
  })

  it('reads a person with no location as having none', () => {
    expect(locationPath(undefined, [room])).toEqual({ text: '—', known: true })
  })
})

describe('roleLabel', () => {
  it('lists every role the account holds rather than only the highest', () => {
    expect(roleLabel(['Admin', 'Technician'])).toBe('Admin, Technician')
  })

  it('says so when an account holds none', () => {
    expect(roleLabel([])).toBe('No role assigned')
  })
})
