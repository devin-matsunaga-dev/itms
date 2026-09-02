import { useEffect, useCallback } from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router'
import { useForm, type SubmitHandler } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { HardDrive } from 'lucide-react'
import { toast } from 'sonner'
import { PageHeader } from '@/components/layout/page-header'
import { Button } from '@/components/ui/button'
import { EmptyState } from '@/components/common/empty-state'
import { ErrorState } from '@/components/common/error-state'
import { ApiError } from '@/lib/api/client'
import type { UpdateAssetRequest } from '@/lib/api/types'
import { useDepartments, useLocations } from '@/features/directory/hooks/use-directory'
import { AssetForm } from '../components/asset-form'
import { AssetDetailSkeleton } from '../components/asset-detail-skeleton'
import { useUpdateAsset } from '../hooks/use-asset-write'
import { useAsset, useAssetStatuses, useAssetTypes } from '../hooks/use-assets'
import { assetTitle } from '../lib/asset-display'
import {
  amount,
  assetFormFields,
  assetFormSchema,
  assetToForm,
  emptyAsset,
  text,
  type AssetFormValues,
} from '../lib/asset-form-schema'

/**
 * Correcting an asset (WP-2.6b).
 *
 * ## Concurrency
 *
 * The `ETag` the asset was read at is sent as `If-Match`, so a form opened before somebody
 * else changed the asset is refused with **412 before the write is attempted** rather than
 * losing a race and overwriting them. That is the same handling the detail screen's
 * lifecycle actions get, and the same one `ticket-detail-page.tsx` settled at WP-1.10.
 *
 * ## What an edit cannot do
 *
 * It cannot retag the asset (invariant 4), and it cannot move the status or the holder —
 * those owe a history entry and a domain event, and belong to the lifecycle actions on the
 * detail screen. `UpdateAssetRequest` carries none of the three, so this is structural
 * rather than a rule the form remembers.
 *
 * An edit is audited as `assets.asset_updated` and writes **no timeline entry**, which is
 * what the package specifies. A correction is therefore visible in the audit trail and not
 * on the asset's own history — recorded in STATUS.md as a gap for a later decision.
 */
export function EditAssetPage(): React.JSX.Element {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const assetId = id ?? ''
  const detail = useAsset(assetId)

  const types = useAssetTypes()
  const statuses = useAssetStatuses()
  const departments = useDepartments()
  const locations = useLocations()

  const update = useUpdateAsset(assetId)

  const form = useForm<AssetFormValues>({
    resolver: zodResolver(assetFormSchema),
    defaultValues: emptyAsset,
  })

  const { reset } = form
  const asset = detail.data?.asset

  // Filled once the read lands, and again if the asset is refetched — a form whose defaults
  // were seeded from a stale copy would silently post the old values back.
  useEffect(() => {
    if (asset) {
      reset(assetToForm(asset))
    }
  }, [asset, reset])

  const etag = detail.data?.etag ?? null

  const onSubmit = useCallback<SubmitHandler<AssetFormValues>>(
    (values) => {
      // No asset tag and no status: a PUT that carried either would be a lifecycle move or
      // a retag wearing an edit's clothes, and `UpdateAssetRequest` has no field for one.
      const request: UpdateAssetRequest = {
        assetTypeId: values.assetTypeId,
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

      update.mutate(
        { request, etag },
        {
          onSuccess: (saved) => {
            toast.success(`${saved.assetTag} saved.`)
            void navigate(`/assets/${saved.id}`)
          },
          onError: (error) => {
            if (error instanceof ApiError && (error.status === 412 || error.status === 409)) {
              // Both mean the same thing to the person in front of it, and both are worth
              // distinguishing from an ordinary failure: somebody else got there first.
              if (error.code === 'assets.duplicate_serial_number') {
                form.setError('serialNumber', { type: 'server', message: error.message })
                return
              }

              toast.error('This asset changed while you were editing it.', {
                description: 'Reload the asset, check what happened, and make the change again.',
              })
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

            toast.error('The asset could not be saved.', {
              description: error instanceof Error ? error.message : undefined,
            })
          },
        },
      )
    },
    [etag, form, navigate, update],
  )

  if (id === undefined) {
    return <Navigate to="/assets" replace />
  }

  if (detail.isPending) {
    return (
      <>
        <PageHeader title="Edit asset" subtitle="Loading…" back={backToAssets} />
        <AssetDetailSkeleton />
      </>
    )
  }

  if (detail.isError) {
    const missing = detail.error instanceof ApiError && detail.error.status === 404

    return (
      <>
        <PageHeader title="Edit asset" subtitle="" back={backToAssets} />
        {missing ? (
          <EmptyState
            icon={HardDrive}
            title="No such asset"
            description="It may have been removed from the register."
          />
        ) : (
          <ErrorState
            title="The asset could not be loaded."
            description="The server did not answer. Nothing has been changed."
            onRetry={() => {
              void detail.refetch()
            }}
          />
        )}
      </>
    )
  }

  const loaded = detail.data.asset
  const backToAsset = { to: `/assets/${loaded.id}`, label: `Back to ${loaded.assetTag}` }

  return (
    <>
      <PageHeader
        title={`Edit ${assetTitle(loaded)}`}
        subtitle={`${loaded.assetTag} · ${loaded.assetTypeName}. The tag, the status, and who holds it are changed elsewhere.`}
        back={backToAsset}
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
          mode="edit"
          types={types.data ?? []}
          statuses={statuses.data ?? []}
          departments={departments.data ?? []}
          locations={locations.data ?? []}
        />

        <div className="flex items-center justify-end gap-3">
          <Button variant="outline" type="button" render={<Link to={backToAsset.to} />}>
            Cancel
          </Button>
          <Button type="submit" disabled={update.isPending}>
            {update.isPending ? 'Saving…' : 'Save changes'}
          </Button>
        </div>
      </form>
    </>
  )
}

const backToAssets = { to: '/assets', label: 'Back to assets' }
