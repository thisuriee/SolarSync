/*
 * File:    ProtectedRoute.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Route guard taking an allowed-roles list (docs/PLUMBING-GUIDE.md §8 step 7).
 *          UX only — the real rule is [Authorize(Roles = ...)] on the API.
 */
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import Spinner from 'react-bootstrap/Spinner'
import { useAuth } from '../context/useAuth'

// Renders the child route only for a signed-in user whose role is in `roles`.
// No user → /login (remembering where they were going); wrong role → /forbidden.
// Hiding a page is not security: a Grid Operator who calls the API directly still
// gets 403 AUTH_FORBIDDEN_ROLE from the server.
export default function ProtectedRoute({ roles, children }) {
  const { user, isRestoring } = useAuth()
  const location = useLocation()

  // Wait for /auth/me on refresh, otherwise a valid session would flash to /login.
  if (isRestoring) {
    return (
      <div className="d-flex justify-content-center align-items-center vh-100">
        <Spinner animation="border" role="status">
          <span className="visually-hidden">Loading…</span>
        </Spinner>
      </div>
    )
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  if (roles && !roles.includes(user.role)) {
    return <Navigate to="/forbidden" replace />
  }

  // Works both as a wrapper (<ProtectedRoute><Page/></ProtectedRoute>)
  // and as a layout route with nested children (<Outlet/>).
  return children ?? <Outlet />
}
