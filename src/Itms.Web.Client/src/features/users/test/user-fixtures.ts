import type {
  AssetStatus,
  AssetSummary,
  Department,
  Location,
  PagedUsers,
  TicketSummary,
  UserSummary,
} from '@/lib/api/types'

/** The people, rooms, and equipment the directory tests are written against. */

export const department: Department = {
  id: 'dep-it',
  name: 'Information Technology',
  code: 'IT',
  description: null,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

export const room: Location = {
  id: 'loc-server',
  name: 'Server Room',
  kind: 'Room',
  parentId: 'loc-building',
  path: 'CUC → Saipan Plant → Server Room',
  depth: 2,
  description: null,
  childCount: 0,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

export const site: Location = {
  id: 'loc-plant',
  name: 'Saipan Plant',
  kind: 'Site',
  parentId: null,
  path: 'Saipan Plant',
  depth: 0,
  description: null,
  childCount: 3,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

export function user(overrides: Partial<UserSummary> = {}): UserSummary {
  return {
    id: 'user-1',
    displayName: 'Jane Santos',
    email: 'jane.santos@itms.local',
    departmentId: department.id,
    locationId: room.id,
    isActive: true,
    roles: ['Technician'],
    ...overrides,
  }
}

export function usersPage(items: UserSummary[], total = items.length): PagedUsers {
  return { items, total, page: 1, pageSize: 25, totalPages: 1, hasNextPage: false }
}

export const seededStatuses: AssetStatus[] = [
  {
    id: 'status-deployed',
    code: 'deployed',
    name: 'Deployed',
    description: null,
    sortOrder: 2,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  },
]

export function heldAsset(overrides: Partial<AssetSummary> = {}): AssetSummary {
  return {
    id: 'asset-1',
    assetTag: 'LAP-0042',
    name: 'Jane’s laptop',
    assetType: 'Laptop',
    // The immutable code, which is what the contract carries (WP-2.5).
    status: 'deployed',
    assignedToUserId: 'user-1',
    locationId: room.id,
    ...overrides,
  }
}

export function ticket(overrides: Partial<TicketSummary> = {}): TicketSummary {
  return {
    id: 'ticket-1',
    number: 'TKT-0007',
    subject: 'Laptop will not charge',
    status: 'InProgress',
    priorityCode: 'high',
    priorityRank: 2,
    requesterId: 'user-1',
    assigneeId: null,
    relatedAssetId: 'asset-1',
    isOpen: true,
    createdAt: '2026-08-30T09:00:00Z',
    dueAt: '2026-09-01T09:00:00Z',
    resolvedAt: null,
    closedAt: null,
    ...overrides,
  }
}
