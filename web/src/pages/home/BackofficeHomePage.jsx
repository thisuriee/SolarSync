/*
 * File:    BackofficeHomePage.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Backoffice landing page. Minimal shell version — filled in with the
 *          identity screens (feature/identity-web-screens).
 */
import { useAuth } from '../../context/useAuth'

// Greets the Backoffice user. Backoffice-only via ProtectedRoute in App.jsx.
export default function BackofficeHomePage() {
  const { user } = useAuth()

  return (
    <>
      <h1 className="h3">Backoffice</h1>
      <p className="text-secondary">Welcome, {user.fullName}.</p>
    </>
  )
}
