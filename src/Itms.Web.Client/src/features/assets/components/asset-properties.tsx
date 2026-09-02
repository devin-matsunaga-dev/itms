import { ClipboardList } from 'lucide-react'
import { Panel } from '@/components/common/panel'
import type { Asset } from '@/lib/api/types'
import { formatDate, formatDateTime } from '@/lib/datetime'
import { parseDateOnly } from '../lib/warranty'

interface AssetPropertiesProps {
  asset: Asset
}

/**
 * Everything about one asset that the register deliberately does not carry.
 *
 * WP-2.3 kept cost, notes, barcode, vendor, and the purchase date off
 * `AssetListItemResponse` — nothing scans a list by them, notes is a four-thousand
 * character column, and an inventory list is the thing somebody screenshots. At the
 * human's direction that stands for `WP-2.6a` too: they are **here**, on a screen where
 * one asset is being read on purpose, and nowhere else.
 *
 * Every instant is rendered through the shared formatter in the viewer's own timezone
 * (DESIGN.md §6). The purchase and warranty dates are `DateOnly` on the wire — calendar
 * facts with no zone — so they go through `parseDateOnly` and are never handed to
 * `new Date()`, which would read them as UTC midnight and show the previous day to
 * everybody west of Greenwich.
 */
export function AssetProperties({ asset }: AssetPropertiesProps): React.JSX.Element {
  return (
    <Panel icon={ClipboardList} title="Details">
      <dl className="flex flex-col gap-4">
        <Section>Identification</Section>
        <Row term="Asset tag">{asset.assetTag}</Row>
        <Row term="Serial number">{asset.serialNumber ?? '—'}</Row>
        <Row term="Barcode">{asset.barcode ?? '—'}</Row>
        <Row term="Manufacturer">{asset.manufacturer ?? '—'}</Row>
        <Row term="Model">{asset.model ?? '—'}</Row>

        <hr className="border-border" />

        <Section>Ownership</Section>
        <Row term="Department">{asset.departmentName ?? '—'}</Row>
        <Row term="Location">{asset.locationPath ?? '—'}</Row>

        <hr className="border-border" />

        <Section>Purchase</Section>
        <Row term="Purchased">{formatDateOnly(asset.purchaseDate)}</Row>
        <Row term="Warranty expires">{formatDateOnly(asset.warrantyExpiresAt)}</Row>
        <Row term="Vendor">{asset.vendor ?? '—'}</Row>
        {/*
          No currency symbol, deliberately. `AssetResponse.Cost` is "in the deployment's
          own currency — there is only one", and there is no field anywhere in the system
          that names which. Grouping and two decimals are what the number needs to be read;
          inventing a symbol would be asserting something the record does not say.
        */}
        <Row term="Cost">{formatCost(asset.cost)}</Row>

        <hr className="border-border" />

        <Row term="Recorded">{formatDateTime(asset.createdAt)}</Row>
        <Row term="Last changed">{formatDateTime(asset.updatedAt)}</Row>
      </dl>
    </Panel>
  )
}

/** DESIGN.md §4: a section label in `primary`, so a long card reads as its parts. */
function Section({ children }: { children: React.ReactNode }): React.JSX.Element {
  return (
    <p className="text-label font-semibold tracking-[0.06em] text-primary uppercase">{children}</p>
  )
}

function Row({ term, children }: { term: string; children: React.ReactNode }): React.JSX.Element {
  return (
    <div className="flex flex-col gap-1">
      <dt className="text-label font-semibold text-muted-foreground uppercase">{term}</dt>
      <dd className="tabular text-cell break-words text-heading">{children}</dd>
    </div>
  )
}

function formatDateOnly(value: string | null | undefined): string {
  const date = parseDateOnly(value)
  return date === null ? '—' : formatDate(date)
}

function formatCost(cost: number | null | undefined): string {
  if (cost === null || cost === undefined) {
    return '—'
  }

  return new Intl.NumberFormat(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(cost)
}
