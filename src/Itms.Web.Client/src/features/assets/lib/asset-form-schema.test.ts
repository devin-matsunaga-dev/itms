import { describe, expect, it } from 'vitest'
import {
  amount,
  assetFormSchema,
  assetToForm,
  emptyAsset,
  text,
} from '@/features/assets/lib/asset-form-schema'
import { asset } from '@/features/assets/test/asset-fixtures'

/**
 * The rules the form applies before a round trip, and the two conversions that decide what
 * "the operator emptied this box" means on the wire.
 */

function parse(overrides: Partial<typeof emptyAsset> = {}) {
  return assetFormSchema.safeParse({ ...emptyAsset, ...overrides })
}

function messageFor(result: ReturnType<typeof parse>, field: string): string | undefined {
  return result.success
    ? undefined
    : result.error.issues.find((issue) => issue.path[0] === field)?.message
}

describe('assetFormSchema', () => {
  it('needs a tag and a type, and nothing else', () => {
    expect(parse().success).toBe(false)
    expect(parse({ assetTag: 'LAP-0042', assetTypeId: 'type-laptop' }).success).toBe(true)
  })

  /**
   * `AssetTagRules` refuses whitespace inside a tag — it is what turns one tag into two
   * when it is scanned, pasted, or put in a URL — and the field says so in the server's own
   * words rather than waiting for a round trip.
   */
  it('refuses a tag containing whitespace, in the server’s wording', () => {
    expect(messageFor(parse({ assetTag: 'LAP 0042' }), 'assetTag')).toBe(
      'An asset tag cannot contain spaces.',
    )
  })

  it('bounds the fields at the lengths the columns hold', () => {
    expect(messageFor(parse({ assetTag: 'A'.repeat(65) }), 'assetTag')).toContain('64')
    expect(messageFor(parse({ name: 'A'.repeat(129) }), 'name')).toContain('128')
    expect(messageFor(parse({ barcode: 'A'.repeat(65) }), 'barcode')).toContain('64')
    expect(messageFor(parse({ notes: 'A'.repeat(4001) }), 'notes')).toContain('4000')
  })

  describe('cost', () => {
    it.each(['1499.50', '0', '1499', '1499.5'])('accepts %s', (value) => {
      expect(parse({ assetTag: 'LAP-0042', assetTypeId: 't', cost: value }).success).toBe(true)
    })

    // A negative price is not a discount, it is a typo — the server says so too. Three
    // decimals would be silently rounded by a `numeric(12,2)` column.
    it.each(['-1', 'about a thousand', '1499.505', '1,499.50'])('refuses %s', (value) => {
      expect(messageFor(parse({ cost: value }), 'cost')).toBe('Enter an amount, like 1499.50')
    })

    it('refuses a cost larger than the column records', () => {
      expect(messageFor(parse({ cost: '10000000000' }), 'cost')).toBe(
        'That cost is larger than this system records.',
      )
    })
  })
})

describe('assetToForm', () => {
  it('turns every null into the empty string a controlled input can hold', () => {
    const values = assetToForm(
      asset({
        name: null,
        serialNumber: null,
        barcode: null,
        manufacturer: null,
        model: null,
        departmentId: null,
        locationId: null,
        purchaseDate: null,
        warrantyExpiresAt: null,
        vendor: null,
        cost: null,
        notes: null,
      }),
    )

    expect(values).toMatchObject({
      assetTag: 'LAP-0042',
      assetTypeId: 'type-laptop',
      name: '',
      serialNumber: '',
      departmentId: '',
      cost: '',
      notes: '',
    })
  })

  /**
   * Not `toFixed(2)`. A cost of 1499.5 is not a cost the operator typed as "1499.50", and
   * re-submitting an untouched form must not look like an edit in the audit trail — the
   * server compares values, and 1499.5 and 1499.50 parse to the same number but only one of
   * them round-trips unchanged through the field.
   */
  it('renders a cost as the number it is, without adding decimals', () => {
    expect(assetToForm(asset({ cost: 1499.5 })).cost).toBe('1499.5')
    expect(assetToForm(asset({ cost: 1200 })).cost).toBe('1200')
  })

  it('round-trips an asset through the form and back to the wire unchanged', () => {
    const subject = asset()
    const values = assetToForm(subject)

    expect(assetFormSchema.safeParse(values).success).toBe(true)
    expect(text(values.name)).toBe(subject.name)
    expect(text(values.vendor)).toBe(subject.vendor)
    expect(amount(values.cost)).toBe(subject.cost)
  })
})

describe('text and amount', () => {
  it.each(['', '   '])('turns %o into null, so an emptied field is cleared', (value) => {
    expect(text(value)).toBeNull()
    expect(amount(value)).toBeNull()
  })

  it('trims what it keeps', () => {
    expect(text('  Reception desktop  ')).toBe('Reception desktop')
  })

  it('converts a cost to a number', () => {
    expect(amount('1499.50')).toBe(1499.5)
    expect(amount('0')).toBe(0)
  })
})
