import {
  Bell,
  BookOpen,
  FileText,
  HardDrive,
  LayoutDashboard,
  MonitorDot,
  Settings,
  Ticket,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { Roles, type Role } from '@/lib/roles'

export interface NavItem {
  path: string
  label: string
  icon: LucideIcon
  /** Roles the destination is offered to. Empty means every signed-in account. */
  roles: readonly Role[]
}

const operational: readonly Role[] = [Roles.admin, Roles.technician]
const everyone: readonly Role[] = []

/**
 * The nav, in the order DESIGN.md §3 fixes, and the single source of truth for which
 * roles a destination is offered to: the sidebar filters on it and the router guards
 * on it, so a screen can never be linkable but unlisted, or listed but unreachable.
 *
 * SPEC.md §14 draws the boundaries. A User submits and follows their own tickets and
 * reads the knowledge base; the operational surface — assets, the user directory,
 * monitoring, alerts, reports — is Technician and Admin; administration is Admin alone.
 *
 * None of this is access control. Every endpoint behind these screens evaluates its own
 * policy server-side, and hiding is never the enforcement (ARCHITECTURE.md §7).
 */
export const navItems: readonly NavItem[] = [
  { path: '/', label: 'Dashboard', icon: LayoutDashboard, roles: everyone },
  { path: '/tickets', label: 'Tickets', icon: Ticket, roles: everyone },
  { path: '/assets', label: 'Assets', icon: HardDrive, roles: operational },
  { path: '/users', label: 'Users', icon: Users, roles: operational },
  { path: '/monitoring', label: 'Monitoring', icon: MonitorDot, roles: operational },
  { path: '/alerts', label: 'Alerts', icon: Bell, roles: operational },
  { path: '/knowledge-base', label: 'Knowledge Base', icon: BookOpen, roles: everyone },
  { path: '/reports', label: 'Reports', icon: FileText, roles: operational },
  { path: '/administration', label: 'Administration', icon: Settings, roles: [Roles.admin] },
]

/** The roles a path is offered to, for the router's guard. */
export function rolesForPath(path: string): readonly Role[] {
  return navItems.find((item) => item.path === path)?.roles ?? everyone
}
