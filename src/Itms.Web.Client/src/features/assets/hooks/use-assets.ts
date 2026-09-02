import { useQuery, type UseQueryResult } from '@tanstack/react-query'
import type {
  AssetStatus,
  AssetType,
  PagedAssetHistory,
  PagedAssets,
  PagedTicketSummaries,
  UserSummary,
} from '@/lib/api/types'
import {
  fetchAsset,
  type AssetRead,
  fetchAssetHistory,
  fetchAssetHolders,
  fetchAssetStatuses,
  fetchAssetTickets,
  fetchAssetTypes,
  fetchAssets,
} from '../api/assets-api'
import { serializeAssetQuery, type AssetQuery } from '../lib/asset-query'

/** The register's cache keys. The query itself is the key — one address, one cached page. */
export const assetKeys = {
  all: ['assets'] as const,
  list: (query: AssetQuery) => ['assets', 'list', serializeAssetQuery(query).toString()] as const,
  detail: (id: string) => ['assets', 'asset', id] as const,
  history: (id: string) => ['assets', 'asset', id, 'history'] as const,
  tickets: (id: string) => ['assets', 'asset', id, 'tickets'] as const,
}

/** A page of the register. */
export function useAssets(query: AssetQuery): UseQueryResult<PagedAssets> {
  return useQuery({
    queryKey: assetKeys.list(query),
    queryFn: ({ signal }) => fetchAssets(query, signal),
    // An inventory is read far more often than it changes, and paging back and forth
    // should not refetch what was on screen a moment ago.
    staleTime: 30_000,
    placeholderData: (previous) => previous,
  })
}

/**
 * One asset in full, with the version tag every write on its screen sends as `If-Match`.
 *
 * `staleTime` is zero, unlike the register's: the lifecycle actions live on this screen,
 * and a cached copy of the state before an issue or a retirement is the one thing a detail
 * screen must never show.
 */
export function useAsset(id: string): UseQueryResult<AssetRead> {
  return useQuery({
    queryKey: assetKeys.detail(id),
    queryFn: ({ signal }) => fetchAsset(id, signal),
    enabled: id.length > 0,
  })
}

/**
 * How much of an asset's timeline and support history the detail screen holds.
 *
 * One page, generous enough that most equipment's whole life fits in it — the screen
 * marks the gap rather than paging, because a timeline is read as a narrative and a
 * "page 2 of this machine's history" control is a thing nobody reaches for.
 */
export const historyPageSize = 50
export const ticketsPageSize = 20

/** An asset's timeline. */
export function useAssetHistory(id: string): UseQueryResult<PagedAssetHistory> {
  return useQuery({
    queryKey: assetKeys.history(id),
    queryFn: ({ signal }) => fetchAssetHistory(id, historyPageSize, signal),
    enabled: id.length > 0,
  })
}

/** The tickets raised about an asset. */
export function useAssetTickets(id: string): UseQueryResult<PagedTicketSummaries> {
  return useQuery({
    queryKey: assetKeys.tickets(id),
    queryFn: ({ signal }) => fetchAssetTickets(id, ticketsPageSize, signal),
    enabled: id.length > 0,
  })
}

/**
 * Reference data for the filter bar.
 *
 * Types, statuses, and the user directory change about once a quarter, so they are held
 * for the session rather than refetched per filter interaction — the same budget the
 * ticket queue's reference data runs on.
 */
const referenceDataStaleTime = 10 * 60_000

export function useAssetTypes(): UseQueryResult<AssetType[]> {
  return useQuery({
    queryKey: ['assets', 'asset-types'],
    queryFn: ({ signal }) => fetchAssetTypes(signal),
    staleTime: referenceDataStaleTime,
  })
}

export function useAssetStatuses(): UseQueryResult<AssetStatus[]> {
  return useQuery({
    queryKey: ['assets', 'asset-statuses'],
    queryFn: ({ signal }) => fetchAssetStatuses(signal),
    staleTime: referenceDataStaleTime,
  })
}

/** People equipment can be issued to, for the holder filter. */
export function useAssetHolders(): UseQueryResult<UserSummary[]> {
  return useQuery({
    queryKey: ['identity', 'asset-holders'],
    queryFn: ({ signal }) => fetchAssetHolders(signal),
    staleTime: referenceDataStaleTime,
  })
}
