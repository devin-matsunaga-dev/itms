/**
 * The asset form's shape, as zod (CONVENTIONS.md: react-hook-form + zod, the schema shared
 * with the display layer).
 *
 * ## One schema for both forms
 *
 * Recording an asset and correcting one ask for the same facts, so they share this. The two
 * differences are handled by the form rather than by a second schema: the **asset tag** is
 * required by both but is read-only on the edit, because invariant 4 makes it immutable
 * once created; and the **status** is offered only when recording, because booking in
 * equipment that is already deployed is stating a fact, while moving an existing asset
 * between statuses is a lifecycle transition that owes a history entry and a domain event.
 * `UpdateAssetRequest` carries neither field, so neither can reach the server from an edit.
 *
 * ## The bounds are the server's own
 *
 * `Asset.NameMaxLength` and its siblings, which `CreateAssetValidator` and
 * `UpdateAssetValidator` both enforce and the columns hold. Checking them here does not
 * make the server's check redundant: it means somebody typing one character too many is
 * told at the field rather than by a round trip, and everything the server refuses still
 * comes back mapped onto the field that caused it.
 *
 * Two things are deliberately not validated here, for the reason `CreateAssetValidator`'s
 * own remarks give. Whether a tag is already taken, or a serial already used by the same
 * manufacturer, is a question only the database can answer, and a client that tried would
 * still lose the race to the row it read — both come back as a 409 naming the collision.
 */

import { z } from 'zod'

/** `AssetTagRules.MaxLength`. */
export const assetTagMaxLength = 64

/** `Asset.NameMaxLength`. */
export const nameMaxLength = 128

/** `Asset.SerialNumberMaxLength`, and the same bound for the manufacturer and the model. */
export const serialNumberMaxLength = 128

/** `Asset.BarcodeMaxLength`. */
export const barcodeMaxLength = 64

/** `Asset.NotesMaxLength`. */
export const notesMaxLength = 4000

/** `AssetTagRules.Requirement`, word for word, so the field says what the server would. */
const tagRequirement = 'An asset tag cannot contain spaces.'

/**
 * What `UpdateAssetValidator` refuses: the column is `numeric(12,2)`, so anything larger
 * would be silently rounded or would fail in the database with a message nobody can act on.
 */
const costCeiling = 10_000_000_000

/** Digits with at most two decimal places, which is what the column stores. */
const money = /^\d+(\.\d{1,2})?$/

export const assetFormSchema = z.object({
  /**
   * Required and immutable. The edit form renders it read-only and still carries it, so
   * one schema describes both screens — and the value it carries is the asset's own, which
   * always passes.
   */
  assetTag: z
    .string()
    .trim()
    .min(1, 'Enter an asset tag.')
    .max(
      assetTagMaxLength,
      `An asset tag cannot be longer than ${String(assetTagMaxLength)} characters.`,
    )
    .refine((tag) => !/\s/.test(tag), tagRequirement),
  assetTypeId: z.string().min(1, 'Choose an asset type.'),
  /** Empty means "the seeded In Stock status", which is what the server defaults to. */
  assetStatusId: z.string(),
  name: z.string().trim().max(nameMaxLength, tooLong('A name', nameMaxLength)),
  serialNumber: z
    .string()
    .trim()
    .max(serialNumberMaxLength, tooLong('A serial number', serialNumberMaxLength)),
  barcode: z.string().trim().max(barcodeMaxLength, tooLong('A barcode', barcodeMaxLength)),
  manufacturer: z
    .string()
    .trim()
    .max(serialNumberMaxLength, tooLong('A manufacturer', serialNumberMaxLength)),
  model: z.string().trim().max(serialNumberMaxLength, tooLong('A model', serialNumberMaxLength)),
  /** Empty means no department. Both are real answers; neither is required. */
  departmentId: z.string(),
  locationId: z.string(),
  /**
   * `YYYY-MM-DD` from a date input, or empty. A warranty that expired before the purchase
   * is deliberately not refused — the server does not either, because second-hand equipment
   * bought with the remainder of somebody else's warranty is real.
   */
  purchaseDate: z.string(),
  warrantyExpiresAt: z.string(),
  vendor: z.string().trim().max(serialNumberMaxLength, tooLong('A vendor', serialNumberMaxLength)),
  /**
   * Held as text and parsed on submit. A number input would let the browser decide what
   * "1,499.5" means, and the column is `numeric(12,2)` in one currency — see `Asset.Cost`.
   */
  cost: z
    .string()
    .trim()
    .refine((value) => value === '' || money.test(value), 'Enter an amount, like 1499.50')
    .refine(
      (value) => value === '' || Number(value) < costCeiling,
      'That cost is larger than this system records.',
    ),
  notes: z.string().trim().max(notesMaxLength, tooLong('A note', notesMaxLength)),
})

export type AssetFormValues = z.infer<typeof assetFormSchema>

/** What the create form holds before anything is typed. */
export const emptyAsset: AssetFormValues = {
  assetTag: '',
  assetTypeId: '',
  assetStatusId: '',
  name: '',
  serialNumber: '',
  barcode: '',
  manufacturer: '',
  model: '',
  departmentId: '',
  locationId: '',
  purchaseDate: '',
  warrantyExpiresAt: '',
  vendor: '',
  cost: '',
  notes: '',
}

/** The fields the server can name in a validation failure, in the form's own words. */
export const assetFormFields = [
  'assetTag',
  'assetTypeId',
  'assetStatusId',
  'name',
  'serialNumber',
  'barcode',
  'manufacturer',
  'model',
  'departmentId',
  'locationId',
  'purchaseDate',
  'warrantyExpiresAt',
  'vendor',
  'cost',
  'notes',
] as const satisfies readonly (keyof AssetFormValues)[]

/**
 * An asset as the form holds it.
 *
 * Every nullable field becomes an empty string, because a controlled input cannot hold
 * null — and on the way back out {@link text} turns it into null again, so a field the
 * operator empties is cleared rather than silently kept.
 */
export function assetToForm(asset: {
  assetTag: string
  assetTypeId: string
  assetStatusId: string
  name?: string | null
  serialNumber?: string | null
  barcode?: string | null
  manufacturer?: string | null
  model?: string | null
  departmentId?: string | null
  locationId?: string | null
  purchaseDate?: string | null
  warrantyExpiresAt?: string | null
  vendor?: string | null
  cost?: number | null
  notes?: string | null
}): AssetFormValues {
  return {
    assetTag: asset.assetTag,
    assetTypeId: asset.assetTypeId,
    assetStatusId: asset.assetStatusId,
    name: asset.name ?? '',
    serialNumber: asset.serialNumber ?? '',
    barcode: asset.barcode ?? '',
    manufacturer: asset.manufacturer ?? '',
    model: asset.model ?? '',
    departmentId: asset.departmentId ?? '',
    locationId: asset.locationId ?? '',
    purchaseDate: asset.purchaseDate ?? '',
    warrantyExpiresAt: asset.warrantyExpiresAt ?? '',
    vendor: asset.vendor ?? '',
    // Not `toFixed(2)`: a cost of 1499.5 is not a cost of "1499.50" the operator typed,
    // and re-submitting an untouched form must not look like an edit in the audit trail.
    cost: asset.cost === null || asset.cost === undefined ? '' : String(asset.cost),
    notes: asset.notes ?? '',
  }
}

/** An optional text field on the way to the wire: empty is null, which clears the column. */
export function text(value: string): string | null {
  const trimmed = value.trim()
  return trimmed.length === 0 ? null : trimmed
}

/** The cost on the way to the wire. Validated by the schema, so this only converts. */
export function amount(value: string): number | null {
  const trimmed = value.trim()
  return trimmed.length === 0 ? null : Number(trimmed)
}

function tooLong(what: string, max: number): string {
  return `${what} cannot be longer than ${String(max)} characters.`
}
