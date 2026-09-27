/*
 * File:    OperatorHomePage.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Grid Operator landing page. Minimal shell version — filled in with the
 *          identity screens (feature/identity-web-screens).
 */
import { useAuth } from '../../context/useAuth'

// Greets the Grid Operator. GridOperator-only via ProtectedRoute in App.jsx.
export default function OperatorHomePage() {
  const { user } = useAuth()

  return (
    <>
      <h1 className="h3">Grid Operator</h1>
      <p className="text-secondary">Welcome, {user.fullName}.</p>
    </>
  )
}
