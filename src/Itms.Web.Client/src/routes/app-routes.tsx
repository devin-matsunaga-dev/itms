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
import { AssetDetailPage } from '@/features/assets/routes/asset-detail-page'
import { UsersPage } from '@/features/users/routes/users-page'
import { MonitoringPage } from '@/features/monitoring/routes/monitoring-page'
import { AlertsPage } from '@/features/alerts/routes/alerts-page'
import { KnowledgeBasePage } from '@/features/knowledge/routes/knowledge-base-page'
import { ReportsPage } from '@/features/reporting/routes/reports-page'
import { AdministrationPage } from '@/features/administration/routes/administration-page'
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
          {/* The asset detail takes the register's nav entry role rule — Technician and
              Admin — because every asset route is Technician-or-Admin server-side
              (SPEC.md §14). An end user's "what am I holding" view is WP-2.7's user page,
              which answers a different question through a different route. */}
          <Route path="assets/:id" element={guarded('/assets', <AssetDetailPage />)} />
          <Route path="users" element={guarded('/users', <UsersPage />)} />
          <Route path="monitoring" element={guarded('/monitoring', <MonitoringPage />)} />
          <Route path="alerts" element={guarded('/alerts', <AlertsPage />)} />
          <Route path="knowledge-base" element={guarded('/knowledge-base', <KnowledgeBasePage />)} />
          <Route path="reports" element={guarded('/reports', <ReportsPage />)} />
          <Route path="administration" element={guarded('/administration', <AdministrationPage />)} />
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
