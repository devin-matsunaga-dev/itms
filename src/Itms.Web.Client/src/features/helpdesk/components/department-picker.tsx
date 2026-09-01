import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
} from '@/components/ui/combobox'
import type { Department } from '@/lib/api/types'

interface DepartmentPickerProps {
  id: string
  departments: readonly Department[]
  /** The chosen department's id, or empty for "the requester's own". */
  value: string
  invalid: boolean
  onValueChange: (departmentId: string) => void
}

/**
 * The department field: type to narrow, or open it and pick.
 *
 * A combobox rather than the plain select every other picker on this form uses, because a
 * department list is the one that grows without bound — SPEC.md §5's hierarchy is an
 * estate, and a real deployment enters more of them than anyone scrolls. Eight seeded
 * departments do not need search; forty do, and this costs nothing to have early.
 *
 * **Empty is a real answer, not a missing one.** Leaving it blank files the ticket against
 * the requester's own department, which is what the server does when the field is absent
 * (WP-1.5) — so an end user is never asked a question their account already answers. That
 * is why there is no "required" mark and no empty-value validation.
 */
export function DepartmentPicker({
  id,
  departments,
  value,
  invalid,
  onValueChange,
}: DepartmentPickerProps): React.JSX.Element {
  const items = departments.map((department) => ({
    value: department.id,
    label: department.name,
  }))

  return (
    <Combobox
      items={items}
      value={items.find((item) => item.value === value) ?? null}
      onValueChange={(next: { value: string; label: string } | null) => {
        onValueChange(next?.value ?? '')
      }}
    >
      <ComboboxInput
        id={id}
        aria-invalid={invalid}
        showClear
        placeholder="Select or search department"
      />
      <ComboboxContent>
        <ComboboxEmpty>No department matches that.</ComboboxEmpty>
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
