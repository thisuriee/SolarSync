/*
 * File:    HomeRedirect.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Sends a signed-in user to their role's home page. Used for "/" until M2's
 *          public landing page takes that path.
 */
import { Navigate } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import { homePathForRole } from '../utils/roles'

// Redirects by the role claim. Must sit inside ProtectedRoute, which guarantees a user.
export default function HomeRedirect() {
  const { user } = useAuth()
  return <Navigate to={homePathForRole(user.role)} replace />
}
