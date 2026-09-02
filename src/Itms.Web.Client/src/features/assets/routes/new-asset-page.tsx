import { useCallback } from 'react'
import { Link, useNavigate } from 'react-router'
import { useForm, type SubmitHandler } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { toast } from 'sonner'
import { PageHeader } from '@/components/layout/page-header'
import { Button } from '@/components/ui/button'
import { ApiError } from '@/lib/api/client'
import type { CreateAssetRequest } from '@/lib/api/types'
import { useDepartments } from '@/features/directory/hooks/use-directory'
import { AssetForm } from '../components/asset-form'
import { useCreateAsset } from '../hooks/use-asset-write'
import { useAssetStatuses, useAssetTypes } from '../hooks/use-assets'
import {
  amount,
  assetFormFields,
  assetFormSchema,
  emptyAsset,
  text,
  type AssetFormValues,
} from '../lib/asset-form-schema'

/**
 * Recording an asset (WP-2.6b).
 *
 * A route rather than a dialog, following `new-ticket-page.tsx`: the address is linkable,
 * and DESIGN.md §4 puts a long form in section cards on a page. The button that reaches it
 * is the register's primary page action and its empty state, which are the only two places
 * §4 puts a create action.
 *
 * **The asset arrives holding nobody, whatever the form says**, because the form does not
 * ask. Invariant 5 requires an asset-history entry for an assignment, and history is
 * written by `Asset.AssignTo` inside a lifecycle call — so equipment is recorded first and
 * issued from its own screen, where the move is timelined. `CreateAssetRequest` has no
 * holder field, which is the same line `NewAsset` draws server-side.
 */
export function NewAssetPage(): React.JSX.Element {
  const navigate = useNavigate()

  const types = useAssetTypes()
  const statuses = useAssetStatuses()
  const departments = useDepartments()

  const create = useCreateAsset()

  const form = useForm<AssetFormValues>({
    resolver: zodResolver(assetFormSchema),
    defaultValues: emptyAsset,
  })

  const onSubmit = useCallback<SubmitHandler<AssetFormValues>>(
    (values) => {
      const request: CreateAssetRequest = {
        assetTag: values.assetTag,
        assetTypeId: values.assetTypeId,
        // Omitted means the seeded In Stock status, which is what the server falls back to.
        assetStatusId: values.assetStatusId === '' ? null : values.assetStatusId,
        name: text(values.name),
        serialNumber: text(values.serialNumber),
        barcode: text(values.barcode),
        manufacturer: text(values.manufacturer),
        model: text(values.model),
        departmentId: values.departmentId === '' ? null : values.departmentId,
        locationId: values.locationId === '' ? null : values.locationId,
        purchaseDate: text(values.purchaseDate),
        warrantyExpiresAt: text(values.warrantyExpiresAt),
        vendor: text(values.vendor),
        cost: amount(values.cost),
        notes: text(values.notes),
      }

      create.mutate(request, {
        onSuccess: (asset) => {
          toast.success(`${asset.assetTag} recorded.`)
          void navigate(`/assets/${asset.id}`)
        },
        onError: (error) => {
          // ProblemDetails carries per-field messages keyed by camel-cased field name
          // (WP-0.3), so a retired asset type lands on the type select. A duplicate tag is
          // a 409 with no field map — it goes to the tag, because that is the field the
          // person has to change.
          if (error instanceof ApiError && error.code === 'assets.duplicate_asset_tag') {
            form.setError('assetTag', { type: 'server', message: error.message })
            return
          }

          if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
            for (const field of assetFormFields) {
              const messages = error.fieldErrors[field]
              if (messages && messages.length > 0) {
                form.setError(field, { type: 'server', message: messages[0] })
              }
            }
            return
          }

          toast.error('The asset could not be recorded.', {
            description: error instanceof Error ? error.message : undefined,
          })
        },
      })
    },
    [create, form, navigate],
  )

  return (
    <>
      <PageHeader
        title="New asset"
        subtitle="Record a piece of equipment on the books. It is issued to somebody from its own page."
        back={backToAssets}
      />

      <form
        noValidate
        className="flex flex-col gap-5"
        onSubmit={(event) => {
          void form.handleSubmit(onSubmit)(event)
        }}
      >
        <AssetForm
          form={form}
          mode="create"
          types={types.data ?? []}
          statuses={statuses.data ?? []}
          departments={departments.data ?? []}
        />

        <div className="flex items-center justify-end gap-3">
          <Button variant="outline" type="button" render={<Link to="/assets" />}>
            Cancel
          </Button>
          <Button type="submit" disabled={create.isPending}>
            {create.isPending ? 'Recording…' : 'Record asset'}
          </Button>
        </div>
      </form>
    </>
  )
}

/** One wording for leaving an asset screen, shared with the register and the detail. */
const backToAssets = { to: '/assets', label: 'Back to assets' }
