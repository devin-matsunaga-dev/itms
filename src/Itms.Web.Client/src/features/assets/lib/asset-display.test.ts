import { describe, expect, it } from 'vitest'
import {
  assetTitle,
  isMappedStatus,
  isTerminalStatus,
  statusCodeOrder,
  statusTone,
} from './asset-display'

describe('statusTone', () => {
  it('gives every seeded status the hue DESIGN.md §2 names', () => {
    expect(statusTone('deployed').dot).toBe('bg-success')
    expect(statusTone('in-stock').dot).toBe('bg-info')
    expect(statusTone('repair').dot).toBe('bg-warning')
    expect(statusTone('retired').dot).toBe('bg-neutral-chart')
    expect(statusTone('lost').dot).toBe('bg-muted-foreground')
    expect(statusTone('disposed').dot).toBe('bg-muted-foreground')
  })

  it('has a colour for all six statuses SPEC.md §3 names', () => {
    for (const code of statusCodeOrder) {
      expect(isMappedStatus(code)).toBe(true)
    }
  })

  it('is keyed on the code, so renaming a status does not repaint it', () => {
    // WP-2.1 gave a status an immutable code precisely so this would hold: an
    // administrator renaming "In Stock" to "Warehouse" moves the word and not the blue.
    expect(statusTone('in-stock')).toEqual(statusTone('IN-STOCK'))
  })

  it('gives a status somebody added beyond the seeded six the unmapped treatment', () => {
    // `muted` is a hue nothing else claims, so a custom status reads as unmapped rather
    // than as somebody else's state.
    expect(isMappedStatus('on-loan')).toBe(false)
    expect(statusTone('on-loan').dot).toBe('bg-muted-foreground')
  })
})

describe('isTerminalStatus', () => {
  it('names the three the lifecycle has no way out of', () => {
    // WP-2.2, at the human's direction. This wording is used for a caption and never to
    // decide whether an action is legal — that stays server-side.
    expect(isTerminalStatus('retired')).toBe(true)
    expect(isTerminalStatus('lost')).toBe(true)
    expect(isTerminalStatus('disposed')).toBe(true)

    expect(isTerminalStatus('deployed')).toBe(false)
    expect(isTerminalStatus('in-stock')).toBe(false)
    expect(isTerminalStatus('repair')).toBe(false)
    // A code this does not know is not terminal, matching `AssetLifecycle.IsTerminal`.
    expect(isTerminalStatus('on-loan')).toBe(false)
  })
})

describe('assetTitle', () => {
  it('prefers the name somebody gave the machine', () => {
    expect(
      assetTitle({
        name: 'Jane’s laptop',
        manufacturer: 'Dell',
        model: 'Latitude 5430',
        assetTag: 'LAP-0042',
      }),
    ).toBe('Jane’s laptop')
  })

  it('falls back to make and model when there is no name', () => {
    expect(
      assetTitle({ name: null, manufacturer: 'Dell', model: 'Latitude 5430', assetTag: 'LAP-0042' }),
    ).toBe('Dell Latitude 5430')

    expect(assetTitle({ name: '  ', manufacturer: 'Dell', model: null, assetTag: 'LAP-0042' })).toBe(
      'Dell',
    )
  })

  it('falls back to the tag, which is never null', () => {
    // A row whose title is blank is a row nobody can click with any confidence, and the
    // tag is the one field that is unique, immutable, and always present (invariant 4).
    expect(
      assetTitle({ name: null, manufacturer: null, model: null, assetTag: 'LAP-0042' }),
    ).toBe('LAP-0042')

    expect(assetTitle({ assetTag: 'LAP-0042' })).toBe('LAP-0042')
  })
})
