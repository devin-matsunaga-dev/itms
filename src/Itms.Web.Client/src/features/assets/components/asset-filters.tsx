import { SlidersHorizontal } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
} from '@/components/ui/combobox'
import { Label } from '@/components/ui/label'
import { Popover, PopoverContent, PopoverTitle, PopoverTrigger } from '@/components/ui/popover'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type {
  AssetStatus,
  AssetType,
  Department,
  Location,
  UserSummary,
} from '@/lib/api/types'
import {
  activeWarrantyOption,
  advancedFilterCount,
  hasActiveFilters,
  warrantyOptions,
  type AssetQuery,
} from '../lib/asset-query'

/** The value a "no filter on this" option carries. Empty string is a legitimate id. */
const any = '__any__'

/** The holder select's "nobody holds it" option, which is a filter rather than an absence. */
const nobody = '__unassigned__'

interface AssetFiltersProps {
  query: AssetQuery
  types: readonly AssetType[]
  statuses: readonly AssetStatus[]
  departments: readonly Department[]
  locations: readonly Location[]
  holders: readonly UserSummary[]
  onChange: (changes: Partial<AssetQuery>) => void
  onClear: () => void
  /** The search box, rendered inside this card — they are one job, not two. */
  search: React.ReactNode
}

/**
 * The register's filter bar: the three a technician reaches for constantly, and the rest
 * behind a popover whose badge counts them (DESIGN.md §4, *Filter bar*).
 *
 * Inline are **type**, **status**, and **warranty** — what kind of thing, where it is in
 * its life, and whether it still has cover. Behind the button are department, location,
 * and who holds it: real questions, asked less often, and each one a picker rather than a
 * two-word select.
 *
 * Every control writes straight through to the URL. There is no draft state and no "apply"
 * button, inside the popover or outside it, so what the address says and what the table
 * shows cannot come apart.
 *
 * **The status filter offers names and writes codes.** An administrator may rename a
 * status; the code is immutable (WP-2.1), and a link written against a code is the same
 * link in every deployment. `asset-query.ts` argues it at length.
 */
export function AssetFilters({
  query,
  types,
  statuses,
  departments,
  locations,
  holders,
  onChange,
  onClear,
  search,
}: AssetFiltersProps): React.JSX.Element {
  const advanced = advancedFilterCount(query)
  const warranty = activeWarrantyOption(query)
  const holderValue = query.unassigned ? nobody : (query.assignedToUserId ?? any)
  const statusLabels = new Map(statuses.map((status) => [status.code, status.name]))

  return (
    <div className="flex flex-col gap-3 rounded-card border border-border bg-surface p-4 shadow-card">
      {/*
        The search and the filters are one card because they are one job — narrowing the
        register. The queue made the same call at WP-1.12.
      */}
      {search}

      <div className="flex flex-wrap items-center gap-3">
        <InlineSelect
          id="filter-asset-type"
          label="Type"
          value={query.assetTypeId ?? any}
          onValueChange={(value) => {
            onChange({ assetTypeId: value === any ? null : value })
          }}
          options={types.map((type) => ({ value: type.id, label: type.name }))}
        />

        <Select
          multiple
          items={statuses.map((status) => ({ label: status.name, value: status.code }))}
          value={[...query.statusCode]}
          onValueChange={(value: string[]) => {
            onChange({ statusCode: value })
          }}
        >
          <SelectTrigger id="filter-asset-status" size="default" className="w-40" aria-label="Status">
            <SelectValue>
              {(value: string[]) =>
                value.length === 0
                  ? 'Status'
                  : value.length === 1
                    ? // A code in the address that no status carries — one an administrator
                      // retired, or a typo — is a filter matching nothing (WP-2.3), so the
                      // trigger shows the code itself rather than a blank.
                      (statusLabels.get(value[0] ?? '') ?? value[0])
                    : `${String(value.length)} statuses`
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {statuses.map((status) => (
              <SelectItem key={status.code} value={status.code}>
                {status.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select
          items={warrantyOptions.map((option) => ({ label: option.label, value: option.value }))}
          value={warranty?.value ?? null}
          onValueChange={(value: string | null) => {
            const chosen = warrantyOptions.find((option) => option.value === value)
            if (chosen) {
              onChange({
                warrantyExpiringInDays: chosen.expiringInDays,
                warrantyExpired: chosen.expired,
              })
            }
          }}
        >
          <SelectTrigger id="filter-warranty" size="default" className="w-56" aria-label="Warranty">
            {/*
              A hand-written `?warrantyExpiringInDays=45` is a filter the server honours
              and no option names. The placeholder says so rather than the trigger reading
              "Any warranty" over a list that is filtered — the call the queue's sort
              select makes for an ordering its options do not cover.
            */}
            <SelectValue placeholder="Custom window">
              {(value: string) =>
                value === 'any'
                  ? 'Warranty'
                  : (warrantyOptions.find((option) => option.value === value)?.label ?? 'Warranty')
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {warrantyOptions.map((option) => (
              <SelectItem key={option.value} value={option.value}>
                {option.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Popover>
          <PopoverTrigger
            render={<Button variant="outline" className={cn(advanced > 0 && 'border-primary')} />}
          >
            <SlidersHorizontal className="size-4" aria-hidden="true" />
            Filters
            {advanced > 0 ? (
              <span className="ml-1 inline-flex size-5 items-center justify-center rounded-full bg-primary text-label font-semibold text-white tabular">
                {advanced}
              </span>
            ) : null}
          </PopoverTrigger>

          <PopoverContent align="start" className="w-80">
            <PopoverTitle>More filters</PopoverTitle>

            <Field label="Department" htmlFor="filter-asset-department">
              <PanelSelect
                id="filter-asset-department"
                value={query.departmentId ?? any}
                onValueChange={(value) => {
                  onChange({ departmentId: value === any ? null : value })
                }}
                options={departments.map((department) => ({
                  value: department.id,
                  label: department.name,
                }))}
                anyLabel="Any department"
              />
            </Field>

            <Field label="Location" htmlFor="filter-asset-location">
              {/*
                A combobox rather than the selects beside it, because a location list is
                the one that grows without bound — an estate is sites, buildings, floors,
                and rooms, and nobody scrolls that. It matches one room exactly and does
                not descend the tree: WP-2.3 kept the subtree filter out of Assets because
                it would mean this module reasoning about Directory's path format, and
                WP-2.4 recorded that a subtree count is `WP-2.7`'s question.
              */}
              <LocationPicker
                id="filter-asset-location"
                locations={locations}
                value={query.locationId}
                onValueChange={(locationId) => {
                  onChange({ locationId })
                }}
              />
            </Field>

            <Field label="Assigned to" htmlFor="filter-asset-holder">
              <PanelSelect
                id="filter-asset-holder"
                value={holderValue}
                onValueChange={(value) => {
                  // "No filter on the holder" and "only the ones nobody holds" are
                  // different questions, and one null cannot mean both (WP-2.3).
                  if (value === nobody) {
                    onChange({ assignedToUserId: null, unassigned: true })
                    return
                  }
                  onChange({
                    assignedToUserId: value === any ? null : value,
                    unassigned: false,
                  })
                }}
                options={[
                  { value: nobody, label: 'Unassigned' },
                  // Everybody, not only the queue: equipment is issued to anybody in the
                  // organisation, which is why `AssignAssetHandler` asks Identity for no
                  // role. Filtering to technicians would hide most of the estate.
                  ...holders.map((user) => ({ value: user.id, label: user.displayName })),
                ]}
                anyLabel="Anybody"
              />
            </Field>
          </PopoverContent>
        </Popover>

        {hasActiveFilters(query) ? (
          <Button variant="link" className="ml-auto px-0" onClick={onClear}>
            Clear all
          </Button>
        ) : null}
      </div>
    </div>
  )
}

function Field({
  label,
  htmlFor,
  children,
}: {
  label: string
  htmlFor: string
  children: React.ReactNode
}): React.JSX.Element {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={htmlFor} className="text-field-label font-medium text-heading">
        {label}
      </Label>
      {children}
    </div>
  )
}

interface SelectProps {
  id: string
  value: string
  onValueChange: (value: string) => void
  options: readonly { value: string; label: string }[]
}

/**
 * One of the filters that stay on the bar.
 *
 * The trigger reads as the filter's own name until something is chosen, so an untouched
 * bar says "Type · Status · Warranty" rather than three variations on "Any".
 */
function InlineSelect({
  id,
  label,
  value,
  onValueChange,
  options,
}: SelectProps & { label: string }): React.JSX.Element {
  const all = [{ value: any, label: `Any ${label.toLowerCase()}` }, ...options]

  return (
    <Select
      items={all}
      value={value}
      onValueChange={(next: string | null) => {
        onValueChange(next ?? any)
      }}
    >
      <SelectTrigger id={id} size="default" className="w-40" aria-label={label}>
        <SelectValue>
          {(current: string) =>
            current === any
              ? label
              : (all.find((option) => option.value === current)?.label ?? label)
          }
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        {all.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

/** One filter inside the popover, where a label sits above it and the width is fixed. */
function PanelSelect({
  id,
  value,
  onValueChange,
  options,
  anyLabel,
}: SelectProps & { anyLabel: string }): React.JSX.Element {
  const all = [{ value: any, label: anyLabel }, ...options]

  return (
    <Select
      items={all}
      value={value}
      onValueChange={(next: string | null) => {
        onValueChange(next ?? any)
      }}
    >
      <SelectTrigger id={id} size="default" className="w-full">
        <SelectValue placeholder={anyLabel} />
      </SelectTrigger>
      <SelectContent>
        {all.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

interface LocationPickerProps {
  id: string
  locations: readonly Location[]
  /** The chosen location's id, or null for no filter. */
  value: string | null
  onValueChange: (locationId: string | null) => void
}

/**
 * The location field: type to narrow, or open it and pick.
 *
 * The label is the room's full `path`, not its name — three buildings can each have a
 * "Server Room", and a picker offering that word three times is one nobody can use. The
 * path is also what the register's own Location column renders, so the filter and the rows
 * read the same way.
 *
 * Clearing it is a real answer: the clear button writes null, which is "no filter on the
 * location" and is what removes the parameter from the address.
 */
function LocationPicker({
  id,
  locations,
  value,
  onValueChange,
}: LocationPickerProps): React.JSX.Element {
  const items = locations.map((location) => ({ value: location.id, label: location.path }))

  return (
    <Combobox
      items={items}
      value={items.find((item) => item.value === value) ?? null}
      onValueChange={(next: { value: string; label: string } | null) => {
        onValueChange(next?.value ?? null)
      }}
    >
      <ComboboxInput id={id} showClear placeholder="Any location" />
      <ComboboxContent>
        <ComboboxEmpty>No location matches that.</ComboboxEmpty>
        <ComboboxList>
          {(item: { value: string; label: string }) => (
            <ComboboxItem key={item.value} value={item}>
              {item.label}
            </ComboboxItem>
          )}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  )
}
