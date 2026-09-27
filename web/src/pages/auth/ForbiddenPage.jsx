/*
 * File:    ForbiddenPage.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: 403 page shown when a signed-in user opens a page their role may not use.
 */
import { Link } from 'react-router-dom'
import Button from 'react-bootstrap/Button'
import { useAuth } from '../../context/useAuth'
import { homePathForRole } from '../../utils/roles'

// Explains the refusal and offers a way back to the user's own home page.
// Mirrors the API's 403 AUTH_FORBIDDEN_ROLE; the API refuses the call regardless.
export default function ForbiddenPage() {
  const { user } = useAuth()

  return (
    <div className="text-center py-5">
      <p className="display-4 fw-bold text-danger mb-2">403</p>
      <h1 className="h4">You don't have access to this page</h1>
      <p className="text-secondary">
        Your role ({user.role}) is not permitted to use this part of SmartSolar.
      </p>
      <Button as={Link} to={homePathForRole(user.role)}>
        Go to my home page
      </Button>
    </div>
  )
}
