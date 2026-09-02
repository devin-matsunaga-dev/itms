import { apiFetch, apiRequest } from '@/lib/api/client'
import type {
  Asset,
  AssetStatus,
  AssetType,
  CreateAssetRequest,
  PagedAssetHistory,
  PagedAssets,
  PagedTicketSummaries,
  UpdateAssetRequest,
  UserSummary,
} from '@/lib/api/types'
import { serializeAssetQuery, type AssetQuery } from '../lib/asset-query'

/** The asset endpoints the register and the detail screen read (WP-2.1 through WP-2.5). */

/**
 * A page of the asset register.
 *
 * The query is serialized by the same function that writes the address bar, so what the
 * screen asks the server for and what the URL claims it is showing cannot drift.
 */
export function fetchAssets(query: AssetQuery, signal?: AbortSignal): Promise<PagedAssets> {
  const params = serializeAssetQuery(query)
  // The URL omits page 1 to keep a first-page link tidy; the API is told explicitly.
  params.set('page', String(query.page))

  return apiFetch<PagedAssets>(`/assets?${params.toString()}`, signal ? { signal } : {})
}

/** One asset, with the version tag every write on its screen sends back as `If-Match`. */
export interface AssetRead {
  readonly asset: Asset
  /** The response's `ETag`, or null if it carried none. Opaque — never parsed. */
  readonly etag: string | null
}

/**
 * One asset in full.
 *
 * The `ETag` is kept because every write on the detail screen sends it back as `If-Match`:
 * ARCHITECTURE.md §6 asks for optimistic concurrency on assets as well as tickets, and
 * WP-2.1 and WP-2.2 built the header surface for exactly this. A caller holding a stale
 * copy is refused with 412 **before** the change is attempted, instead of losing a race and
 * finding out through a 409. WP-2.6a dropped the tag deliberately, because a read-only
 * screen has no precondition to state; WP-2.6b is where that stopped being true.
 */
export async function fetchAsset(id: string, signal?: AbortSignal): Promise<AssetRead> {
  const result = await apiRequest<Asset>(`/assets/${id}`, signal ? { signal } : {})
  return { asset: result.data, etag: result.etag }
}

/**
 * Records a new asset.
 *
 * The reply is the asset itself, so the screen can go straight to its detail page. No
 * `ETag` is carried through: the detail route reads the asset again and picks up the tag
 * its own writes will need, which is the call `useCreateTicket` already made.
 */
export function createAsset(request: CreateAssetRequest): Promise<Asset> {
  return apiFetch<Asset>('/assets', { method: 'POST', body: request })
}

/**
 * Corrects an asset.
 *
 * A full replacement of the descriptive half: the form posts every field it holds, and a
 * field sent as null is cleared. The tag, the status and the holder are not in the shape at
 * all — the tag is immutable and the other two move through the lifecycle calls below.
 */
export function updateAsset(
  id: string,
  request: UpdateAssetRequest,
  etag: string | null,
): Promise<Asset> {
  return apiFetch<Asset>(`/assets/${id}`, { method: 'PUT', body: request, ...ifMatch(etag) })
}

/**
 * Issues an asset to somebody, transfers it, or takes it back.
 *
 * One route for all three (WP-2.2), the way a ticket's assignment is: a null
 * `assignedToUserId` is a deliberate instruction to take the asset back, not an omitted
 * field.
 */
export function assignAsset(
  id: string,
  assignedToUserId: string | null,
  note: string | null,
  etag: string | null,
): Promise<Asset> {
  return apiFetch<Asset>(`/assets/${id}/assignments`, {
    method: 'POST',
    body: { assignedToUserId, note },
    ...ifMatch(etag),
  })
}

/** The three lifecycle routes that name no other party — repair, return to service, retire. */
export type AssetLifecycleRoute = 'repairs' | 'returns-to-service' | 'retirements'

/**
 * Moves an asset through its lifecycle.
 *
 * One function for the three routes that differ only in their segment, matching the server,
 * where `MapLifecycle` maps them from one place for the same reason: written out three
 * times, the third copy is where the `If-Match` goes missing.
 */
export function moveAsset(
  id: string,
  route: AssetLifecycleRoute,
  note: string | null,
  etag: string | null,
): Promise<Asset> {
  return apiFetch<Asset>(`/assets/${id}/${route}`, {
    method: 'POST',
    body: { note },
    ...ifMatch(etag),
  })
}

/** The precondition header, when a version is in hand. */
function ifMatch(etag: string | null): { headers?: Record<string, string> } {
  return etag === null ? {} : { headers: { 'If-Match': etag } }
}

/**
 * A page of an asset's timeline, newest first.
 *
 * The whole page is asked for at once rather than paged through: an asset's history is
 * short — five operations exist and most equipment sees a handful — and the timeline is
 * read as a narrative rather than scanned. The envelope's `total` is what tells the
 * screen whether to mark the gap above the recorded line.
 */
export function fetchAssetHistory(
  id: string,
  pageSize: number,
  signal?: AbortSignal,
): Promise<PagedAssetHistory> {
  return apiFetch<PagedAssetHistory>(
    `/assets/${id}/history?page=1&pageSize=${String(pageSize)}`,
    signal ? { signal } : {},
  )
}

/**
 * The tickets raised about an asset, newest first (WP-2.5).
 *
 * Read through Helpdesk's public contract server-side, so the rows carry the ticket
 * summary rather than the full ticket — a row follows to `/tickets/{id}` for the detail.
 * Every linked ticket is here whatever its status, because an asset's support history is
 * the whole story of that machine.
 */
export function fetchAssetTickets(
  id: string,
  pageSize: number,
  signal?: AbortSignal,
): Promise<PagedTicketSummaries> {
  return apiFetch<PagedTicketSummaries>(
    `/assets/${id}/tickets?page=1&pageSize=${String(pageSize)}`,
    signal ? { signal } : {},
  )
}

/** Active asset types, for the register's type filter. */
export async function fetchAssetTypes(signal?: AbortSignal): Promise<AssetType[]> {
  const page = await apiFetch<{ items: AssetType[] }>(
    '/asset-types?pageSize=200',
    signal ? { signal } : {},
  )
  return page.items
}

/**
 * Active asset statuses, in the order an administrator put them in.
 *
 * The filter offers their names and addresses them by their immutable `code`
 * (`asset-query.ts` says why), so this read is what maps one to the other.
 */
export async function fetchAssetStatuses(signal?: AbortSignal): Promise<AssetStatus[]> {
  const page = await apiFetch<{ items: AssetStatus[] }>(
    '/asset-statuses?pageSize=200',
    signal ? { signal } : {},
  )
  return page.items
}

/**
 * People who can hold equipment, for the register's holder filter.
 *
 * Everybody, not only the queue: `AssignAssetHandler` asks Identity for no role because
 * equipment is issued to anybody in the organisation, which is the note
 * `AssetEndpoints` carries about who may *perform* an assignment versus who may receive
 * one.
 */
export function fetchAssetHolders(signal?: AbortSignal): Promise<UserSummary[]> {
  return apiFetch<UserSummary[]>('/users?limit=200', signal ? { signal } : {})
}
