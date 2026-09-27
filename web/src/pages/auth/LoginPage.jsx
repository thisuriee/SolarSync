/*
 * File:    LoginPage.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Single login screen for all roles. Sends the user to their role's home page.
 *          Account-status rules (Pending / Deactivated) are decided by the API, not here.
 */
import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import Alert from 'react-bootstrap/Alert'
import Button from 'react-bootstrap/Button'
import Card from 'react-bootstrap/Card'
import Form from 'react-bootstrap/Form'
import { useAuth } from '../../context/useAuth'
import { homePathForRole } from '../../utils/roles'

// Renders the login form. On success, routes by the role claim (or back to the page the
// user was sent away from). On failure, shows the API's `detail` unchanged — e.g.
// AUTH_ACCOUNT_PENDING — because the client never writes its own rule message.
export default function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  // Already signed in (e.g. pressed Back) — go home instead of showing the form.
  if (user) {
    return <Navigate to={homePathForRole(user.role)} replace />
  }

  // Submits credentials to POST /auth/login via AuthContext.
  async function handleSubmit(event) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const signedIn = await login(username.trim(), password)
      const from = location.state?.from?.pathname
      navigate(from ?? homePathForRole(signedIn.role), { replace: true })
    } catch (err) {
      setError(err.detail)
      setSubmitting(false)
    }
  }

  return (
    <div className="d-flex justify-content-center align-items-center vh-100 px-3">
      <Card className="shadow-sm w-100" style={{ maxWidth: 400 }}>
        <Card.Body className="p-4">
          <h1 className="h4 mb-1">SmartSolar</h1>
          <p className="text-secondary mb-4">Sign in to continue</p>

          {error && <Alert variant="danger">{error}</Alert>}

          <Form onSubmit={handleSubmit}>
            <Form.Group className="mb-3" controlId="username">
              <Form.Label>Username</Form.Label>
              <Form.Control
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                autoComplete="username"
                autoFocus
                required
              />
            </Form.Group>
            <Form.Group className="mb-4" controlId="password">
              <Form.Label>Password</Form.Label>
              <Form.Control
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                required
              />
            </Form.Group>
            <Button type="submit" className="w-100" disabled={submitting}>
              {submitting ? 'Signing in…' : 'Sign in'}
            </Button>
          </Form>
        </Card.Body>
      </Card>
    </div>
  )
}
