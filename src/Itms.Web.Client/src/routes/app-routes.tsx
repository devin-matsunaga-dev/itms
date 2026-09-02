import { Navigate, Route, Routes } from 'react-router'
import { AppShell } from '@/components/layout/app-shell'
import { RequireAuth } from '@/features/auth/components/require-auth'
import { RequireRole } from '@/features/auth/components/require-role'
import { LoginPage } from '@/features/auth/routes/login-page'
import { DashboardPage } from '@/features/dashboard/routes/dashboard-page'
import { TicketsPage } from '@/features/helpdesk/routes/tickets-page'
import { NewTicketPage } from '@/features/helpdesk/routes/new-ticket-page'
import { TicketDetailPage } from '@/features/helpdesk/routes/ticket-detail-page'
import { AssetsPage } from '@/features/assets/routes/assets-page'
import { NewAssetPage } from '@/features/assets/routes/new-asset-page'
import { AssetDetailPage } from '@/features/assets/routes/asset-detail-page'
import { EditAssetPage } from '@/features/assets/routes/edit-asset-page'
import { UsersPage } from '@/features/users/routes/users-page'
import { UserDetailPage } from '@/features/users/routes/user-detail-page'
import { MonitoringPage } from '@/features/monitoring/routes/monitoring-page'
import { AlertsPage } from '@/features/alerts/routes/alerts-page'
import { KnowledgeBasePage } from '@/features/knowledge/routes/knowledge-base-page'
import { ReportsPage } from '@/features/reporting/routes/reports-page'
import { AdministrationPage } from '@/features/administration/routes/administration-page'
import { DepartmentsPage } from '@/features/directory/routes/departments-page'
import { LocationsPage } from '@/features/directory/routes/locations-page'
import { NotFoundPage } from '@/routes/not-found-page'
import { rolesForPath } from '@/routes/navigation'

/**
 * The route table. Every application route sits inside the shell, behind the session
 * gate, and behind the same role rule the sidebar filters on — `rolesForPath` is what
 * keeps the two from drifting, so a screen can never be listed but unreachable or
 * reachable but unlisted.
 */
export function AppRoutes(): React.JSX.Element {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>
          <Route index element={guarded('/', <DashboardPage />)} />
          <Route path="tickets" element={guarded('/tickets', <TicketsPage />)} />
          {/* The two screens under the queue take its nav entry's role rule, which is
              every signed-in account: a User raises and follows their own tickets, and
              the server's row filter is what decides which ones they can see. */}
          <Route path="tickets/new" element={guarded('/tickets', <NewTicketPage />)} />
          <Route path="tickets/:id" element={guarded('/tickets', <TicketDetailPage />)} />
          <Route path="assets" element={guarded('/assets', <AssetsPage />)} />
          {/* The create form takes the register's nav role rule, like every other asset
              route — and like `tickets/new`, it sits before the `:id` route so a literal
              "new" is never read as an asset id. */}
          <Route path="assets/new" element={guarded('/assets', <NewAssetPage />)} />
          {/* The asset detail takes the register's nav entry role rule — Technician and
              Admin — because every asset route is Technician-or-Admin server-side
              (SPEC.md §14). An end user's "what am I holding" view is WP-2.7's user page,
              which answers a different question through a different route. */}
          <Route path="assets/:id" element={guarded('/assets', <AssetDetailPage />)} />
          <Route path="assets/:id/edit" element={guarded('/assets', <EditAssetPage />)} />
          <Route path="users" element={guarded('/users', <UsersPage />)} />
          {/* The user 360 takes the directory's nav role rule — Technician and Admin —
              because `GET /api/v1/users/{id}` is Technician-only (WP-2.5). The two panel
              endpoints beneath it are open to the person they are about, but the profile
              read this screen opens with is not, so a self-service "what am I holding"
              view is a different screen for a different route and nobody has built it. */}
          <Route path="users/:id" element={guarded('/users', <UserDetailPage />)} />
          <Route path="monitoring" element={guarded('/monitoring', <MonitoringPage />)} />
          <Route path="alerts" element={guarded('/alerts', <AlertsPage />)} />
          <Route path="knowledge-base" element={guarded('/knowledge-base', <KnowledgeBasePage />)} />
          <Route path="reports" element={guarded('/reports', <ReportsPage />)} />
          <Route path="administration" element={guarded('/administration', <AdministrationPage />)} />
          {/* The directory management screens take Administration's role rule — Admin
              alone — because every department and location write is Admin-only
              server-side (WP-0.6, WP-2.4). The reads behind them are open to any signed-in
              account, which is what the pickers on other screens use. */}
          <Route
            path="administration/departments"
            element={guarded('/administration', <DepartmentsPage />)}
          />
          <Route
            path="administration/locations"
            element={guarded('/administration', <LocationsPage />)}
          />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Route>

      {/* Anything unmatched outside the shell — a stale bookmark, say — goes through
          the gate rather than rendering a 404 to someone who is not signed in. */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

/** Wraps a screen in the role rule its nav entry declares. */
function guarded(path: string, screen: React.ReactNode): React.JSX.Element {
  return <RequireRole allowed={rolesForPath(path)}>{screen}</RequireRole>
}
