# SPEC.md — Unified IT Management System, Production V1

> Reference document. Sessions do not read this file; `WORK_PACKAGES.md` carries the buildable detail. Keep this as the authority on *what V1 is* when editing work packages later.
>
> Source: `Unified IT Management System — Production V1 Feature Set`. This document restates it and adds the field-level and rule-level detail needed to implement it.

**Release principle:** a deliberately scoped first release that is operational in a real IT department from day one, leaving advanced automation, observability, integrations, and enterprise workflows for later. Once these capabilities are reliable, stop adding features and deploy.

**Core operating loop:** issue reported → ticket managed → user/device identified → technician investigates → device status checked → work documented → issue resolved → history retained.

---

## 1. Dashboard / IT operations overview

Answers one question: *what needs IT's attention right now?* An IT command center, not a BI platform.

- Open, unassigned, overdue, and SLA-risk ticket counts, each with a delta against the prior period and each clicking through to the equivalent filtered list.
- Tickets by status and by priority (donut, per `DESIGN.md`).
- Recently created / open tickets table: number, subject, requester, priority, age.
- Asset status summary: online, offline, in stock, maintenance, retired.
- Warranty and license expiration warnings, soonest first.
- Recent monitoring alerts with severity, device, location, and relative time.
- Quick actions: New Ticket, New Asset, Find Asset, Find User.

All figures are permission-scoped: a Technician sees the queue, a User sees only their own tickets.

## 2. Helpdesk / ticket management

The strongest V1 module.

**Ticket fields:** ticket number (sequential, human-readable, e.g. `INC-1052`), title, description, requester, department, category, priority, status, assigned technician, created/updated timestamps, due/SLA date, related asset, related alert, related KB articles, attachments, internal notes, user-visible comments, resolution notes, closed timestamp.

**Status workflow:** `New → Assigned → In Progress → Waiting → Resolved → Closed`, with optional `Cancelled` from any pre-Resolved state. Reopen from Resolved returns to In Progress; Closed is terminal. Transitions are enforced server-side.

**Priorities:** Critical, High, Medium, Low — configurable, each carrying a response and resolution target.

**Categories (configurable, seeded):** Hardware, Software, Network, Account/Access, Microsoft 365, Printer, Security, Other.

**Behaviors:** assignment and reassignment; priority and status changes; resolution recording; full ticket history. Every important change is logged — priority changes, reassignment, status transitions — with actor and timestamp.

**SLA (basic):** per-priority response and resolution targets measured against ticket creation. Waiting status pauses the resolution clock. Flags: approaching (80% consumed) and breached. No calendars, no business-hour schedules, no per-customer policies — those are V2.

## 3. IT asset management

A reliable source of truth for equipment and its lifecycle.

**Types:** desktop, laptop, server, switch, router, firewall, access point, printer, phone, tablet, UPS, other.

**Identification:** asset tag (unique, immutable), serial number, barcode, manufacturer, model, device type.

**Assignment:** user, department, location.

**Lifecycle:** status, purchase date, warranty expiration, vendor, cost, notes.

**Statuses:** In Stock, Deployed, Repair, Retired, Lost, Disposed.

**Asset history:** assignment, transfer, repair, return to service, retirement — each with actor, timestamp, and note. History is what keeps the inventory from decaying into a static spreadsheet.

**Network fields (for monitorable assets):** hostname, IP address, monitoring enabled flag, SNMP settings (read-only community/credentials, port).

## 4. User & department management

A central directory of the people IT supports, connected to operational records.

- Fields: name, username, email, department, job title, phone, location, manager, account status.
- User page shows assigned assets, open tickets, and previous tickets.
- Department records are used across tickets, assets, users, and reporting.

Acceptance shape: a technician searches a user and immediately sees their equipment and support history.

## 5. Locations

Configurable physical locations giving assets, users, alerts, and tickets operational context.

- Hierarchy: Organization → Site → Building → Floor/Area → Room.
- Supports offices, plants, remote facilities, pump stations, and similar operational sites.
- Geographic capability stays simple in V1; mapping is deferred.

## 6. Basic device monitoring

- ICMP/ping checks; online/offline state; response latency.
- Last successful and last failed check.
- Monitoring enabled/disabled per device.
- Availability, latency, outage, and recovery history.
- 24-hour, 7-day, and 30-day views.

## 7. Basic SNMP monitoring

Read-only, narrow by design: hostname, manufacturer, model, system uptime, device description, basic interface list and interface status. Interface utilization, traffic analytics, and configuration management stay out of V1.

## 8. Alerts

Monitoring events become actionable operational records.

- Fields: device, alert type, severity, start time, status, resolution, duration.
- Offline and recovery alerts, automatically paired.
- **Alert → Ticket** action that pre-populates device, location, timestamp, and monitoring context, and permanently links the two.

This integration is the point where separate modules become one platform.

## 9. Knowledge base

- Fields: title, category, content, author, last updated, Published/Draft state.
- Articles link to tickets.
- Seeded procedures: password reset, printer setup, network-drive mapping, new-computer setup.

## 10. Global search

One search across the system: ticket number and title, user and email, asset tag and serial number, hostname and IP address. Results group related assets, users, and tickets together. Disproportionately valuable because it is what makes the product feel unified.

## 11. Notifications

- In-app: ticket assignment, new comments, SLA approaching/exceeded, monitoring alerts and recovery.
- Email: ticket created, assigned, technician response, resolved.

Complex routing and additional channels are deferred.

## 12. Reports

Practical operational reporting, not a BI system.

- Helpdesk: opened/closed tickets, by category, department, technician; average resolution time; SLA compliance.
- Assets: by type, department, location, status; warranty expiration; unassigned assets.
- Monitoring: offline devices, availability, recent outages, frequently unavailable devices.
- CSV export on every report.

## 13. Administration

- Manage users, roles, departments, locations.
- Manage ticket categories and priorities.
- Manage asset types and statuses.
- Monitoring configuration and notification settings.

Nothing operational is hardcoded.

## 14. Authentication & permissions

| Role | Boundary |
|---|---|
| **Admin** | Complete system management, including administration and audit log |
| **Technician** | Operational access to tickets, assets, users, monitoring, alerts, KB, reports |
| **User** | Submit and view their own tickets; comment on them; no internal notes |

Three roles are enough for V1; granular RBAC is deferred.

## 15. Audit log

Mandatory in a production V1: user logins, ticket modifications, asset modifications, assignment changes, administrative changes, user and role changes. Append-only, with actor, timestamp, entity, and a field-level diff.

## 16. Import / export

- CSV asset import: tag, serial, manufacturer, model, type, user, department, location.
- CSV user import: username, name, email, department, location.
- Preview before import, validation, duplicate detection, and per-row error reporting.
- CSV export for major tables.

---

## Integration model

The value is in the relationships, not the modules.

| Area | Key relationships |
|---|---|
| Helpdesk | Requester ↔ User · Ticket ↔ Asset · Ticket ↔ Alert · Ticket ↔ KB |
| Assets | Asset ↔ User · Department · Location · Monitoring · Ticket history |
| Monitoring | Monitoring ↔ Asset · Alert ↔ Device · Alert → Ticket |
| Users | User ↔ Department · Location · Assigned assets · Ticket history |

## V1 now vs. V2+ later

| Build in V1 | Defer to V2+ |
|---|---|
| Tickets | Change management |
| Asset inventory & assignment | Procurement / purchase orders |
| Users, departments & locations | Contract management |
| Basic ping & SNMP monitoring | NetFlow / advanced observability |
| Alerts and Alert → Ticket | Automated remediation / event correlation |
| Knowledge base | AI knowledge search |
| Operational reports | Custom BI / report builder |
| CSV import/export | Automated discovery |
| Basic SLA | Complex SLA policies |
| Email/in-app notifications | Teams/SMS integrations |
| Global search | AI-assisted search |
| Audit logs | Advanced compliance reporting |
| Basic RBAC | Highly granular permissions |
| Manual asset records | AD/Entra/Intune synchronization |

## Delivery order

1. Authentication + Users + RBAC
2. Helpdesk
3. Asset management
4. User ↔ Asset ↔ Ticket relationships
5. Departments + Locations
6. Global search
7. Basic monitoring
8. Alerts + Alert → Ticket
9. Knowledge base
10. Dashboard + Reports
11. Notifications
12. CSV import/export
13. Audit logging
14. Administration / configuration
15. Production hardening: backups, logging, HTTPS, validation, rate limiting, health checks, migrations, recovery procedures

`ROADMAP.md` reorders two of these for practical reasons — minimal departments/locations and the audit spine move earlier, because both are painful to retrofit. Both deviations are recorded in `DECISIONS.md`.
