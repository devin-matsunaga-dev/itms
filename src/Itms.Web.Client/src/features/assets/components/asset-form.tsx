import { Controller, type UseFormReturn } from 'react-hook-form'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { Field, FormSection } from '@/components/common/form-section'
import type { AssetStatus, AssetType, Department } from '@/lib/api/types'
import { DepartmentPicker } from '@/features/helpdesk/components/department-picker'
import { LocationPicker } from '@/features/directory/components/location-picker'
import {
  assetTagMaxLength,
  barcodeMaxLength,
  nameMaxLength,
  notesMaxLength,
  serialNumberMaxLength,
  type AssetFormValues,
} from '../lib/asset-form-schema'

interface AssetFormProps {
  form: UseFormReturn<AssetFormValues>
  /**
   * Recording a new asset, or correcting one that exists.
   *
   * The two differ in exactly two fields, and both differences are invariants rather than
   * preferences — see the schema. Everything else is identical, which is why there is one
   * form and not two.
   */
  mode: 'create' | 'edit'
  types: readonly AssetType[]
  statuses: readonly AssetStatus[]
  departments: readonly Department[]
}

/**
 * The fields an asset is recorded or corrected from (WP-2.6b).
 *
 * Three section cards, per DESIGN.md §4 — long forms use section cards, not accordions —
 * each opening with its uppercase `primary` label, which is what makes them read as three
 * parts of one form rather than three unrelated panels.
 *
 * ## The two fields that differ between the modes
 *
 * **The asset tag is read-only on the edit, with the reason in its tooltip**, rather than
 * hidden. Invariant 4 makes it immutable once created, and DESIGN.md §4 says a field that
 * is fixed for the person reading it is shown read-only with the reason given — a form that
 * quietly has different fields in different places is harder to trust than one that says
 * why. It is also not the enforcement: `UpdateAssetRequest` has no tag field at all, so a
 * tag sent by a hand-crafted request never reaches the entity.
 *
 * **The status is offered only when recording.** Booking in equipment that is already
 * deployed or already away for repair is recording a fact; moving an existing asset between
 * statuses is a lifecycle transition, which owes a history entry (invariant 5) and a domain
 * event. Those live on the detail screen's lifecycle actions, and an edit that could set the
 * column would route round both.
 */
export function AssetForm({
  form,
  mode,
  types,
  statuses,
  departments,
}: AssetFormProps): React.JSX.Element {
  const errors = form.formState.errors
  const editing = mode === 'edit'

  return (
    <>
      <FormSection title="Identification">
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <Field
            label="Asset tag"
            htmlFor="asset-tag"
            required
            error={errors.assetTag?.message}
            hint={
              editing
                ? 'An asset tag is the identifier on the physical label, and cannot be changed once the asset exists.'
                : 'The identifier on the physical label. It must be unique, and it cannot be changed afterwards.'
            }
          >
            <Input
              id="asset-tag"
              readOnly={editing}
              maxLength={assetTagMaxLength}
              placeholder="LAP-0042"
              className={editing ? 'bg-canvas' : undefined}
              aria-invalid={errors.assetTag !== undefined}
              {...form.register('assetTag')}
            />
          </Field>

          <Field label="Asset type" htmlFor="asset-type" required error={errors.assetTypeId?.message}>
            <Controller
              control={form.control}
              name="assetTypeId"
              render={({ field }) => (
                <FormSelect
                  id="asset-type"
                  placeholder="Choose an asset type"
                  value={field.value}
                  invalid={errors.assetTypeId !== undefined}
                  options={types.map((type) => ({ value: type.id, label: type.name }))}
                  onValueChange={field.onChange}
                />
              )}
            />
          </Field>

          <Field
            label="Name"
            htmlFor="asset-name"
            error={errors.name?.message}
            hint="What people call this machine. Left blank, the register falls back to the make and model, and then to the tag."
          >
            <Input
              id="asset-name"
              maxLength={nameMaxLength}
              placeholder="Reception desktop"
              aria-invalid={errors.name !== undefined}
              {...form.register('name')}
            />
          </Field>

          {editing ? null : (
            <Field
              label="Status"
              htmlFor="asset-status"
              error={errors.assetStatusId?.message}
              hint="Where the equipment already is. Leave it blank for In Stock, which is where new equipment starts."
            >
              <Controller
                control={form.control}
                name="assetStatusId"
                render={({ field }) => (
                  <FormSelect
                    id="asset-status"
                    placeholder="In Stock"
                    value={field.value}
                    invalid={errors.assetStatusId !== undefined}
                    options={statuses.map((status) => ({ value: status.id, label: status.name }))}
                    onValueChange={field.onChange}
                  />
                )}
              />
            </Field>
          )}
        </div>
      </FormSection>

      <FormSection title="Make and model">
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <Field label="Manufacturer" htmlFor="asset-manufacturer" error={errors.manufacturer?.message}>
            <Input
              id="asset-manufacturer"
              maxLength={serialNumberMaxLength}
              placeholder="Dell"
              aria-invalid={errors.manufacturer !== undefined}
              {...form.register('manufacturer')}
            />
          </Field>

          <Field label="Model" htmlFor="asset-model" error={errors.model?.message}>
            <Input
              id="asset-model"
              maxLength={serialNumberMaxLength}
              placeholder="Latitude 5430"
              aria-invalid={errors.model !== undefined}
              {...form.register('model')}
            />
          </Field>

          <Field
            label="Serial number"
            htmlFor="asset-serial"
            error={errors.serialNumber?.message}
            hint="Unique per manufacturer. Two vendors numbering their products from 1 is ordinary, so only a repeat from the same manufacturer is refused."
          >
            <Input
              id="asset-serial"
              maxLength={serialNumberMaxLength}
              placeholder="CND1234XYZ"
              aria-invalid={errors.serialNumber !== undefined}
              {...form.register('serialNumber')}
            />
          </Field>

          <Field label="Barcode" htmlFor="asset-barcode" error={errors.barcode?.message}>
            <Input
              id="asset-barcode"
              maxLength={barcodeMaxLength}
              placeholder="BC-4410"
              aria-invalid={errors.barcode !== undefined}
              {...form.register('barcode')}
            />
          </Field>
        </div>
      </FormSection>

      <FormSection title="Placement and purchase">
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <Field label="Department" htmlFor="asset-department" error={errors.departmentId?.message}>
            <Controller
              control={form.control}
              name="departmentId"
              render={({ field }) => (
                <DepartmentPicker
                  id="asset-department"
                  departments={departments}
                  value={field.value}
                  invalid={errors.departmentId !== undefined}
                  onValueChange={field.onChange}
                />
              )}
            />
          </Field>

          <Field label="Location" htmlFor="asset-location" error={errors.locationId?.message}>
            <Controller
              control={form.control}
              name="locationId"
              render={({ field }) => (
                <LocationPicker
                  id="asset-location"
                  value={field.value === '' ? null : field.value}
                  placeholder="Select or search a room"
                  invalid={errors.locationId !== undefined}
                  onValueChange={(locationId) => {
                    field.onChange(locationId ?? '')
                  }}
                />
              )}
            />
          </Field>

          <Field label="Purchase date" htmlFor="asset-purchased" error={errors.purchaseDate?.message}>
            <Input
              id="asset-purchased"
              type="date"
              aria-invalid={errors.purchaseDate !== undefined}
              {...form.register('purchaseDate')}
            />
          </Field>

          <Field
            label="Warranty expires"
            htmlFor="asset-warranty"
            error={errors.warrantyExpiresAt?.message}
            hint="Second-hand equipment bought with the remainder of somebody else’s warranty is real, so a warranty that ends before the purchase date is accepted."
          >
            <Input
              id="asset-warranty"
              type="date"
              aria-invalid={errors.warrantyExpiresAt !== undefined}
              {...form.register('warrantyExpiresAt')}
            />
          </Field>

          <Field label="Vendor" htmlFor="asset-vendor" error={errors.vendor?.message}>
            <Input
              id="asset-vendor"
              maxLength={serialNumberMaxLength}
              placeholder="Island Computing"
              aria-invalid={errors.vendor !== undefined}
              {...form.register('vendor')}
            />
          </Field>

          <Field
            label="Cost"
            htmlFor="asset-cost"
            error={errors.cost?.message}
            hint="In this deployment’s own currency — there is only one, so no currency is recorded beside the figure."
          >
            <Input
              id="asset-cost"
              inputMode="decimal"
              placeholder="1499.50"
              aria-invalid={errors.cost !== undefined}
              {...form.register('cost')}
            />
          </Field>
        </div>

        <Field label="Notes" htmlFor="asset-notes" error={errors.notes?.message}>
          <Textarea
            id="asset-notes"
            rows={4}
            maxLength={notesMaxLength}
            placeholder="Anything else worth recording — what was issued with it, where the charger went."
            aria-invalid={errors.notes !== undefined}
            {...form.register('notes')}
          />
        </Field>
      </FormSection>
    </>
  )
}

interface FormSelectProps {
  id: string
  placeholder: string
  value: string
  invalid: boolean
  options: readonly { value: string; label: string }[]
  onValueChange: (value: string) => void
}

function FormSelect({
  id,
  placeholder,
  value,
  invalid,
  options,
  onValueChange,
}: FormSelectProps): React.JSX.Element {
  return (
    <Select
      items={options}
      value={value === '' ? null : value}
      onValueChange={(next: string | null) => {
        onValueChange(next ?? '')
      }}
    >
      <SelectTrigger id={id} size="default" className="w-full" aria-invalid={invalid}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
