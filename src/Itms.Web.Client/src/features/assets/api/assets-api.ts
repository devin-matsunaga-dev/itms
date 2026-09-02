import { apiFetch } from '@/lib/api/client'
import type {
  Asset,
  AssetStatus,
  AssetType,
  PagedAssetHistory,
  PagedAssets,
  PagedTicketSummaries,
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

/**
 * One asset in full.
 *
 * `GET /assets/{id}` answers with an `ETag` naming the row's version, and this read
 * deliberately drops it: nothing on a read-only screen has a precondition to state.
 * **`WP-2.6b` is where that changes** — its lifecycle calls each honour an `If-Match`, so
 * it swaps this to `apiRequest` and carries the tag through the hook, the way
 * `fetchTicket` already does.
 */
export function fetchAsset(id: string, signal?: AbortSignal): Promise<Asset> {
  return apiFetch<Asset>(`/assets/${id}`, signal ? { signal } : {})
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
