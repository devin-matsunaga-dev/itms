import type {
  Asset,
  AssetHistoryEntry,
  AssetListItem,
  AssetStatus,
  AssetType,
  Department,
  Location,
  TicketSummary,
  UserSummary,
} from '@/lib/api/types'

/**
 * One shape of each asset payload, shared by the tests that read them.
 *
 * Factories rather than literals per test file: `AssetResponse` has twenty-five fields and
 * two copies of it would drift, which is exactly how a test ends up asserting against a
 * shape the server stopped sending.
 */

export const assetId = 'asset-1'
export const holderId = '22222222-2222-2222-2222-222222222222'
export const technicianId = '11111111-1111-1111-1111-111111111111'

export function assetType(overrides: Partial<AssetType> = {}): AssetType {
  return {
    id: 'type-laptop',
    name: 'Laptop',
    description: 'Portable workstations.',
    sortOrder: 20,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

export function assetStatus(overrides: Partial<AssetStatus> = {}): AssetStatus {
  return {
    id: 'status-deployed',
    code: 'deployed',
    name: 'Deployed',
    description: 'Issued and in service.',
    sortOrder: 20,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

/** The six seeded statuses, in lifecycle order — what `/asset-statuses` answers with. */
export const seededStatuses: AssetStatus[] = [
  assetStatus({ id: 'status-in-stock', code: 'in-stock', name: 'In Stock', sortOrder: 10 }),
  assetStatus(),
  assetStatus({ id: 'status-repair', code: 'repair', name: 'Repair', sortOrder: 30 }),
  assetStatus({ id: 'status-retired', code: 'retired', name: 'Retired', sortOrder: 40 }),
  assetStatus({ id: 'status-lost', code: 'lost', name: 'Lost', sortOrder: 50 }),
  assetStatus({ id: 'status-disposed', code: 'disposed', name: 'Disposed', sortOrder: 60 }),
]

export const department: Department = {
  id: 'dep-it',
  name: 'Information Technology',
  code: 'IT',
  description: null,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

export const location: Location = {
  id: 'loc-room-12',
  name: 'Room 12',
  kind: 'Room',
  parentId: 'loc-floor-2',
  path: 'Main Office → Admin Building → 2nd Floor → Room 12',
  depth: 3,
  description: null,
  childCount: 0,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

export const holder: UserSummary = {
  id: holderId,
  displayName: 'Jane Doe',
  email: 'jane@itms.local',
  departmentId: department.id,
  locationId: location.id,
  isActive: true,
  roles: ['User'],
}

export function assetListItem(overrides: Partial<AssetListItem> = {}): AssetListItem {
  return {
    id: assetId,
    assetTag: 'LAP-0042',
    name: 'Jane’s laptop',
    serialNumber: 'SN-99001',
    manufacturer: 'Dell',
    model: 'Latitude 5430',
    assetTypeId: 'type-laptop',
    assetTypeName: 'Laptop',
    assetStatusId: 'status-deployed',
    assetStatusCode: 'deployed',
    assetStatusName: 'Deployed',
    assignedToUserId: holderId,
    assignedToUserName: 'Jane Doe',
    departmentId: department.id,
    departmentName: department.name,
    locationId: location.id,
    locationPath: location.path,
    warrantyExpiresAt: '2026-09-20',
    createdAt: '2026-08-01T09:00:00Z',
    updatedAt: '2026-08-20T09:00:00Z',
    ...overrides,
  }
}

export function asset(overrides: Partial<Asset> = {}): Asset {
  return {
    id: assetId,
    assetTag: 'LAP-0042',
    name: 'Jane’s laptop',
    serialNumber: 'SN-99001',
    barcode: 'BC-4410',
    manufacturer: 'Dell',
    model: 'Latitude 5430',
    assetTypeId: 'type-laptop',
    assetTypeName: 'Laptop',
    assetStatusId: 'status-deployed',
    assetStatusCode: 'deployed',
    assetStatusName: 'Deployed',
    assignedToUserId: holderId,
    assignedToUserName: 'Jane Doe',
    departmentId: department.id,
    departmentName: department.name,
    locationId: location.id,
    locationPath: location.path,
    purchaseDate: '2023-09-20',
    warrantyExpiresAt: '2026-09-20',
    vendor: 'Island Computing',
    cost: 1499.5,
    notes: 'Docking station issued with it.',
    createdAt: '2026-08-01T09:00:00Z',
    updatedAt: '2026-08-20T09:00:00Z',
    ...overrides,
  }
}

export function historyEntry(overrides: Partial<AssetHistoryEntry> = {}): AssetHistoryEntry {
  return {
    id: 'history-1',
    kind: 'Assignment',
    fromValue: null,
    toValue: 'Jane Doe',
    note: null,
    occurredAt: '2026-08-20T09:00:00Z',
    sequence: 0,
    actorId: technicianId,
    actorName: 'Mark Reyes',
    ...overrides,
  }
}

export function ticketSummary(overrides: Partial<TicketSummary> = {}): TicketSummary {
  return {
    id: 'ticket-1',
    number: 'TKT-0001',
    subject: 'Laptop will not connect to Wi-Fi',
    status: 'New',
    priorityCode: 'high',
    priorityRank: 2,
    requesterId: holderId,
    assigneeId: null,
    relatedAssetId: assetId,
    isOpen: true,
    createdAt: '2026-08-25T09:00:00Z',
    dueAt: '2026-08-25T17:00:00Z',
    resolvedAt: null,
    closedAt: null,
    ...overrides,
  }
}
