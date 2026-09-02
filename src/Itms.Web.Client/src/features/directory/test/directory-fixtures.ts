import type {
  Department,
  DepartmentUsage,
  Location,
  LocationUsage,
  PagedDepartments,
} from '@/lib/api/types'

/** The departments and the tree the directory tests are written against. */

export function department(overrides: Partial<Department> = {}): Department {
  return {
    id: 'dep-it',
    name: 'Information Technology',
    code: 'IT',
    description: 'Keeps the lights blinking.',
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

export function departmentPage(items: Department[], total = items.length): PagedDepartments {
  return { items, total, page: 1, pageSize: 25, totalPages: 1, hasNextPage: false }
}

export function departmentUsage(overrides: Partial<DepartmentUsage> = {}): DepartmentUsage {
  return {
    departmentId: 'dep-it',
    name: 'Information Technology',
    isActive: true,
    references: [
      { entityName: 'assets', count: 4 },
      { entityName: 'tickets', count: 0 },
      { entityName: 'users', count: 2 },
    ],
    totalReferences: 6,
    ...overrides,
  }
}

export function location(overrides: Partial<Location> = {}): Location {
  return {
    id: 'loc-plant',
    name: 'Saipan Plant',
    kind: 'Site',
    parentId: 'loc-cuc',
    path: 'CUC → Saipan Plant',
    depth: 1,
    description: null,
    childCount: 2,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

export const organisation = location({
  id: 'loc-cuc',
  name: 'CUC',
  kind: 'Organization',
  parentId: null,
  path: 'CUC',
  depth: 0,
  childCount: 1,
})

export const room = location({
  id: 'loc-server',
  name: 'Server Room',
  kind: 'Room',
  parentId: 'loc-plant',
  path: 'CUC → Saipan Plant → Server Room',
  depth: 2,
  childCount: 0,
})

export function locationUsage(overrides: Partial<LocationUsage> = {}): LocationUsage {
  return {
    locationId: room.id,
    name: room.name,
    path: room.path,
    childCount: 0,
    references: [
      { entityName: 'assets', count: 0 },
      { entityName: 'tickets', count: 0 },
      { entityName: 'users', count: 0 },
    ],
    totalReferences: 0,
    canDelete: true,
    ...overrides,
  }
}
