import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes, useLocation } from 'react-router'
import { DepartmentsPage } from '@/features/directory/routes/departments-page'
import { ApiError } from '@/lib/api/client'
import type {
  CreateDepartmentRequest,
  Department,
  DepartmentUsage,
  PagedDepartments,
  UpdateDepartmentRequest,
} from '@/lib/api/types'
import type { DepartmentListQuery } from '@/features/directory/api/directory-api'
import {
  department,
  departmentPage,
  departmentUsage,
} from '@/features/directory/test/directory-fixtures'
import { renderWithProviders } from '@/test/render'

const fetchDepartmentPage = vi.fn<(query: DepartmentListQuery) => Promise<PagedDepartments>>()
const fetchDepartmentUsage = vi.fn<(id: string) => Promise<DepartmentUsage>>()
const createDepartment = vi.fn<(request: CreateDepartmentRequest) => Promise<Department>>()
const updateDepartment =
  vi.fn<(id: string, request: UpdateDepartmentRequest) => Promise<Department>>()
const setDepartmentActive = vi.fn<(id: string, active: boolean) => Promise<Department>>()

vi.mock('@/features/directory/api/directory-api', () => ({
  fetchDepartments: vi.fn(),
  fetchDepartmentPage: (query: DepartmentListQuery) => fetchDepartmentPage(query),
  fetchDepartmentUsage: (id: string) => fetchDepartmentUsage(id),
  createDepartment: (request: CreateDepartmentRequest) => createDepartment(request),
  updateDepartment: (id: string, request: UpdateDepartmentRequest) => updateDepartment(id, request),
  setDepartmentActive: (id: string, active: boolean) => setDepartmentActive(id, active),
  fetchLocations: vi.fn(),
  fetchLocationRoots: vi.fn(),
  fetchLocationChildren: vi.fn(),
  searchLocations: vi.fn(),
  fetchLocationAncestors: vi.fn(),
  fetchLocationSubtree: vi.fn(),
  fetchLocationUsage: vi.fn(),
  createLocation: vi.fn(),
  updateLocation: vi.fn(),
  moveLocation: vi.fn(),
  deleteLocation: vi.fn(),
}))

function AddressProbe(): React.JSX.Element {
  const { search } = useLocation()
  return <output data-testid="address">{search}</output>
}

function renderDepartments(route = '/administration/departments') {
  return renderWithProviders(
    <Routes>
      <Route
        path="/administration/departments"
        element={
          <>
            <DepartmentsPage />
            <AddressProbe />
          </>
        }
      />
      <Route path="/administration" element={<AddressProbe />} />
    </Routes>,
    { route },
  )
}

function address(): string {
  return screen.getByTestId('address').textContent ?? ''
}

beforeEach(() => {
  fetchDepartmentPage.mockReset()
  fetchDepartmentUsage.mockReset()
  createDepartment.mockReset()
  updateDepartment.mockReset()
  setDepartmentActive.mockReset()

  fetchDepartmentPage.mockResolvedValue(departmentPage([department()]))
  fetchDepartmentUsage.mockResolvedValue(departmentUsage())
  createDepartment.mockResolvedValue(department({ id: 'dep-new', name: 'Water Division' }))
  updateDepartment.mockResolvedValue(department({ name: 'IT Services' }))
  setDepartmentActive.mockResolvedValue(department({ isActive: false }))
})

describe('DepartmentsPage', () => {
  it('lists the departments with their code and status', async () => {
    renderDepartments()

    expect(await screen.findByText('Information Technology')).toBeInTheDocument()
    expect(screen.getByText('IT')).toBeInTheDocument()
    expect(screen.getByText('Active')).toBeInTheDocument()
  })

  it('hides retired departments until they are asked for', async () => {
    renderDepartments()
    await screen.findByText('Information Technology')

    expect(fetchDepartmentPage.mock.calls[0]?.[0].includeInactive).toBe(false)

    await userEvent.click(screen.getByRole('checkbox', { name: /show retired/i }))

    await waitFor(() => {
      expect(address()).toContain('includeInactive=true')
    })
    await waitFor(() => {
      expect(fetchDepartmentPage.mock.calls.at(-1)?.[0].includeInactive).toBe(true)
    })
  })

  it('asks the server for the search term rather than filtering in the browser', async () => {
    renderDepartments()
    await screen.findByText('Information Technology')

    await userEvent.type(screen.getByRole('searchbox'), 'water{Enter}')

    await waitFor(() => {
      expect(fetchDepartmentPage.mock.calls.at(-1)?.[0].search).toBe('water')
    })
  })

  it('creates a department from the page header action', async () => {
    renderDepartments()
    await screen.findByText('Information Technology')

    await userEvent.click(screen.getByRole('button', { name: /new department/i }))
    await userEvent.type(await screen.findByLabelText(/^name/i), 'Water Division')
    await userEvent.click(screen.getByRole('button', { name: 'Create department' }))

    await waitFor(() => {
      expect(createDepartment).toHaveBeenCalledWith({
        name: 'Water Division',
        code: null,
        description: null,
      })
    })
  })

  it('puts a duplicate name back on the field that caused it', async () => {
    // Only the database can answer whether a name is taken, so the refusal is what the
    // form shows — mapped onto the field rather than thrown into a toast.
    createDepartment.mockRejectedValue(
      new ApiError(
        409,
        { code: 'directory.duplicate_department_name', detail: "A department named 'IT' already exists." },
        "A department named 'IT' already exists.",
      ),
    )
    renderDepartments()
    await screen.findByText('Information Technology')

    await userEvent.click(screen.getByRole('button', { name: /new department/i }))
    await userEvent.type(await screen.findByLabelText(/^name/i), 'IT')
    await userEvent.click(screen.getByRole('button', { name: 'Create department' }))

    expect(await screen.findByText("A department named 'IT' already exists.")).toBeInTheDocument()
  })

  it('edits the department the row names', async () => {
    renderDepartments()
    await screen.findByText('Information Technology')

    await userEvent.click(screen.getByRole('button', { name: 'Edit' }))

    const name = await screen.findByLabelText(/^name/i)
    expect(name).toHaveValue('Information Technology')

    await userEvent.clear(name)
    await userEvent.type(name, 'IT Services')
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => {
      expect(updateDepartment).toHaveBeenCalledWith('dep-it', {
        name: 'IT Services',
        code: 'IT',
        description: 'Keeps the lights blinking.',
      })
    })
  })

  it('shows what still references a department before retiring it', async () => {
    renderDepartments()
    await screen.findByText('Information Technology')

    await userEvent.click(screen.getByRole('button', { name: 'Retire' }))

    expect(await screen.findByText('Still referenced by')).toBeInTheDocument()
    // A module reporting zero is shown rather than dropped.
    expect(screen.getByText('tickets')).toBeInTheDocument()
    expect(screen.getByText('4')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Retire', hidden: false }))

    await waitFor(() => {
      expect(setDepartmentActive).toHaveBeenCalledWith('dep-it', false)
    })
  })

  it('offers to bring a retired department back, and asks for no usage to do it', async () => {
    // Retiring is reversible, so the reversal needs no warning and no counts.
    fetchDepartmentPage.mockResolvedValue(departmentPage([department({ isActive: false })]))
    renderDepartments()
    await screen.findByText('Information Technology')

    expect(screen.getByText('Retired')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Bring back' }))
    await screen.findByRole('heading', { name: /bring back/i })

    expect(fetchDepartmentUsage).not.toHaveBeenCalled()
  })

  it('never offers to delete one, because departments are retire-only', async () => {
    // WP-0.6 settled it and WP-2.4 left it standing: a department is named by rows that
    // outlive it and no foreign key protects them.
    renderDepartments()
    await screen.findByText('Information Technology')

    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument()
  })

  it('offers a retry when the list cannot be read', async () => {
    fetchDepartmentPage.mockRejectedValue(new Error('network'))
    renderDepartments()

    expect(await screen.findByText('The departments could not be loaded.')).toBeInTheDocument()
  })

  it('offers the create action a second time from an empty list', async () => {
    fetchDepartmentPage.mockResolvedValue(departmentPage([]))
    renderDepartments()

    expect(await screen.findByText('No departments yet')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Create the first department' }),
    ).toBeInTheDocument()
  })
})
