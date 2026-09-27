/*
 * File:    navItems.js
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Navigation entries and the roles that see each one. AppLayout filters this list
 *          by the signed-in role. Hiding a link is convenience, not authorisation — the
 *          API's [Authorize(Roles = ...)] is the rule (docs/PLUMBING-GUIDE.md §8 traps).
 *          Other members: add your entry here with the same roles as your route in App.jsx.
 */
import { ROLES } from '../utils/roles'

const { BACKOFFICE, GRID_OPERATOR } = ROLES

export const NAV_ITEMS = [
  // M1 — Identity & Access
  { to: '/backoffice', label: 'Home', roles: [BACKOFFICE] },
  { to: '/operator', label: 'Home', roles: [GRID_OPERATOR] },
  { to: '/webusers', label: 'Web Users', roles: [BACKOFFICE] },
  { to: '/prosumers', label: 'Prosumers', roles: [BACKOFFICE, GRID_OPERATOR] },
  { to: '/prosumers/pending', label: 'Pending Activations', roles: [BACKOFFICE] },

  // M2 — Nodes & Slots (proposed path; owner may rename)
  { to: '/nodes', label: 'Nodes', roles: [BACKOFFICE, GRID_OPERATOR] },

  // M3 — Reservations (proposed path; owner may rename)
  { to: '/reservations', label: 'Reservations', roles: [BACKOFFICE, GRID_OPERATOR] },

  // M4 — Dashboards & Fulfilment (proposed paths; owner may rename)
  { to: '/dashboard', label: 'Dashboard', roles: [BACKOFFICE, GRID_OPERATOR] },
  { to: '/history', label: 'Booking History', roles: [BACKOFFICE, GRID_OPERATOR] },
  { to: '/fulfilments', label: 'Fulfilment Log', roles: [BACKOFFICE, GRID_OPERATOR] },
]

// Returns only the entries the given role may see.
export function navItemsForRole(role) {
  return NAV_ITEMS.filter((item) => item.roles.includes(role))
}
