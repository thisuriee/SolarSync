/*
 * File:    roles.js
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Role strings exactly as the API emits them in the JWT "role" claim
 *          (docs/api-contract.md §1 — role strings), and each role's web home page.
 */

export const ROLES = Object.freeze({
  BACKOFFICE: 'Backoffice',
  GRID_OPERATOR: 'GridOperator',
  PROSUMER: 'Prosumer',
})

const HOME_PATHS = {
  [ROLES.BACKOFFICE]: '/backoffice',
  [ROLES.GRID_OPERATOR]: '/operator',
  [ROLES.PROSUMER]: '/use-mobile',
}

// Returns where a role lands after login — the role claim drives home routing
// (api-contract §2, POST /auth/login). Prosumers have no web screens (they use
// Android), so they land on a page pointing them to the mobile app.
export function homePathForRole(role) {
  return HOME_PATHS[role] ?? '/forbidden'
}
